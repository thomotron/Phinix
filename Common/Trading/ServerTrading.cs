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

namespace Trading
{
    public class ServerTrading : Trading, IPersistent
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <see cref="NetServer"/> instance to bind events and send data through.
        /// </summary>
        private NetServer netServer;

        /// <summary>
        /// <see cref="ServerAuthenticator"/> instance to check session validity.
        /// </summary>
        private ServerAuthenticator authenticator;

        /// <summary>
        /// <see cref="ServerUserManager"/> instance to check login state and trade preferences through.
        /// </summary>
        private ServerUserManager userManager;

        /// <summary>
        /// Collection of active trades organised by trade ID.
        /// </summary>
        private Dictionary<string, Trade> activeTrades;
        /// <summary>
        /// Collection of completed trades organised by trade ID.
        /// Trades are stored here until both parties have been notified that they have been completed.
        /// </summary>
        private Dictionary<string, CompletedTrade> completedTrades;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <see cref="activeTrades"/> and <see cref="completedTrades"/>.
        /// </summary>
        private object tradeDataLock = new object();

        /// <summary>
        /// How long to keep receipts. A player who's away longer than this gets their held items put back, which
        /// could duplicate them, but 30 days is far longer than anyone sits mid-trade.
        /// </summary>
        private static readonly TimeSpan TOKEN_RECEIPT_LIFETIME = TimeSpan.FromDays(30);

        /// <summary>
        /// Receipts for every trade offer we accepted, by token (see Packets/EscrowPackets.proto). Protected by
        /// <see cref="tradeDataLock"/>, so a receipt and the offer it's for are always updated together.
        /// </summary>
        private Dictionary<string, AcceptedToken> acceptedTokens = new Dictionary<string, AcceptedToken>();

        /// <summary>
        /// Players whose client confirms each trade result once the items are in their colony
        /// (<see cref="AckDeliveriesPacket"/>). Their finished trades stay in <see cref="completedTrades"/> until
        /// confirmed and are sent again on their next login if they never were, so a result lost on the way still
        /// arrives. Older clients never confirm, so theirs are let go once sent, as before. Protected by
        /// <see cref="tradeDataLock"/>.
        /// </summary>
        private HashSet<string> ackingUuids = new HashSet<string>();

        /// <summary>
        /// Limits how often each user can open a trade. Every new trade pops a letter up for the other player, so
        /// without this, opening and cancelling trades over and over would be an easy way to spam someone.
        /// </summary>
        private readonly RateLimiter tradeRateLimiter;

        /// <param name="tradeRateLimitTrades">Most trades one user can open within <paramref name="tradeRateLimitSeconds"/></param>
        /// <param name="tradeRateLimitSeconds">Length of the trade rate limit window in seconds</param>
        public ServerTrading(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, int tradeRateLimitTrades = 3, int tradeRateLimitSeconds = 60)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            this.tradeRateLimiter = new RateLimiter(tradeRateLimitTrades, TimeSpan.FromSeconds(tradeRateLimitSeconds));

            this.activeTrades = new Dictionary<string, Trade>();
            this.completedTrades = new Dictionary<string, CompletedTrade>();

            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            userManager.OnLogin += loginEventHandler;
        }

        public ServerTrading(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, string tradeDatabasePath) : this(netServer, authenticator, userManager)
        {
            Load(tradeDatabasePath);
        }

        /// <inheritdoc />
        /// <summary>
        /// Saves the active trades to the given file, overwriting if it exists.
        /// </summary>
        /// <param name="path">Active trade store path</param>
        public void Save(string path)
        {
            lock (tradeDataLock)
            {
                // Create the store from the trade list
                // Drop receipts nobody is ever going to ask about
                long cutoff = DateTimeOffset.UtcNow.Subtract(TOKEN_RECEIPT_LIFETIME).ToUnixTimeSeconds();
                foreach (string oldToken in acceptedTokens.Values.Where(receipt => receipt.Time < cutoff).Select(receipt => receipt.Token).ToList())
                {
                    acceptedTokens.Remove(oldToken);
                }

                ActiveTradesStore store = new ActiveTradesStore
                {
                    ActiveTrades = { activeTrades.Values.Select(Trade.ToTradeStore) },
                    CompletedTrades = { completedTrades.Values.Select(CompletedTrade.ToStore) },
                    AcceptedTokens = { acceptedTokens.Values },
                    AckingUuids = { ackingUuids }
                };

                // Write the store without ever leaving a half-written file, since it holds items on offer (see SafeFile)
                SafeFile.WriteAllBytes(path, store.ToByteArray());

                RaiseLogEntry(new LogEventArgs(string.Format("Saved {0} active trade{1} and {2} trade{3} pending notification", activeTrades.Count, activeTrades.Count != 1 ? "s" : "", completedTrades.Count, completedTrades.Count != 1 ? "s" : "")));
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Loads the active trades from the given file, reinitialising the active trades dictionary if it doesn't exist.
        /// </summary>
        /// <param name="path">Active trade store path</param>
        public void Load(string path)
        {
            lock (tradeDataLock)
            {
                // Create a new store if there's no copy of it at all. A damaged one throws instead, so the items in it
                // never get replaced by an empty store (see SafeFile)
                if (!SafeFile.TryRead(path, ActiveTradesStore.Parser.ParseFrom, out ActiveTradesStore store, out string usedPath))
                {
                    RaiseLogEntry(new LogEventArgs("No trades database, generating a new one"));

                    // Initialise new active and completed trade dictionaries
                    activeTrades = new Dictionary<string, Trade>();
                    completedTrades = new Dictionary<string, CompletedTrade>();
                    acceptedTokens = new Dictionary<string, AcceptedToken>();
                    ackingUuids = new HashSet<string>();

                    // Save the new dictionaries to disk
                    Save(path);

                    // Stop here
                    return;
                }

                if (usedPath != path) RaiseLogEntry(new LogEventArgs(string.Format("The trades database was damaged or missing, so loaded {0} instead", usedPath), LogLevel.WARNING));

                // Set the active and completed trade dictionaries to the data that was just loaded
                activeTrades = store.ActiveTrades.Select(Trade.FromTradeStore).ToDictionary(item => item.TradeId, item => item);
                completedTrades = store.CompletedTrades.Select(CompletedTrade.FromStore).ToDictionary(item => item.Trade.TradeId, item => item);
                acceptedTokens = store.AcceptedTokens.ToDictionary(receipt => receipt.Token);
                ackingUuids = new HashSet<string>(store.AckingUuids);

                RaiseLogEntry(new LogEventArgs(string.Format("Loaded {0} active trade{1} and {2} trade{3} pending notification", activeTrades.Count, activeTrades.Count != 1 ? "s" : "", completedTrades.Count, completedTrades.Count != 1 ? "s" : "")));
            }
        }

        /// <summary>
        /// Handles incoming packets from <see cref="NetCommon"/>.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerTrading).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "CreateTradePacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a CreateTradePacket from {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));
                    createTradePacketHandler(connectionId, message.Unpack<CreateTradePacket>());
                    break;
                case "UpdateTradeItemsPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an UpdateTradeItemsPacket from {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));
                    updateTradeItemsPacketHandler(connectionId, message.Unpack<UpdateTradeItemsPacket>());
                    break;
                case "UpdateTradeStatusPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an UpdateTradeStatusPacket from {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));
                    updateTradeStatusPacketHandler(connectionId, message.Unpack<UpdateTradeStatusPacket>());
                    break;
                case "CheckTokensPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a CheckTokensPacket from {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));
                    checkTokensPacketHandler(connectionId, message.Unpack<CheckTokensPacket>());
                    break;
                case "AckDeliveriesPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an AckDeliveriesPacket from {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));
                    ackDeliveriesPacketHandler(connectionId, message.Unpack<AckDeliveriesPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        private void loginEventHandler(object sender, ServerLoginEventArgs args)
        {
            // Send the user a list of their active trades
            lock (tradeDataLock)
            {
                // Get all trades involving the user
                List<Trade> trades = activeTrades.Values.Where(trade => trade.PartyUuids.Contains(args.Uuid)).ToList();

                // Make sure there are trades to send before sending any
                if (trades.Count != 0)
                {
                    // Send the user a sync packet with each trade
                    sendSyncTradesPacket(args.ConnectionId, trades, args.Uuid);
                }

                // Notify the user of trades they were not notified of earlier (or never confirmed). ToList() takes a copy,
                // since notifyCompleted can remove finished ones from the dictionary
                foreach (CompletedTrade completedTrade in completedTrades.Values.Where(trade => trade.PendingNotification.Contains(args.Uuid)).ToList())
                {
                    notifyCompleted(completedTrade, args.Uuid, args.ConnectionId);
                }
            }
        }

        /// <summary>
        /// Handles incoming <see cref="CreateTradePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="CreateTradePacket"/></param>
        private void createTradePacketHandler(string connectionId, CreateTradePacket packet)
        {
            // Make sure the session is valid
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId))
            {
                // Fail trade creation attempt due to bad session
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.SessionId, "Cannot create a trade because your session is not valid. Try reconnecting.");

                // Stop here
                return;
            }

            // Make sure the user is valid
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                // Fail trade creation attempt due to bad UUID
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.Uuid, "Cannot create a trade because your login is not valid. Try reconnecting.");

                // Stop here
                return;
            }

            // Make sure the other party exists
            if (!userManager.TryGetLoggedIn(packet.OtherPartyUuid, out bool otherPartyLoggedIn))
            {
                // Fail the trade creation request because the other party does not exist
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.OtherPartyDoesNotExist, "Cannot create a trade because the other party does not exist.");

                // Stop here
                return;
            }

            // On a companion, players from different main servers can't see each other, so they can't trade either
            if (!userManager.SameHomeServer(packet.Uuid, packet.OtherPartyUuid))
            {
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.OtherPartyDoesNotExist, "Cannot create a trade because the other party is on a different server.");
                return;
            }

            // Make sure the other party is logged in
            // TODO: Offline trade offers
            if (!otherPartyLoggedIn)
            {
                // Fail the trade creation request because the other party is not logged in
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.OtherPartyOffline, "Cannot create a trade because the other party is not logged in.");

                // Stop here
                return;
            }

            // Make sure the other party is accepting trades
            if (!userManager.TryGetAcceptingTrades(packet.OtherPartyUuid, out bool otherPartyAcceptingTrades) || !otherPartyAcceptingTrades)
            {
                // Fail the trade creation request because the other party is not accepting trades
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.NotAcceptingTrades, "Cannot create a trade because the other party is not accepting trades.");

                // Stop here
                return;
            }

            lock (tradeDataLock)
            {
                // Check if both parties are already in another active trade
                bool alreadyTrading = activeTrades.Values.Any(t => t.PartyUuids.Contains(packet.Uuid) && t.PartyUuids.Contains(packet.OtherPartyUuid));
                if (alreadyTrading)
                {
                    // Fail the trade creation request because both parties are already trading with each other
                    sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.AlreadyTrading, "Cannot create a trade because you are already trading with the other party.");

                    // Stop here
                    return;
                }

                // Get the other party's connection ID
                if (!userManager.TryGetConnection(packet.OtherPartyUuid, out string otherPartyConnectionId))
                {
                    // Fail the trade creation request because the other party's connection ID cannot be retrieved
                    sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.InternalServerError, "Cannot create a trade because the server could not find the other party's connection.");

                    // Stop here
                    return;
                }

                // Refuse users opening trades too quickly. Checked last so refused attempts above don't use up the
                // allowance
                if (!tradeRateLimiter.TryAct(packet.Uuid))
                {
                    RaiseLogEntry(new LogEventArgs(string.Format("Rate limited a trade from {0}", packet.Uuid.Highlight(HighlightType.UUID)), LogLevel.DEBUG));
                    sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.RateLimited, "You're opening trades too quickly. Wait a little and try again.");

                    // Stop here
                    return;
                }

                // Passed all tests, create a new trade
                Trade trade = new Trade(new[] {packet.Uuid, packet.OtherPartyUuid});
                activeTrades.Add(trade.TradeId, trade);

                // Send both parties a successful trade creation packet
                sendSuccessfulCreateTradeResponsePacket(connectionId, trade.TradeId, packet.OtherPartyUuid); // Sender
                sendSuccessfulCreateTradeResponsePacket(otherPartyConnectionId, trade.TradeId, packet.Uuid); // Other party

                RaiseLogEntry(new LogEventArgs(string.Format("Created trade {0} between {1} and {2}", trade.TradeId.Highlight(HighlightType.TradeID), packet.Uuid.Highlight(HighlightType.UUID), packet.OtherPartyUuid.Highlight(HighlightType.UUID)), LogLevel.DEBUG));
            }
        }

        /// <summary>
        /// Sends a successful <see cref="CreateTradeResponsePacket"/> with the given trade ID and UUID of the other party.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        private void sendSuccessfulCreateTradeResponsePacket(string connectionId, string tradeId, string otherPartyUuid)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful CreateTradeResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Create and pack a response
            CreateTradeResponsePacket packet = new CreateTradeResponsePacket
            {
                Success = true,
                TradeId = tradeId,
                OtherPartyUuid = otherPartyUuid
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send CreateTradeResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a failed <see cref="CreateTradeResponsePacket"/> with the given failure reason and message.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="failureMessage">Failure message</param>
        private void sendFailedCreateTradeResponsePacket(string connectionId, TradeFailureReason failureReason, string failureMessage)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed CreateTradeResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Create and pack a response
            CreateTradeResponsePacket packet = new CreateTradeResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send CreateTradeResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Handles incoming <see cref="UpdateTradeStatusPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="UpdateTradeStatusPacket"/></param>
        private void updateTradeStatusPacketHandler(string connectionId, UpdateTradeStatusPacket packet)
        {
            // Ignore packets from non-authenticated and non-logged in users
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (tradeDataLock)
            {
                // Make sure the trade exists, returning on failure
                if (!activeTrades.ContainsKey(packet.TradeId)) return;

                Trade trade = activeTrades[packet.TradeId];

                // Try to get the other party's UUID, returning on failure
                if (!trade.TryGetOtherParty(packet.Uuid, out string otherPartyUuid)) return;

                // Check if the trade is being cancelled
                if (packet.Cancelled)
                {
                    // Move the trade from the active trades dictionary to the completed trades dictionary
                    completedTrades.Add(trade.TradeId, new CompletedTrade(trade, new []{packet.Uuid, otherPartyUuid}, true));
                    CompletedTrade completedTrade = completedTrades[trade.TradeId];
                    activeTrades.Remove(trade.TradeId);
                    trade = completedTrade.Trade;

                    // Items are going back to both sides, so get it on disk soon
                    SaveRequests.Request();

                    // Return the sender's items to them, and the other party's to them if they're online (otherwise
                    // when they next log in)
                    notifyCompleted(completedTrade, packet.Uuid, connectionId);
                    if (userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) && otherPartyLoggedIn &&
                        userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId))
                    {
                        notifyCompleted(completedTrade, otherPartyUuid, otherPartyConnectionId);
                    }
                }
                else
                {
                    // Check if the sender has just accepted
                    if (packet.Accepted && !trade.AcceptedParties.Contains(packet.Uuid))
                    {
                        // Add the sender's UUID to the accepted list
                        trade.AcceptedParties.Add(packet.Uuid);
                    }
                    // Check if the sender has just un-accepted
                    else if (!packet.Accepted && trade.AcceptedParties.Contains(packet.Uuid))
                    {
                        // Remove the sender's UUID from the accepted list
                        trade.AcceptedParties.Remove(packet.Uuid);
                    }
                    else
                    {
                        // Nothing has changed, stop here
                        return;
                    }

                    // Check if all parties have accepted and the trade can be completed successfully
                    if (trade.AcceptedParties.Count == trade.PartyUuids.Length)
                    {
                        // Move the trade from the active trades dictionary to the completed trades dictionary
                        completedTrades.Add(trade.TradeId, new CompletedTrade(trade, new []{packet.Uuid, otherPartyUuid}, false));
                        CompletedTrade completedTrade = completedTrades[trade.TradeId];
                        activeTrades.Remove(trade.TradeId);
                        trade = completedTrade.Trade;

                        // Items are changing hands, so get it on disk soon
                        SaveRequests.Request();

                        // Give each side the other's items: the sender now, the other party now if they're online
                        // (otherwise when they next log in)
                        notifyCompleted(completedTrade, packet.Uuid, connectionId);
                        if (userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) && otherPartyLoggedIn &&
                            userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId))
                        {
                            notifyCompleted(completedTrade, otherPartyUuid, otherPartyConnectionId);
                        }
                    }
                    else
                    {
                        // Ok so this next block here might look a little confusing.
                        // Because these methods are all 'Try...' methods, we need to consider their failure despite that
                        // only the first one - trying to get the other party's UUID - should fail.
                        // The other party's UUID and accepted state are necessary to respond to the sender, so that is done
                        // early.
                        // The remaining checks are for the other party and are just checking login and getting their
                        // connection ID.

                        // Try to get the other party's accepted state, returning on failure
                        if (!trade.TryGetAccepted(otherPartyUuid, out bool otherPartyAccepted)) return;

                        // Send an update to the sender
                        sendUpdateTradeStatusPacket(connectionId, trade.TradeId, packet.Accepted, otherPartyAccepted);

                        // Check if the other party is logged in
                        if (!userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) || !otherPartyLoggedIn) return;

                        // Try to get the other party's connection ID
                        if (!userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId)) return;

                        // Send an update to the other party
                        sendUpdateTradeStatusPacket(otherPartyConnectionId, trade.TradeId, otherPartyAccepted, packet.Accepted);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a <see cref="CompleteTradePacket"/> to the given connection ID.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="cancelled">Whether the trade was cancelled</param>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        /// <param name="items">Collection of items to send</param>
        /// <param name="returned">Whether these are things the other player's game couldn't use, coming back</param>
        /// <returns>Whether it was sent</returns>
        private bool sendCompleteTradePacket(string connectionId, string tradeId, bool cancelled, string otherPartyUuid, IEnumerable<ProtoThing> items, bool returned = false)
        {
            // Create and pack a response
            CompleteTradePacket packet = new CompleteTradePacket
            {
                TradeId = tradeId,
                Success = !cancelled,
                OtherPartyUuid = otherPartyUuid,
                Items = {items},
                Returned = returned
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send CompleteTradePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sends one party the result of a finished trade: their own items back if it was cancelled, the other party's
        /// if it went through. They're checked off once it's sent, or for a client that confirms results, once it
        /// confirms. The trade is forgotten when nobody is left waiting. Must be called while holding
        /// <see cref="tradeDataLock"/>.
        /// </summary>
        /// <param name="completedTrade">The finished trade</param>
        /// <param name="uuid">Party to tell</param>
        /// <param name="connectionId">Their connection</param>
        private void notifyCompleted(CompletedTrade completedTrade, string uuid, string connectionId)
        {
            Trade trade = completedTrade.Trade;
            if (!trade.TryGetOtherParty(uuid, out string otherPartyUuid)) return;

            // Things being sent back sit on the receiver's side, like a cancelled trade's
            string whoseItems = completedTrade.Cancelled ? uuid : otherPartyUuid;
            if (!trade.TryGetItemsOnOffer(whoseItems, out ProtoThing[] items)) return;

            bool sent = sendCompleteTradePacket(connectionId, trade.TradeId, completedTrade.Cancelled, otherPartyUuid, items, completedTrade.Returned);
            if (sent && !ackingUuids.Contains(uuid)) completedTrade.PendingNotification.Remove(uuid);
            if (completedTrade.PendingNotification.Count == 0) completedTrades.Remove(trade.TradeId);
        }

        /// <summary>
        /// Handles a client confirming trade results have arrived in its colony. The first one (sent right after logging
        /// in, even if empty) marks the player as confirming, so their results are kept until confirmed from then on.
        /// Things from the other player that their game couldn't use are sent back to that player.
        /// </summary>
        private void ackDeliveriesPacketHandler(string connectionId, AckDeliveriesPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (tradeDataLock)
            {
                if (ackingUuids.Add(packet.Uuid)) SaveRequests.Request();

                foreach (DeliveryAck ack in packet.Acks)
                {
                    if (!completedTrades.TryGetValue(ack.DeliveryId, out CompletedTrade completedTrade) || !completedTrade.PendingNotification.Remove(packet.Uuid)) continue;
                    if (completedTrade.PendingNotification.Count == 0) completedTrades.Remove(ack.DeliveryId);
                    SaveRequests.Request();

                    // Only things that came from the other player can go back to them; things already coming back
                    // never go back again
                    Trade trade = completedTrade.Trade;
                    if (completedTrade.Cancelled || completedTrade.Returned || ack.UnusableItems.Count == 0) continue;
                    if (!trade.TryGetOtherParty(packet.Uuid, out string otherPartyUuid) || !trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] received)) continue;

                    List<ProtoThing> unusable = ack.UnusableItems.Where(i => i >= 0 && i < received.Length).Distinct().Select(i => received[i]).ToList();
                    if (unusable.Count == 0) continue;

                    RaiseLogEntry(new LogEventArgs(string.Format("Sending {0} unusable thing(s) from trade {1} back to {2}", unusable.Count, trade.TradeId.Highlight(HighlightType.TradeID), otherPartyUuid.Highlight(HighlightType.UUID))));
                    Trade sendBack = new Trade(new[] { packet.Uuid, otherPartyUuid });
                    sendBack.TrySetItemsOnOffer(otherPartyUuid, unusable);
                    CompletedTrade returned = new CompletedTrade(sendBack, new[] { otherPartyUuid }, true) { Returned = true };
                    completedTrades.Add(sendBack.TradeId, returned);
                    if (userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId))
                    {
                        notifyCompleted(returned, otherPartyUuid, otherPartyConnectionId);
                    }
                }
            }
        }

        /// <summary>
        /// Sends an <see cref="UpdateTradeStatusPacket"/> with the given trade ID and both accepted states to the given connection.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="accepted">Whether this party has accepted</param>
        /// <param name="otherPartyAccepted">Whether the other party has accepted</param>
        private void sendUpdateTradeStatusPacket(string connectionId, string tradeId, bool accepted, bool otherPartyAccepted)
        {
            // Create and pack a response
            UpdateTradeStatusPacket packet = new UpdateTradeStatusPacket
            {
                TradeId = tradeId,
                Accepted = accepted,
                OtherPartyAccepted = otherPartyAccepted
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send UpdateTradeStatusPacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Handles incoming <see cref="UpdateTradeItemsPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="UpdateTradeItemsPacket"/></param>
        private void updateTradeItemsPacketHandler(string connectionId, UpdateTradeItemsPacket packet)
        {
            // Ignore packets from non-authenticated and non-logged in users
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (tradeDataLock)
            {
                // Make sure trade exists
                if (!activeTrades.ContainsKey(packet.TradeId))
                {
                    // Send a failed response
                    sendFailedUpdateTradeItemsResponsePacket(connectionId, packet.TradeId, packet.Token, new ProtoThing[0], TradeFailureReason.TradeDoesNotExist, "The trade does not exist.");

                    // Stop here
                    return;
                };

                Trade trade = activeTrades[packet.TradeId];

                bool success;
                if (packet.Items.Count == 0)
                {
                    // Clear the party's items
                    success = trade.TryClearItemsOnOffer(packet.Uuid);
                }
                else
                {
                    // Update the party's items
                    success = trade.TrySetItemsOnOffer(packet.Uuid, packet.Items);
                }

                // Only send an update on a successful change
                if (success)
                {
                    // Keep a receipt, in the same locked block that changed the offer, so a client whose connection
                    // drops before our answer arrives can still find out we took its items
                    if (!string.IsNullOrEmpty(packet.Token))
                    {
                        acceptedTokens[packet.Token] = new AcceptedToken { Token = packet.Token, Uuid = packet.Uuid, Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                    }

                    // The offer changed hands, so get it on disk soon
                    SaveRequests.Request();

                    // Try to get the other party's UUID, returning on failure
                    if (!trade.TryGetOtherParty(packet.Uuid, out string otherPartyUuid)) return;

                    // Try to get the other party's items on offer, returning on failure
                    if (!trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] otherPartyItems)) return;

                    // Send a successful response to the sender
                    sendSuccessfulUpdateTradeItemsResponsePacket(connectionId, trade.TradeId, packet.Token, packet.Items);

                    // Check if the other party is logged in
                    if (!userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) || !otherPartyLoggedIn) return;

                    // Try to get the other party's connection ID
                    if (!userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId)) return;

                    // Send an update to the other party
                    sendUpdateTradeItemsPacket(otherPartyConnectionId, trade.TradeId, otherPartyItems, packet.Items);
                }
                else
                {
                    // Try get the sender's items
                    if (trade.TryGetItemsOnOffer(packet.Uuid, out ProtoThing[] items))
                    {
                        // Send a failed response with their current items on offer
                        sendFailedUpdateTradeItemsResponsePacket(connectionId, packet.TradeId, packet.Token, items, TradeFailureReason.InternalServerError, "An error occurred on the server. Please try again.");
                    }
                    else
                    {
                        // Send a failed response with no items (gotta be having a bad day to get this far)
                        sendFailedUpdateTradeItemsResponsePacket(connectionId, packet.TradeId, packet.Token, new ProtoThing[0], TradeFailureReason.InternalServerError, "An error occurred on the server. Please try again.");
                    }
                }
            }
        }

        /// <summary>
        /// Answers a client asking which of its held trade tokens we accepted (see Packets/EscrowPackets.proto).
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="CheckTokensPacket"/></param>
        private void checkTokensPacketHandler(string connectionId, CheckTokensPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            TokenResultsPacket results = new TokenResultsPacket();
            results.Checked.AddRange(packet.Tokens);
            lock (tradeDataLock)
            {
                // Only answer for receipts that belong to whoever is asking
                results.Accepted.AddRange(packet.Tokens.Where(token => acceptedTokens.TryGetValue(token, out AcceptedToken receipt) && receipt.Uuid == packet.Uuid));
            }

            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(results).ToByteArray());
        }

        /// <summary>
        /// Sends an <see cref="UpdateTradeItemsPacket"/> with the given items of both parties for the given trade.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="items">Party's items</param>
        /// <param name="otherPartyItems">Other party's items</param>
        private void sendUpdateTradeItemsPacket(string connectionId, string tradeId, IEnumerable<ProtoThing> items, IEnumerable<ProtoThing> otherPartyItems)
        {
            // Create and pack a response
            UpdateTradeItemsPacket packet = new UpdateTradeItemsPacket
            {
                TradeId = tradeId,
                Items = {items},
                OtherPartyItems = {otherPartyItems}
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send UpdateTradeItemsPacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a successful <see cref="UpdateTradeItemsResponsePacket"/> with the given items of the sender for the given trade and token.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="token">Token sent with the request</param>
        /// <param name="items">Sender's items</param>
        private void sendSuccessfulUpdateTradeItemsResponsePacket(string connectionId, string tradeId, string token, IEnumerable<ProtoThing> items)
        {
            // Create and pack a response
            UpdateTradeItemsResponsePacket packet = new UpdateTradeItemsResponsePacket
            {
                TradeId = tradeId,
                Token = token,
                Success = true,
                Items = {items}
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send UpdateTradeItemsResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a failed <see cref="UpdateTradeItemsResponsePacket"/> with the given items of the sender for the given trade and token.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="token">Token sent with the request</param>
        /// <param name="items">Sender's items</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="failureMessage">Failure message</param>
        private void sendFailedUpdateTradeItemsResponsePacket(string connectionId, string tradeId, string token, IEnumerable<ProtoThing> items, TradeFailureReason failureReason, string failureMessage)
        {
            // Create and pack a response
            UpdateTradeItemsResponsePacket packet = new UpdateTradeItemsResponsePacket
            {
                TradeId = tradeId,
                Token = token,
                Success = false,
                Items = {items},
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send UpdateTradeItemsResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a <see cref="SyncTradesPacket"/> containing the given trades from the given party's perspective.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="trades">Collection of trades toi send</param>
        /// <param name="partyUuid">Party's UUID</param>
        private void sendSyncTradesPacket(string connectionId, IEnumerable<Trade> trades, string partyUuid)
        {
            // Create our SyncTradesPacket
            SyncTradesPacket packet = new SyncTradesPacket();

            // Proto-ify each trade and add them to the packet
            foreach (Trade trade in trades)
            {
                // Try get the other party's UUID, continuing on failure
                if (!trade.TryGetOtherParty(partyUuid, out string otherPartyUuid)) continue;

                // Try get each party's items on offer, continuing on failure
                if (!trade.TryGetItemsOnOffer(partyUuid, out ProtoThing[] partyItems) ||
                    !trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] otherPartyItems))
                {
                    continue;
                }

                // Try get each party's accepted state, continuing on failure
                if (!trade.TryGetAccepted(partyUuid, out bool partyAccepted) ||
                    !trade.TryGetAccepted(otherPartyUuid, out bool otherPartyAccepted))
                {
                    continue;
                }

                // Create a proto-ified trade with the values we just got
                TradeProto tradeProto = new TradeProto
                {
                    TradeId = trade.TradeId,
                    OtherPartyUuid = otherPartyUuid,
                    Items = {partyItems},
                    OtherPartyItems = {otherPartyItems},
                    Accepted = partyAccepted,
                    OtherPartyAccepted = otherPartyAccepted
                };

                // Add it to the packet
                packet.Trades.Add(tradeProto);
            }

            // Pack the packet
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send SyncTradesPacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }
    }
}