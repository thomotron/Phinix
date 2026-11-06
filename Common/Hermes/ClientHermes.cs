using System;
using System.Collections.Generic;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Trading;
using UserManagement;
using Utils;

namespace Hermes
{
    /// <summary>
    /// Client half of Hermes. Keeps this player's private conversations and sends messages and gifts.
    /// </summary>
    /// <remarks>
    /// Modelled on <c>Agora.ClientAgora</c>. The client never decides anything important: it asks the server, and the
    /// server's answer arrives later as an event.
    /// </remarks>
    public class ClientHermes : Hermes
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Raised whenever a conversation changes (a new message, a login sync, or a disconnect), so the window redraws.
        /// </summary>
        public event EventHandler OnConversationsChanged;

        /// <summary>
        /// Raised for each new message someone ELSE sent us while we're online, so the mod can pop up a notification.
        /// Our own messages coming back from the server don't raise this.
        /// </summary>
        public event EventHandler<DirectMessage> OnDirectMessageReceived;

        /// <summary>
        /// Raised once after logging in, with the messages we missed while offline (possibly none).
        /// </summary>
        public event EventHandler<DirectMessage[]> OnMissedDirectMessages;

        /// <summary>
        /// Raised when the server refuses one of our messages, with the reason.
        /// </summary>
        public event EventHandler<SendDirectMessageFailedPacket> OnSendDirectMessageFailed;

        /// <summary>
        /// Raised when the server answers one of our gifts. The token says which gift, so the mod knows which held
        /// items to destroy (success) or put back (failure).
        /// </summary>
        public event EventHandler<SendGiftResponsePacket> OnSendGiftResponse;

        /// <summary>
        /// Raised when someone's gift arrives for us.
        /// </summary>
        public event EventHandler<GiftDeliveryPacket> OnGiftReceived;

        /// <summary>
        /// Whether the server has private messages turned on. Comes with the login sync; false until then.
        /// Gifts work either way.
        /// </summary>
        public bool DirectMessagesEnabled { get; private set; }

        /// <summary>
        /// Whether the server has Hermes at all (gifts and private messages). A Hermes server always sends the login
        /// sync, even with messages turned off; an original Phinix server never does, so this stays false there.
        /// </summary>
        public bool Available { get; private set; }

        private NetClient netClient;
        private ClientAuthenticator authenticator;
        private ClientUserManager userManager;

        /// <summary>
        /// Our conversations, keyed by the OTHER player's UUID. Each list is oldest message first.
        /// </summary>
        private readonly Dictionary<string, List<DirectMessage>> conversations = new Dictionary<string, List<DirectMessage>>();

        /// <summary>
        /// How many unread messages each conversation has, keyed by the other player's UUID.
        /// </summary>
        private readonly Dictionary<string, int> unread = new Dictionary<string, int>();

        /// <summary>
        /// Lock protecting <see cref="conversations"/> and <see cref="unread"/>. Packets arrive on the network
        /// thread while the window reads them on the game thread.
        /// </summary>
        private readonly object conversationsLock = new object();

        /// <summary>
        /// Initialises a new <see cref="ClientHermes"/> instance.
        /// </summary>
        public ClientHermes(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;

            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netClient.OnDisconnect += disconnectHandler;
        }

        /// <summary>
        /// UUIDs of everyone we have a conversation with, most recently active first.
        /// </summary>
        public string[] GetConversationPartners()
        {
            lock (conversationsLock)
            {
                return conversations
                    .OrderByDescending(pair => pair.Value.Count > 0 ? pair.Value[pair.Value.Count - 1].SentTime : 0)
                    .Select(pair => pair.Key)
                    .ToArray();
            }
        }

        /// <summary>
        /// A copy of our conversation with someone, oldest first. Empty if we've never talked.
        /// </summary>
        /// <param name="otherUuid">The other player's UUID</param>
        public DirectMessage[] GetConversation(string otherUuid)
        {
            lock (conversationsLock)
            {
                return conversations.TryGetValue(otherUuid, out List<DirectMessage> history) ? history.ToArray() : new DirectMessage[0];
            }
        }

        /// <summary>
        /// How many unread messages a conversation has.
        /// </summary>
        public int UnreadCount(string otherUuid)
        {
            lock (conversationsLock)
            {
                return unread.TryGetValue(otherUuid, out int count) ? count : 0;
            }
        }

        /// <summary>
        /// Unread messages across all conversations. Part of the Inbox tab's "New (n)" number.
        /// </summary>
        public int TotalUnread()
        {
            lock (conversationsLock)
            {
                return unread.Values.Sum();
            }
        }

        /// <summary>
        /// Marks a conversation as read, e.g. because its window is open.
        /// </summary>
        public void MarkRead(string otherUuid)
        {
            lock (conversationsLock)
            {
                unread.Remove(otherUuid);
            }
        }

        /// <summary>
        /// Makes sure a conversation exists even with no messages, so the window can show it straight away after
        /// "Send message" is picked from someone's menu.
        /// </summary>
        public void StartConversation(string otherUuid)
        {
            lock (conversationsLock)
            {
                if (!conversations.ContainsKey(otherUuid)) conversations[otherUuid] = new List<DirectMessage>();
            }
            OnConversationsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Asks the server to send a private message. It shows up in our conversation once the server echoes it back.
        /// </summary>
        /// <param name="recipientUuid">Who to send it to</param>
        /// <param name="text">What to say</param>
        public void SendDirectMessage(string recipientUuid, string text)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            send(new SendDirectMessagePacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                RecipientUuid = recipientUuid,
                Text = text
            });
        }

        /// <summary>
        /// Sends items to another player. The caller must already have taken the items off the map and kept them
        /// under <paramref name="token"/>; the answer arrives through <see cref="OnSendGiftResponse"/>.
        /// </summary>
        /// <param name="token">Token the held items are kept under</param>
        /// <param name="recipientUuid">Who to send them to</param>
        /// <param name="items">The items, already converted for sending</param>
        /// <param name="note">Short message to go with them</param>
        /// <returns>Whether the request went out. If false, the caller should put the items back.</returns>
        public bool SendGift(string token, string recipientUuid, IEnumerable<ProtoThing> items, string note)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            SendGiftPacket packet = new SendGiftPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                RecipientUuid = recipientUuid,
                Note = note ?? ""
            };
            packet.Items.AddRange(items);

            send(packet);
            return true;
        }

        /// <summary>
        /// Raised when the server answers <see cref="CheckTokens"/>.
        /// </summary>
        public event EventHandler<TokenResultsPacket> OnTokenResults;

        /// <summary>
        /// Asks the server which of these held-item tokens it accepted. Used after logging back in, to settle items
        /// that were mid-hand-over when the connection dropped (see Packets/EscrowPackets.proto).
        /// </summary>
        /// <param name="tokens">Tokens we're still holding items for</param>
        /// <summary>
        /// Tells the server these deliveries are in our colony, so it can stop holding them, and which of their things
        /// this game couldn't use, so it can send them back. Calling it with nothing right after logging in tells the
        /// server we confirm, so it keeps our deliveries until we do.
        /// </summary>
        /// <param name="acks">One per delivery</param>
        public void AckDeliveries(IEnumerable<DeliveryAck> acks)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            AckDeliveriesPacket packet = new AckDeliveriesPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid };
            packet.Acks.AddRange(acks);
            send(packet);
        }

        /// <summary>
        /// Tells the server the player has been asked whether to take these gifts and hasn't answered yet, so it can send
        /// them back if they never do. An older server ignores this and keeps them until answered.
        /// </summary>
        /// <param name="giftIds">The gifts' IDs</param>
        public void ReportGiftsWaiting(IEnumerable<string> giftIds)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            GiftsWaitingPacket packet = new GiftsWaitingPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid };
            packet.GiftIds.AddRange(giftIds);
            send(packet);
        }

        public void CheckTokens(IEnumerable<string> tokens)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            CheckTokensPacket packet = new CheckTokensPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid };
            packet.Tokens.AddRange(tokens);
            if (packet.Tokens.Count == 0) return;  // Nothing to ask about

            send(packet);
        }

        /// <summary>
        /// Packs and sends a packet to the server.
        /// </summary>
        private void send(IMessage packet)
        {
            netClient.Send(MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        /// <summary>
        /// Forgets every conversation on disconnect. The server sends them all again on the next login.
        /// </summary>
        private void disconnectHandler(object sender, EventArgs e)
        {
            lock (conversationsLock)
            {
                conversations.Clear();
                unread.Clear();
            }
            DirectMessagesEnabled = false;
            Available = false;
            OnConversationsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles incoming packets, sending each one to the right handler by its type.
        /// </summary>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientHermes).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            switch (typeUrl.Type)
            {
                case "DirectMessagePacket":
                    directMessagePacketHandler(message.Unpack<DirectMessagePacket>().Message);
                    break;
                case "SyncDirectMessagesPacket":
                    syncDirectMessagesPacketHandler(message.Unpack<SyncDirectMessagesPacket>());
                    break;
                case "SendDirectMessageFailedPacket":
                    OnSendDirectMessageFailed?.Invoke(this, message.Unpack<SendDirectMessageFailedPacket>());
                    break;
                case "SendGiftResponsePacket":
                    OnSendGiftResponse?.Invoke(this, message.Unpack<SendGiftResponsePacket>());
                    break;
                case "TokenResultsPacket":
                    OnTokenResults?.Invoke(this, message.Unpack<TokenResultsPacket>());
                    break;
                case "GiftDeliveryPacket":
                    OnGiftReceived?.Invoke(this, message.Unpack<GiftDeliveryPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Works out who the "other person" in a message is from our point of view.
        /// </summary>
        private string otherParty(DirectMessage message)
        {
            return message.SenderUuid == userManager.Uuid ? message.RecipientUuid : message.SenderUuid;
        }

        /// <summary>
        /// Adds one new message to its conversation.
        /// </summary>
        private void directMessagePacketHandler(DirectMessage message)
        {
            bool fromThem = message.SenderUuid != userManager.Uuid;
            string other = otherParty(message);

            lock (conversationsLock)
            {
                if (!conversations.TryGetValue(other, out List<DirectMessage> history))
                {
                    history = new List<DirectMessage>();
                    conversations[other] = history;
                }
                history.Add(message);

                // Only messages from them count as unread; our own echo obviously doesn't
                if (fromThem) unread[other] = (unread.TryGetValue(other, out int count) ? count : 0) + 1;
            }

            OnConversationsChanged?.Invoke(this, EventArgs.Empty);
            if (fromThem) OnDirectMessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Rebuilds every conversation from the login sync, and reports the ones we missed.
        /// </summary>
        private void syncDirectMessagesPacketHandler(SyncDirectMessagesPacket packet)
        {
            // A HashSet makes "is this id in the missed list" a quick lookup, like a Python set
            DirectMessagesEnabled = packet.Enabled;
            Available = true;
            HashSet<string> missedIds = new HashSet<string>(packet.MissedMessageIds);
            List<DirectMessage> missed = new List<DirectMessage>();

            lock (conversationsLock)
            {
                conversations.Clear();
                unread.Clear();

                foreach (DirectMessage message in packet.Messages.OrderBy(m => m.SentTime))
                {
                    string other = otherParty(message);
                    if (!conversations.TryGetValue(other, out List<DirectMessage> history))
                    {
                        history = new List<DirectMessage>();
                        conversations[other] = history;
                    }
                    history.Add(message);

                    if (missedIds.Contains(message.MessageId))
                    {
                        missed.Add(message);
                        unread[other] = (unread.TryGetValue(other, out int count) ? count : 0) + 1;
                    }
                }
            }

            OnConversationsChanged?.Invoke(this, EventArgs.Empty);
            OnMissedDirectMessages?.Invoke(this, missed.ToArray());
        }
    }
}
