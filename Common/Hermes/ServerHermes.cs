using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Hermes
{
    /// <summary>
    /// Server half of Hermes. Passes private messages between players and holds gifts until their recipient is online.
    /// </summary>
    /// <remarks>
    /// The server is a go-between here: it never shows messages to anyone but the two people in the conversation.
    /// <para>
    /// Messages: kept in memory, the most recent <see cref="MESSAGES_PER_CONVERSATION"/> per pair of players. A player
    /// who was offline gets everything on their next login, with the ones they missed marked so their client can tell
    /// them. Nothing is written to disk, matching how public chat works on this server.
    /// </para>
    /// <para>
    /// Gifts: the sender's client takes the items off their map first (like bounties), so by the time a gift reaches
    /// the server, the server is the only place those items exist. That's why undelivered gifts ARE saved to disk.
    /// </para>
    /// </remarks>
    public class ServerHermes : Hermes
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// How many messages to remember for each pair of players. Older ones are dropped.
        /// </summary>
        private const int MESSAGES_PER_CONVERSATION = 50;

        private NetServer netServer;
        private ServerAuthenticator authenticator;
        private ServerUserManager userManager;

        /// <summary>
        /// Recent messages for each conversation. The key is both players' UUIDs, see <see cref="conversationKey"/>.
        /// </summary>
        private readonly Dictionary<string, List<DirectMessage>> conversations = new Dictionary<string, List<DirectMessage>>();

        /// <summary>
        /// IDs of messages whose recipient hasn't seen them yet because they were offline. Like a Python set().
        /// </summary>
        private readonly HashSet<string> undeliveredMessageIds = new HashSet<string>();

        /// <summary>
        /// Lock protecting <see cref="conversations"/> and <see cref="undeliveredMessageIds"/>.
        /// </summary>
        private readonly object messagesLock = new object();

        /// <summary>
        /// Gifts waiting for their recipient to log in. Saved to disk.
        /// </summary>
        private List<PendingGift> pendingGifts = new List<PendingGift>();

        /// <summary>
        /// Receipts for gifts we accepted, by token, so a sender whose connection dropped can find out whether their
        /// gift went. See Packets/EscrowPackets.proto. Protected by <see cref="giftsLock"/>.
        /// </summary>
        private Dictionary<string, AcceptedToken> acceptedTokens = new Dictionary<string, AcceptedToken>();

        /// <summary>
        /// Players who have logged in with a mod that understands gifts. Saved to disk. Protected by <see cref="giftsLock"/>.
        /// </summary>
        /// <remarks>
        /// The server only lets in mods with the current PROTOCOL_VERSION, and every mod with that version has Hermes.
        /// So anyone who has logged in since gifts were added can receive them, and anyone who hasn't (they're still on
        /// an old mod, or have never joined) can't. Like a Python set().
        /// </remarks>
        private HashSet<string> giftCapableUuids = new HashSet<string>();

        /// <summary>
        /// Players whose client confirms each gift once it's in their colony (<see cref="AckDeliveriesPacket"/>). Their
        /// gifts stay in <see cref="pendingGifts"/> until confirmed and are sent again on their next login if they never
        /// were. Older clients never confirm, so theirs are let go once sent, as before. Protected by <see cref="giftsLock"/>.
        /// </summary>
        private HashSet<string> ackingUuids = new HashSet<string>();

        /// <summary>
        /// How long a gift waits for its recipient to log in before it goes back to the sender.
        /// </summary>
        private const int GIFT_RETURN_DAYS = 30;

        /// <summary>
        /// How long a gift the recipient was asked about (<see cref="GiftsWaitingPacket"/>) waits for an answer before it
        /// goes back. Longer than the 30 days the client still lets the player take it (GiftOfferLetter), so a gift
        /// can never be taken after it has gone back, even with the two clocks a little apart.
        /// </summary>
        private const int UNANSWERED_RETURN_DAYS = 45;

        /// <summary>
        /// How long to keep gift receipts.
        /// </summary>
        private static readonly TimeSpan TOKEN_RECEIPT_LIFETIME = TimeSpan.FromDays(30);

        /// <summary>
        /// Lock protecting <see cref="pendingGifts"/> and <see cref="acceptedTokens"/>.
        /// </summary>
        private readonly object giftsLock = new object();

        /// <summary>
        /// Whether private messages are turned on (EnableDirectMessages in server.conf). Gifts work either way.
        /// </summary>
        private readonly bool directMessagesEnabled;

        /// <summary>
        /// Stops anyone flooding someone else's inbox. Same idea as chat's rate limit.
        /// </summary>
        private readonly RateLimiter messageRateLimiter;

        /// <summary>
        /// Stops anyone spamming gifts.
        /// </summary>
        private readonly RateLimiter giftRateLimiter;

        /// <summary>
        /// Initialises a new <see cref="ServerHermes"/> instance.
        /// </summary>
        /// <param name="netServer"><see cref="NetServer"/> to receive packets from</param>
        /// <param name="authenticator"><see cref="ServerAuthenticator"/> to check sessions with</param>
        /// <param name="userManager"><see cref="ServerUserManager"/> to check logins and find connections with</param>
        /// <param name="messageRateLimitMessages">Most private messages one user can send within <paramref name="messageRateLimitSeconds"/></param>
        /// <param name="messageRateLimitSeconds">Length of the message rate limit window in seconds</param>
        /// <param name="giftRateLimitGifts">Most gifts one user can send within <paramref name="giftRateLimitSeconds"/></param>
        /// <param name="giftRateLimitSeconds">Length of the gift rate limit window in seconds</param>
        /// <param name="directMessagesEnabled">Whether players can send each other private messages</param>
        public ServerHermes(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, int messageRateLimitMessages = 3, int messageRateLimitSeconds = 10, int giftRateLimitGifts = 2, int giftRateLimitSeconds = 60, bool directMessagesEnabled = true)
        {
            this.directMessagesEnabled = directMessagesEnabled;
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            this.messageRateLimiter = new RateLimiter(messageRateLimitMessages, TimeSpan.FromSeconds(messageRateLimitSeconds));
            this.giftRateLimiter = new RateLimiter(giftRateLimitGifts, TimeSpan.FromSeconds(giftRateLimitSeconds));

            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            userManager.OnLogin += loginHandler;
        }

        /// <summary>
        /// Builds the dictionary key for a conversation between two players.
        /// </summary>
        /// <remarks>
        /// Sorting the two UUIDs first means A-to-B and B-to-A give the same key, so both directions of a
        /// conversation end up in the same list. In Python: "|".join(sorted([a, b]))
        /// </remarks>
        private static string conversationKey(string uuidA, string uuidB)
        {
            return string.CompareOrdinal(uuidA, uuidB) < 0 ? uuidA + "|" + uuidB : uuidB + "|" + uuidA;
        }

        /// <summary>
        /// Gets a logged-in player's connection, or null if they're offline.
        /// </summary>
        private string connectionOf(string uuid)
        {
            return userManager.TryGetConnection(uuid, out string connectionId) ? connectionId : null;
        }

        /// <summary>
        /// Packs and sends a packet to one connection. Returns whether it went.
        /// </summary>
        private bool send(string connectionId, IMessage packet)
        {
            if (connectionId == null) return false;
            return netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        /// <summary>
        /// Handles incoming packets, sending each one to the right handler by its type.
        /// </summary>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerHermes).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            switch (typeUrl.Type)
            {
                case "SendDirectMessagePacket":
                    sendDirectMessagePacketHandler(connectionId, message.Unpack<SendDirectMessagePacket>());
                    break;
                case "SendGiftPacket":
                    sendGiftPacketHandler(connectionId, message.Unpack<SendGiftPacket>());
                    break;
                case "CheckTokensPacket":
                    checkTokensPacketHandler(connectionId, message.Unpack<CheckTokensPacket>());
                    break;
                case "AckDeliveriesPacket":
                    ackGiftsPacketHandler(connectionId, message.Unpack<AckDeliveriesPacket>());
                    break;
                case "GiftsWaitingPacket":
                    giftsWaitingPacketHandler(connectionId, message.Unpack<GiftsWaitingPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Tells a sender their message wasn't sent, and why.
        /// </summary>
        private void refuseMessage(string connectionId, SendDirectMessagePacket packet, string reason)
        {
            send(connectionId, new SendDirectMessageFailedPacket { RecipientUuid = packet.RecipientUuid, FailureMessage = reason });
        }

        /// <summary>
        /// Tells a sender their gift wasn't sent, and why. Their client then puts the items back on their map, so
        /// refusing is always safe: nothing is lost.
        /// </summary>
        private void refuseGift(string connectionId, SendGiftPacket packet, string reason)
        {
            send(connectionId, new SendGiftResponsePacket { Success = false, Token = packet.Token, FailureMessage = reason });
        }

        /// <summary>
        /// Handles a player sending a private message.
        /// </summary>
        private void sendDirectMessagePacketHandler(string connectionId, SendDirectMessagePacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            if (!directMessagesEnabled)
            {
                refuseMessage(connectionId, packet, "Private messages are turned off on this server");
                return;
            }

            // TryGetDisplayName is just a handy way to check the recipient is a real player on this server
            if (packet.RecipientUuid == packet.Uuid || !userManager.TryGetDisplayName(packet.RecipientUuid, out _))
            {
                refuseMessage(connectionId, packet, userManager.CompanionMode ? "That player hasn't linked to this companion server yet" : "That player doesn't exist");
                return;
            }

            string text = TextHelper.SanitiseRichText(packet.Text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > MAX_MESSAGE_LENGTH) text = text.Substring(0, MAX_MESSAGE_LENGTH);

            if (!messageRateLimiter.TryAct(packet.Uuid))
            {
                refuseMessage(connectionId, packet, "You are sending messages too fast!");
                return;
            }

            DirectMessage directMessage = new DirectMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderUuid = packet.Uuid,
                RecipientUuid = packet.RecipientUuid,
                Text = text,
                SentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            lock (messagesLock)
            {
                // Get this conversation's list, creating it the first time (like dict.setdefault in Python)
                string key = conversationKey(packet.Uuid, packet.RecipientUuid);
                if (!conversations.TryGetValue(key, out List<DirectMessage> history))
                {
                    history = new List<DirectMessage>();
                    conversations[key] = history;
                }

                history.Add(directMessage);
                if (history.Count > MESSAGES_PER_CONVERSATION)
                {
                    // Forget the oldest message, and stop tracking it as undelivered if it still was
                    undeliveredMessageIds.Remove(history[0].MessageId);
                    history.RemoveAt(0);
                }

                // Send it to the recipient. If they're offline (or the send fails), remember it as undelivered so
                // their next login can say "you missed this"
                DirectMessagePacket outgoing = new DirectMessagePacket { Message = directMessage };
                if (!send(connectionOf(packet.RecipientUuid), outgoing))
                {
                    undeliveredMessageIds.Add(directMessage.MessageId);
                }

                // And back to the sender, so their window shows the message as the server stored it
                send(connectionId, outgoing);
            }
        }

        /// <summary>
        /// Handles a player sending a gift.
        /// </summary>
        private void sendGiftPacketHandler(string connectionId, SendGiftPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                refuseGift(connectionId, packet, "Session is not valid");
                return;
            }
            if (packet.RecipientUuid == packet.Uuid || !userManager.TryGetDisplayName(packet.RecipientUuid, out _))
            {
                // On a companion, players only exist here once they've linked to it from their main server
                refuseGift(connectionId, packet, userManager.CompanionMode ? "That player hasn't linked to this companion server yet" : "That player doesn't exist");
                return;
            }
            // On a companion, gifts only go between players from the same main server
            if (!userManager.SameHomeServer(packet.Uuid, packet.RecipientUuid))
            {
                refuseGift(connectionId, packet, "That player is on a different server");
                return;
            }
            bool recipientCanReceive;
            int alreadyWaiting;
            lock (giftsLock)
            {
                recipientCanReceive = giftCapableUuids.Contains(packet.RecipientUuid);
                alreadyWaiting = pendingGifts.Count(p => p.RecipientUuid == packet.RecipientUuid);
            }
            if (!recipientCanReceive)
            {
                refuseGift(connectionId, packet, "That player's Phinix doesn't support gifts yet. They need to update their mod and join the server first");
                return;
            }
            if (alreadyWaiting >= MAX_PENDING_GIFTS_PER_RECIPIENT)
            {
                refuseGift(connectionId, packet, "That player already has too many gifts waiting for them. Try again once they've logged in");
                return;
            }

            if (packet.Items.Count == 0 || packet.Items.Count > MAX_ITEMS_PER_GIFT)
            {
                refuseGift(connectionId, packet, string.Format("A gift needs between 1 and {0} items", MAX_ITEMS_PER_GIFT));
                return;
            }
            if (!giftRateLimiter.TryAct(packet.Uuid))
            {
                refuseGift(connectionId, packet, "You are sending gifts too fast!");
                return;
            }

            string note = TextHelper.SanitiseRichText(packet.Note ?? "");
            if (note.Length > MAX_NOTE_LENGTH) note = note.Substring(0, MAX_NOTE_LENGTH);

            GiftDeliveryPacket gift = new GiftDeliveryPacket
            {
                SenderUuid = packet.Uuid,
                Note = note,
                SentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            gift.Items.AddRange(packet.Items);

            bool waiting;
            lock (giftsLock)
            {
                // Send it now if they're online, otherwise hold on to it (on disk, via Save) until they log in.
                // A player whose mod is too old can't log in at all (see PROTOCOL_VERSION), so they count as offline
                waiting = !deliverGift(packet.RecipientUuid, gift);

                // Receipt, in case our answer to the sender gets lost
                acceptedTokens[packet.Token] = new AcceptedToken { Token = packet.Token, Uuid = packet.Uuid, Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            }

            // We hold the gift now and the sender will let go of theirs, so get it on disk soon
            SaveRequests.Request();

            RaiseLogEntry(new LogEventArgs(string.Format("{0} sent a gift to {1}", packet.Uuid.Highlight(HighlightType.UUID), packet.RecipientUuid.Highlight(HighlightType.UUID))));

            // Tell the sender it worked, so their client gets rid of the items it was holding
            send(connectionId, new SendGiftResponsePacket { Success = true, Token = packet.Token, Waiting = waiting, RecipientUuid = packet.RecipientUuid });
        }

        /// <summary>
        /// Sends a gift to a player, or keeps it until they log in. A client that confirms gifts has it kept until it
        /// does, too. Must be called while holding <see cref="giftsLock"/>.
        /// </summary>
        /// <param name="recipientUuid">Who it's for</param>
        /// <param name="gift">The gift. Gets a new <see cref="GiftDeliveryPacket.GiftId"/></param>
        /// <returns>Whether it was sent straight away</returns>
        private bool deliverGift(string recipientUuid, GiftDeliveryPacket gift)
        {
            gift.GiftId = Guid.NewGuid().ToString();
            bool sent = send(connectionOf(recipientUuid), gift);
            if (!sent || ackingUuids.Contains(recipientUuid))
            {
                pendingGifts.Add(new PendingGift { RecipientUuid = recipientUuid, Gift = gift, Sent = sent });
            }
            SaveRequests.Request();
            return sent;
        }

        /// <summary>
        /// Sends some of a gift back to whoever sent it, as a gift from the player it was for. A gift that's already
        /// on its way back is never sent back again, so nothing bounces forever. Must be called while holding
        /// <see cref="giftsLock"/>.
        /// </summary>
        private void returnGift(PendingGift pending, IEnumerable<Trading.ProtoThing> items, GiftReturnReason reason)
        {
            List<Trading.ProtoThing> list = items.ToList();
            if (list.Count == 0 || pending.Gift.ReturnReason != GiftReturnReason.NotReturned) return;

            GiftDeliveryPacket returned = new GiftDeliveryPacket
            {
                SenderUuid = pending.RecipientUuid,
                Note = "",
                SentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ReturnReason = reason
            };
            returned.Items.AddRange(list);
            RaiseLogEntry(new LogEventArgs(string.Format("Sending {0} thing(s) of a gift back to {1} ({2})", list.Count, pending.Gift.SenderUuid.Highlight(HighlightType.UUID), reason)));
            deliverGift(pending.Gift.SenderUuid, returned);
        }

        /// <summary>
        /// Handles a client confirming gifts have arrived. The first one (sent right after logging in, even if empty)
        /// marks the player as confirming, so their gifts are kept until confirmed from then on. A gift turned away
        /// (the recipient blocked the sender), or the parts of one their game couldn't use, go back to the sender.
        /// </summary>
        private void ackGiftsPacketHandler(string connectionId, AckDeliveriesPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (giftsLock)
            {
                if (ackingUuids.Add(packet.Uuid)) SaveRequests.Request();

                foreach (DeliveryAck ack in packet.Acks)
                {
                    PendingGift pending = pendingGifts.FirstOrDefault(p => p.RecipientUuid == packet.Uuid && p.Gift.GiftId == ack.DeliveryId);
                    if (pending == null) continue;  // Already confirmed, or sent before we kept them
                    pendingGifts.Remove(pending);
                    SaveRequests.Request();

                    if (ack.Refused)
                    {
                        returnGift(pending, pending.Gift.Items, GiftReturnReason.Refused);
                    }
                    else
                    {
                        returnGift(pending, ack.UnusableItems.Where(i => i >= 0 && i < pending.Gift.Items.Count).Distinct().Select(i => pending.Gift.Items[i]), GiftReturnReason.Unusable);
                    }
                }
            }
        }

        /// <summary>
        /// Handles a client saying its player has been asked about these gifts and hasn't answered yet, so they can go
        /// back to the sender if that goes on too long (<see cref="UNANSWERED_RETURN_DAYS"/>).
        /// </summary>
        private void giftsWaitingPacketHandler(string connectionId, GiftsWaitingPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (giftsLock)
            {
                foreach (PendingGift pending in pendingGifts.Where(p => p.RecipientUuid == packet.Uuid && !p.Waiting && packet.GiftIds.Contains(p.Gift.GiftId)))
                {
                    pending.Waiting = true;
                    SaveRequests.Request();
                }
            }
        }

        /// <summary>
        /// Sends gifts nobody collected back to their senders after <see cref="GIFT_RETURN_DAYS"/>, and gifts the
        /// recipient was asked about and never answered after <see cref="UNANSWERED_RETURN_DAYS"/>. Other gifts that
        /// went out and are only waiting to be confirmed stay, since they may well have arrived. Must be called while
        /// holding <see cref="giftsLock"/>.
        /// </summary>
        private void returnExpiredGifts()
        {
            long cutoff = DateTimeOffset.UtcNow.AddDays(-GIFT_RETURN_DAYS).ToUnixTimeSeconds();
            long unansweredCutoff = DateTimeOffset.UtcNow.AddDays(-UNANSWERED_RETURN_DAYS).ToUnixTimeSeconds();
            foreach (PendingGift pending in pendingGifts.Where(p => p.Gift.ReturnReason == GiftReturnReason.NotReturned).ToList())
            {
                if (pending.Waiting && pending.Gift.SentTime < unansweredCutoff)
                {
                    pendingGifts.Remove(pending);
                    returnGift(pending, pending.Gift.Items, GiftReturnReason.Unanswered);
                }
                else if (!pending.Sent && pending.Gift.SentTime < cutoff)
                {
                    pendingGifts.Remove(pending);
                    returnGift(pending, pending.Gift.Items, GiftReturnReason.Expired);
                }
            }
        }

        /// <summary>
        /// Answers a client asking which of its held gift tokens we accepted.
        /// </summary>
        private void checkTokensPacketHandler(string connectionId, CheckTokensPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            TokenResultsPacket results = new TokenResultsPacket();
            results.Checked.AddRange(packet.Tokens);
            lock (giftsLock)
            {
                results.Accepted.AddRange(packet.Tokens.Where(token => acceptedTokens.TryGetValue(token, out AcceptedToken receipt) && receipt.Uuid == packet.Uuid));
            }
            send(connectionId, results);
        }

        /// <summary>
        /// Catches a player up when they log in: their recent messages (marking any they missed) and any waiting gifts.
        /// </summary>
        private void loginHandler(object sender, ServerLoginEventArgs args)
        {
            // When messages are off, the sync still goes out (so the mod learns they're off) but carries no messages
            SyncDirectMessagesPacket sync = new SyncDirectMessagesPacket { Enabled = directMessagesEnabled };
            lock (messagesLock)
            {
                // Every message in every conversation this player is part of
                IEnumerable<DirectMessage> theirs = conversations.Values
                    .SelectMany(history => history)
                    .Where(m => m.SenderUuid == args.Uuid || m.RecipientUuid == args.Uuid);
                sync.Messages.AddRange(theirs);

                // Messages sent TO them that never reached them
                List<string> missed = sync.Messages
                    .Where(m => m.RecipientUuid == args.Uuid && undeliveredMessageIds.Contains(m.MessageId))
                    .Select(m => m.MessageId)
                    .ToList();
                sync.MissedMessageIds.AddRange(missed);

                // Only stop tracking them as undelivered once the sync has actually gone out
                if (send(args.ConnectionId, sync))
                {
                    undeliveredMessageIds.ExceptWith(missed);
                }
            }

            lock (giftsLock)
            {
                // Getting this far means their mod is new enough for gifts (see giftCapableUuids)
                giftCapableUuids.Add(args.Uuid);

                // ToList() makes a copy so we can remove from the real list while looping
                foreach (PendingGift pending in pendingGifts.Where(p => p.RecipientUuid == args.Uuid).ToList())
                {
                    if (!send(args.ConnectionId, pending.Gift)) continue;

                    // A client that confirms gifts keeps it on the list until it does (ackGiftsPacketHandler)
                    if (ackingUuids.Contains(args.Uuid)) pending.Sent = true;
                    else pendingGifts.Remove(pending);
                }
            }
        }

        /// <summary>
        /// Saves undelivered gifts to disk, overwriting the file if it exists.
        /// </summary>
        /// <param name="path">File to save to</param>
        public void Save(string path)
        {
            lock (giftsLock)
            {
                // Gifts left uncollected too long go back to their senders first
                returnExpiredGifts();

                HermesStore store = new HermesStore();
                store.PendingGifts.AddRange(pendingGifts);
                store.AckingUuids.AddRange(ackingUuids);

                // Forget old receipts, then save the rest
                long cutoff = DateTimeOffset.UtcNow.Subtract(TOKEN_RECEIPT_LIFETIME).ToUnixTimeSeconds();
                acceptedTokens = acceptedTokens.Values.Where(receipt => receipt.Time >= cutoff).ToDictionary(receipt => receipt.Token, receipt => receipt);
                store.AcceptedTokens.AddRange(acceptedTokens.Values);
                store.GiftCapableUuids.AddRange(giftCapableUuids);

                // Write to a temporary file first, then swap it in, so a crash mid-write can't wreck the old save
                SafeFile.WriteAllBytes(path, store.ToByteArray());

                RaiseLogEntry(new LogEventArgs(string.Format("Saved {0} undelivered gift{1}", pendingGifts.Count, pendingGifts.Count == 1 ? "" : "s")));
            }
        }

        /// <summary>
        /// Loads undelivered gifts from disk. Starts empty if the file doesn't exist yet.
        /// </summary>
        /// <param name="path">File to load from</param>
        public void Load(string path)
        {
            lock (giftsLock)
            {
                // A damaged file throws instead of starting empty, so the gifts held in it never get saved over (see SafeFile)
                if (!SafeFile.TryRead(path, HermesStore.Parser.ParseFrom, out HermesStore store, out string usedPath))
                {
                    RaiseLogEntry(new LogEventArgs("No gift database, starting empty"));
                    return;
                }
                if (usedPath != path) RaiseLogEntry(new LogEventArgs(string.Format("The gift database was damaged or missing, so loaded {0} instead", usedPath), LogLevel.WARNING));
                pendingGifts = store.PendingGifts.ToList();
                acceptedTokens = store.AcceptedTokens.ToDictionary(receipt => receipt.Token, receipt => receipt);
                giftCapableUuids = new HashSet<string>(store.GiftCapableUuids);
                ackingUuids = new HashSet<string>(store.AckingUuids);
                RaiseLogEntry(new LogEventArgs(string.Format("Loaded {0} undelivered gift{1}", pendingGifts.Count, pendingGifts.Count == 1 ? "" : "s")));
            }
        }
    }
}
