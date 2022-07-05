using System;
using System.Collections.Generic;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Trading
{
    public class ClientTrading : Trading
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Raised when a trade is created successfully.
        /// </summary>
        public event EventHandler<CreateTradeEventArgs> OnTradeCreationSuccess;
        /// <summary>
        /// Raised when a trade fails to create.
        /// </summary>
        public event EventHandler<CreateTradeEventArgs> OnTradeCreationFailure;

        /// <summary>
        /// Raised when a trade completes successfully.
        /// </summary>
        public event EventHandler<CompleteTradeEventArgs> OnTradeCompleted;
        /// <summary>
        /// Raised when a trade is cancelled.
        /// </summary>
        public event EventHandler<CompleteTradeEventArgs> OnTradeCancelled;

        /// <summary>
        /// Raised when a trade is updated successfully.
        /// </summary>
        public event EventHandler<TradeUpdateEventArgs> OnTradeUpdateSuccess;
        /// <summary>
        /// Raised when a trade fails to update.
        /// </summary>
        public event EventHandler<TradeUpdateEventArgs> OnTradeUpdateFailure;

        /// <summary>
        /// Raised when trades are synced from the server.
        /// </summary>
        public event EventHandler<TradesSyncedEventArgs> OnTradesSynced;

        /// <summary>
        /// <see cref="NetClient"/> instance to bind events and send data through.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <see cref="ClientAuthenticator"/> instance to get a session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <see cref="ClientUserManager"/> instance to get a UUID from.
        /// </summary>
        private ClientUserManager userManager;

        /// <summary>
        /// Collection of active trades with other users organised by trade ID.
        /// </summary>
        private Dictionary<string, Trade> activeTrades;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <see cref="activeTrades"/>.
        /// </summary>
        private object activeTradesLock = new object();

        public ClientTrading(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;

            this.activeTrades = new Dictionary<string, Trade>();

            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netClient.OnDisconnect += disconnectHandler;
        }

        private void disconnectHandler(object sender, EventArgs e)
        {
            // Clear all active trades on disconnect
            lock (activeTradesLock)
            {
                activeTrades.Clear();
            }
        }

        /// <summary>
        /// Attempts to create a trade with another party.
        /// </summary>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public void CreateTrade(string otherPartyUuid)
        {
            if (string.IsNullOrEmpty(otherPartyUuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(otherPartyUuid));

            // Do nothing if not online
            if (!(netClient.Connected || authenticator.Authenticated || userManager.LoggedIn)) return;

            // Create and pack a CreateTradePacket
            CreateTradePacket packet = new CreateTradePacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                OtherPartyUuid = otherPartyUuid
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Cancels a trade with the given ID.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <exception cref="ArgumentException">Trade ID cannot be null or empty</exception>
        public void CancelTrade(string tradeId)
        {
            if (string.IsNullOrEmpty(tradeId)) throw new ArgumentException("Trade ID cannot be null or empty.", nameof(tradeId));

            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId)) return;
            }

            // Do nothing if not online
            if (!(netClient.Connected || authenticator.Authenticated || userManager.LoggedIn)) return;

            // Create and pack a CompleteTradePacket
            UpdateTradeStatusPacket packet = new UpdateTradeStatusPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                TradeId = tradeId,
                Cancelled = true
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Returns a collection of all active trade IDs.
        /// </summary>
        /// <returns>Collection of all active trade IDs</returns>
        public string[] GetTradeIds()
        {
            lock (activeTradesLock)
            {
                return activeTrades.Values.Select(trade => trade.TradeId).ToArray();
            }
        }

        /// <summary>
        /// Returns a collection of all active trade IDs except those with the given parties.
        /// </summary>
        /// <param name="otherPartyUuids">Other parties' UUIDs to ignore trades from</param>
        /// <returns>Collection of all active trade IDs except those with the given parties</returns>
        public string[] GetTradeIdsExceptWith(IEnumerable<string> otherPartyUuids)
        {
            lock (activeTradesLock)
            {
                return activeTrades.Values
                                   .Where(trade => trade.PartyUuids.All(uuid => !otherPartyUuids.Contains(uuid))) // Ignore the other parties
                                   .Select(trade => trade.TradeId) // Extract the trade ID
                                   .ToArray();
            }
        }

        /// <summary>
        /// Returns a collection of of all active trades.
        /// </summary>
        /// <returns>Collection of all active trades</returns>
        public ImmutableTrade[] GetTrades()
        {
            List<ImmutableTrade> trades = new List<ImmutableTrade>();
            lock (activeTradesLock)
            {
                foreach (string tradeId in activeTrades.Keys)
                {
                    ImmutableTrade trade;
                    try
                    {
                        trade = getTrade(tradeId);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    trades.Add(trade);
                }
            }

            return trades.ToArray();
        }

        /// <summary>
        /// Returns a collection of all active trades except those with the given parties.
        /// </summary>
        /// <param name="otherPartyUuids">Other parties' UUIDs to ignore trades from</param>
        /// <returns>Collection of all active trades except those with the given parties</returns>
        public ImmutableTrade[] GetTradesExceptWith(IEnumerable<string> otherPartyUuids)
        {
            List<ImmutableTrade> trades = new List<ImmutableTrade>();
            lock (activeTradesLock)
            {
                foreach (string tradeId in activeTrades.Keys.Except(otherPartyUuids))
                {
                    ImmutableTrade trade;
                    try
                    {
                        trade = getTrade(tradeId);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    trades.Add(trade);
                }
            }

            return trades.ToArray();
        }

        /// <summary>
        /// Tries to get the other party's UUID from the given trade.
        /// Returns whether the UUID was retrieved successfully.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherPartyUuid">Output UUID</param>
        /// <returns>UUID was retrieved successfully</returns>
        public bool TryGetOtherPartyUuid(string tradeId, out string otherPartyUuid)
        {
            // Assign other party UUID to something arbitrary
            otherPartyUuid = null;

            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId)) return false;

                try
                {
                    // Try to get a single UUID that isn't ours from the party UUIDs array
                    otherPartyUuid = activeTrades[tradeId].PartyUuids.Single(uuid => uuid != userManager.Uuid);
                }
                catch (InvalidOperationException)
                {
                    // Couldn't find a single UUID that wasn't ours
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the accepted state of the other party from the given trade.
        /// Returns whether the accepted state was retrieved successfully.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherPartyAccepted">Whether the other party has accepted</param>
        /// <returns>Whether the accepted state was returned successfully</returns>
        public bool TryGetOtherPartyAccepted(string tradeId, out bool otherPartyAccepted)
        {
            // Set other party accepted to something arbitrary
            otherPartyAccepted = false;

            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId)) return false;

                // Try to get the other party's UUID
                if (!TryGetOtherPartyUuid(tradeId, out string otherPartyUuid)) return false;

                // Set the accepted state to whether they are in the accepted parties list
                otherPartyAccepted = activeTrades[tradeId].AcceptedParties.Contains(otherPartyUuid);
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the accepted state of the given party from the given trade.
        /// Returns whether the accepted state was retrieved successfully.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="partyUuid">Party's UUID</param>
        /// <param name="accepted">Accepted state output</param>
        /// <returns>Whether the accepted state was returned successfully</returns>
        public bool TryGetPartyAccepted(string tradeId, string partyUuid, out bool accepted)
        {
            // Set other party accepted to something arbitrary
            accepted = false;

            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId)) return false;

                Trade trade = activeTrades[tradeId];

                // Try to get the party's accepted state, returning false on failure
                if (!trade.TryGetAccepted(partyUuid, out accepted)) return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the items on offer in the given trade for a given party.
        /// Returns whether the operation completed successfully.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="partyUuid">Party's UUID</param>
        /// <param name="items">Items output</param>
        /// <returns>Whether the operation completed successfully</returns>
        public bool TryGetItemsOnOffer(string tradeId, string partyUuid, out IEnumerable<ProtoThing> items)
        {
            // Initialise items to something arbitrary
            items = null;

            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId)) return false;

                Trade trade = activeTrades[tradeId];

                // Make sure the party is a part of this trade
                if (!trade.PartyUuids.Contains(partyUuid)) return false;

                // Check if the party has any items on offer
                if (trade.ItemsOnOffer.ContainsKey(partyUuid))
                {
                    // Set items as those on offer
                    items = trade.ItemsOnOffer[partyUuid];
                }
                else
                {
                    // No items on offer, return an empty array
                    items = new ProtoThing[0];
                }
            }

            return true;
        }

        /// <summary>
        /// Tries to get the trade details for the given trade ID.
        /// Returns whether the trade was retrieved successfully.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="immutableTrade">Trade details output</param>
        /// <returns>Whether the trade was received successfully</returns>
        public bool TryGetTrade(string tradeId, out ImmutableTrade immutableTrade)
        {
            try
            {
                immutableTrade = getTrade(tradeId);
            }
            catch (Exception e)
            {
                immutableTrade = new ImmutableTrade();
                RaiseLogEntry(new LogEventArgs($"Failed to get/convert trade {tradeId}: {e.Message}", LogLevel.ERROR));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sends an item update for the given trade with the given items to the server.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="items">Items on offer</param>
        /// <param name="token">Unique request token</param>
        public void UpdateItems(string tradeId, IEnumerable<ProtoThing> items, string token = "")
        {
            // Do nothing if not online
            if (!(netClient.Connected && authenticator.Authenticated && userManager.LoggedIn)) return;

            // Create and pack an UpdateTradeItems packet
            UpdateTradeItemsPacket packet = new UpdateTradeItemsPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                TradeId = tradeId,
                Items = {items},
                Token = token
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Sends a status update for the given trade to the server.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="accepted">Accepted state</param>
        /// <param name="cancelled">Cancel the trade</param>
        public void UpdateStatus(string tradeId, bool? accepted = null, bool? cancelled = null)
        {
            // Do nothing if not online
            if (!(netClient.Connected && authenticator.Authenticated && userManager.LoggedIn)) return;

            // Create and pack an UpdateTradeStatus packet
            UpdateTradeStatusPacket packet = new UpdateTradeStatusPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                TradeId = tradeId,
                Accepted = accepted.HasValue ? accepted.Value : false,
                Cancelled = cancelled.HasValue ? cancelled.Value : false
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Gets a trade's details.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <returns>Trade details</returns>
        /// <exception cref="ArgumentException">Trade does not exist</exception>
        /// <exception cref="InvalidOperationException">Trade does not have a valid other party</exception>
        private ImmutableTrade getTrade(string tradeId)
        {
            lock (activeTradesLock)
            {
                // Make sure the trade exists
                if (!activeTrades.ContainsKey(tradeId))
                {
                    throw new ArgumentException("Trade does not exist", nameof(tradeId));
                }

                Trade trade = activeTrades[tradeId];

                // Ensure we can get the other party's UUID
                if (!trade.TryGetOtherParty(userManager.Uuid, out string otherPartyUuid))
                {
                    throw new InvalidOperationException("Trade does not have a valid other party");
                }

                // Try to pull out the other party from userManager, creating a barebones one if that fails
                if (!userManager.TryGetUser(otherPartyUuid, out ImmutableUser otherParty))
                {
                    otherParty = new ImmutableUser(otherPartyUuid);
                }

                // Build an immutable copy of the trade and return successfully
                return new ImmutableTrade(
                    tradeId: tradeId,
                    otherParty: otherParty,
                    ourItemsOnOffer: trade.ItemsOnOffer[userManager.Uuid],
                    otherPartyItemsOnOffer: trade.ItemsOnOffer[otherPartyUuid],
                    accepted: trade.AcceptedParties.Contains(userManager.Uuid),
                    otherPartyAccepted: trade.AcceptedParties.Contains(otherPartyUuid)
                );
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
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientTrading).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "CreateTradeResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a CreateTradeResponsePacket", LogLevel.DEBUG));
                    createTradeResponsePacketHandler(connectionId, message.Unpack<CreateTradeResponsePacket>());
                    break;
                case "CompleteTradePacket":
                    RaiseLogEntry(new LogEventArgs("Got a CompleteTradePacket", LogLevel.DEBUG));
                    completeTradePacketHandler(connectionId, message.Unpack<CompleteTradePacket>());
                    break;
                case "UpdateTradeItemsPacket":
                    RaiseLogEntry(new LogEventArgs("Got an UpdateTradeItemsPacket", LogLevel.DEBUG));
                    updateTradeItemsPacketHandler(connectionId, message.Unpack<UpdateTradeItemsPacket>());
                    break;
                case "UpdateTradeItemsResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got an UpdateTradeItemsResponsePacket", LogLevel.DEBUG));
                    updateTradeItemsResponsePacketHandler(connectionId, message.Unpack<UpdateTradeItemsResponsePacket>());
                    break;
                case "UpdateTradeStatusPacket":
                    RaiseLogEntry(new LogEventArgs("Got an UpdateTradeStatusPacket", LogLevel.DEBUG));
                    updateTradeStatusPacketHandler(connectionId, message.Unpack<UpdateTradeStatusPacket>());
                    break;
                case "SyncTradesPacket":
                    RaiseLogEntry(new LogEventArgs("Got a SyncTradesPacket", LogLevel.DEBUG));
                    syncTradesPacketHandler(connectionId, message.Unpack<SyncTradesPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <see cref="CreateTradeResponsePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="CreateTradeResponsePacket"/></param>
        private void createTradeResponsePacketHandler(string connectionId, CreateTradeResponsePacket packet)
        {
            if (packet.Success)
            {
                lock (activeTradesLock)
                {
                    // Stop here if the trade already exists locally
                    if (activeTrades.ContainsKey(packet.TradeId)) return;

                    // Add a new trade with the ID contained in the packet
                    activeTrades.Add(packet.TradeId, new Trade(packet.TradeId, new[]{userManager.Uuid, packet.OtherPartyUuid}));
                }

                // Raise the successful trade creation event
                OnTradeCreationSuccess?.Invoke(this, new CreateTradeEventArgs(packet.TradeId, packet.OtherPartyUuid));
            }
            else
            {
                // Raise the failed trade creation event
                OnTradeCreationFailure?.Invoke(this, new CreateTradeEventArgs(packet.FailureReason, packet.FailureMessage));
            }
        }

        /// <summary>
        /// Handles incoming <see cref="CompleteTradePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="CompleteTradePacket"/></param>
        private void completeTradePacketHandler(string connectionId, CompleteTradePacket packet)
        {
            // Raise finalised trade events
            if (packet.Success)
            {
                OnTradeCompleted?.Invoke(this, new CompleteTradeEventArgs(packet.TradeId, true, packet.OtherPartyUuid, packet.Items));
            }
            else
            {
                OnTradeCancelled?.Invoke(this, new CompleteTradeEventArgs(packet.TradeId, false, packet.OtherPartyUuid, packet.Items));
            }

            lock (activeTradesLock)
            {
                // Remove the trade
                activeTrades.Remove(packet.TradeId);
            }
        }

        /// <summary>
        /// Handles incoming <see cref="UpdateTradeItemsPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="UpdateTradeItemsPacket"/></param>
        private void updateTradeItemsPacketHandler(string connectionId, UpdateTradeItemsPacket packet)
        {
            lock (activeTradesLock)
            {
                // Ignore packets from trades we don't have
                if (!activeTrades.ContainsKey(packet.TradeId)) return;

                Trade trade = activeTrades[packet.TradeId];

                // Set our items on offer
                trade.TrySetItemsOnOffer(userManager.Uuid, packet.Items);

                RaiseLogEntry(new LogEventArgs(string.Format("Got items {0} for us", packet.Items.Count), LogLevel.DEBUG));

                // Try get the other party's UUID
                if (trade.TryGetOtherParty(userManager.Uuid, out string otherPartyUuid))
                {
                    // Set the other party's items on offer
                    trade.TrySetItemsOnOffer(otherPartyUuid, packet.OtherPartyItems);

                    RaiseLogEntry(new LogEventArgs(string.Format("Got items {0} for {1}", packet.Items.Count, otherPartyUuid), LogLevel.DEBUG));
                }
            }

            // Raise the trade update event
            OnTradeUpdateSuccess?.Invoke(this, new TradeUpdateEventArgs(packet.TradeId));
        }

        /// <summary>
        /// Handles incoming <see cref="UpdateTradeItemsResponsePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="UpdateTradeItemsResponsePacket"/></param>
        private void updateTradeItemsResponsePacketHandler(string connectionId, UpdateTradeItemsResponsePacket packet)
        {
            lock (activeTradesLock)
            {
                // Ignore packets from trades we don't have
                if (!activeTrades.ContainsKey(packet.TradeId)) return;

                Trade trade = activeTrades[packet.TradeId];

                if (packet.Success)
                {
                    // Set our items on offer
                    trade.TrySetItemsOnOffer(userManager.Uuid, packet.Items);

                    RaiseLogEntry(new LogEventArgs(string.Format("Got items {0} for us", packet.Items.Count), LogLevel.DEBUG));

                    // Raise the successful trade update event
                    OnTradeUpdateSuccess?.Invoke(this, new TradeUpdateEventArgs(packet.TradeId, packet.Token));
                }
                else
                {
                    // Raise the failed trade update event
                    // Do this before removing the trade so its state is preserved for anything that needs handle it
                    OnTradeUpdateFailure?.Invoke(this, new TradeUpdateEventArgs(packet.TradeId, packet.FailureReason, packet.FailureMessage, packet.Token));

                    // Check the failure reason
                    switch (packet.FailureReason)
                    {
                        case TradeFailureReason.TradeDoesNotExist:
                            lock (activeTradesLock)
                            {
                                // Remove the trade since it doesn't exist
                                activeTrades.Remove(packet.TradeId);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles incoming <see cref="UpdateTradeStatusPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="UpdateTradeStatusPacket"/></param>
        private void updateTradeStatusPacketHandler(string connectionId, UpdateTradeStatusPacket packet)
        {
            lock (activeTradesLock)
            {
                // Ignore packets from trades we don't have
                if (!activeTrades.ContainsKey(packet.TradeId)) return;

                Trade trade = activeTrades[packet.TradeId];

                // Try set our accepted state
                trade.TrySetAccepted(userManager.Uuid, packet.Accepted);

                // Try get the other party's UUID
                if (trade.TryGetOtherParty(userManager.Uuid, out string otherPartyUuid))
                {
                    // Try set their accepted state
                    trade.TrySetAccepted(otherPartyUuid, packet.OtherPartyAccepted);
                }
            }

            // Raise the trade update event
            OnTradeUpdateSuccess?.Invoke(this, new TradeUpdateEventArgs(packet.TradeId));
        }

        /// <summary>
        /// Handles incoming <see cref="SyncTradesPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="SyncTradesPacket"/></param>
        private void syncTradesPacketHandler(string connectionId, SyncTradesPacket packet)
        {
            lock (activeTradesLock)
            {
                // Clear out all active trades
                activeTrades.Clear();

                // Construct new trades for each in the sync packet
                foreach (TradeProto tradeProto in packet.Trades)
                {
                    // Create a new trade between us and the other party
                    Trade trade = new Trade(tradeProto.TradeId, new[]{userManager.Uuid, tradeProto.OtherPartyUuid});

                    // Set items on offer
                    trade.TrySetItemsOnOffer(userManager.Uuid, tradeProto.Items);
                    trade.TrySetItemsOnOffer(tradeProto.OtherPartyUuid, tradeProto.OtherPartyItems);

                    // Set accepted states
                    trade.TrySetAccepted(userManager.Uuid, tradeProto.Accepted);
                    trade.TrySetAccepted(tradeProto.OtherPartyUuid, tradeProto.OtherPartyAccepted);

                    // Add the trade to the active trades collection
                    activeTrades.Add(trade.TradeId, trade);
                }
            }

            // Raise the trades synced event
            OnTradesSynced?.Invoke(this, new TradesSyncedEventArgs(packet.Trades.Select(trade => trade.TradeId)));
        }
    }
}