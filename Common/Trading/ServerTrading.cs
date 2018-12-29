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
    public class ServerTrading : Trading
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <c>NetServer</c> instance to bind events and send data through.
        /// </summary>
        private NetServer netServer;

        /// <summary>
        /// <c>ServerAuthenticator</c> instance to check session validity.
        /// </summary>
        private ServerAuthenticator authenticator;

        /// <summary>
        /// <c>ServerUserManager</c> instance to check login state and trade preferences through.
        /// </summary>
        private ServerUserManager userManager;

        /// <summary>
        /// Collection of active trades organised by trade ID.
        /// </summary>
        private Dictionary<string, Trade> activeTrades;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>activeTrades</c>.
        /// </summary>
        private object activeTradesLock = new object();

        public ServerTrading(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            
            this.activeTrades = new Dictionary<string, Trade>();
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
        }

        /// <summary>
        /// Handles incoming packets from <c>NetCommon</c>.
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
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a CreateTradePacket from {0}", connectionId), LogLevel.DEBUG));
                    createTradePacketHandler(connectionId, message.Unpack<CreateTradePacket>());
                    break;
                case "UpdateTradeItemsPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an UpdateTradeItemsPacket from {0}", connectionId), LogLevel.DEBUG));
                    updateTradeItemsPacketHandler(connectionId, message.Unpack<UpdateTradeItemsPacket>());
                    break;
                case "UpdateTradeStatusPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an UpdateTradeStatusPacket from {0}", connectionId), LogLevel.DEBUG));
                    updateTradeStatusPacketHandler(connectionId, message.Unpack<UpdateTradeStatusPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>CreateTradePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>CreateTradePacket</c></param>
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
            
            // Make sure the other party is logged in
            // TODO: Offline trade offers
            if (!otherPartyLoggedIn)
            {
                // Fail the trade creation request because the other party is not logged in
                sendFailedCreateTradeResponsePacket(connectionId, TradeFailureReason.OtherPartyOffline, "Cannot create a trade because the other party is not logged in.");
                
                // Stop here
                return; 
            }

            lock (activeTradesLock)
            {
                // Check if both parties are already in another active trade
                bool alreadyTrading = activeTrades.Values.Any(t => t.PartyUuids.Contains(packet.Uuid) || t.PartyUuids.Contains(packet.OtherPartyUuid));
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
                
                // Passed all tests, create a new trade
                Trade trade = new Trade(new[] {packet.Uuid, packet.OtherPartyUuid});
                activeTrades.Add(trade.TradeId, trade);
                
                // Send both parties a successful trade creation packet
                sendSuccessfulCreateTradeResponsePacket(connectionId, trade.TradeId, packet.OtherPartyUuid); // Sender
                sendSuccessfulCreateTradeResponsePacket(otherPartyConnectionId, trade.TradeId, packet.Uuid); // Other party
                
                RaiseLogEntry(new LogEventArgs(string.Format("Created trade {0} between {1} and {2}", trade.TradeId, packet.Uuid, packet.OtherPartyUuid), LogLevel.DEBUG));
            }
        }

        /// <summary>
        /// Sends a successful <c>CreateTradeResponsePacket</c> with the given trade ID and UUID of the other party.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        private void sendSuccessfulCreateTradeResponsePacket(string connectionId, string tradeId, string otherPartyUuid)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful CreateTradeResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Create and pack a response
            CreateTradeResponsePacket packet = new CreateTradeResponsePacket
            {
                Success = true,
                TradeId = tradeId,
                OtherPartyUuid = otherPartyUuid
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Sends a failed <c>CreateTradeResponsePacket</c> with the given failure reason and message.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="failureMessage">Failure message</param>
        private void sendFailedCreateTradeResponsePacket(string connectionId, TradeFailureReason failureReason, string failureMessage)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed CreateTradeResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Create and pack a response
            CreateTradeResponsePacket packet = new CreateTradeResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }
        
        /// <summary>
        /// Handles incoming <c>UpdateTradeStatusPacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>UpdateTradeStatusPacket</c></param>
        private void updateTradeStatusPacketHandler(string connectionId, UpdateTradeStatusPacket packet)
        {
            // Ignore packets from non-authenticated and non-logged in users
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (activeTradesLock)
            {
                Trade trade = activeTrades[packet.TradeId];
                
                // Try to get the other party's UUID, returning on failure
                if (!trade.TryGetOtherParty(packet.Uuid, out string otherPartyUuid)) return;
                
                // Check if the trade is being cancelled
                if (packet.Cancelled)
                {
                    // Try to get the sender's items on offer, returning on failure
                    if (!trade.TryGetItemsOnOffer(packet.Uuid, out ProtoThing[] items)) return;
                    
                    // Return the sender's items to them
                    sendCompleteTradePacket(connectionId, trade.TradeId, true, otherPartyUuid, items);
                    
                    // Try to get the other party's items on offer, returning on failure
                    if (!trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] otherPartyItems)) return;
                    
                    // Check if the other party is logged in
                    if (!userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) || !otherPartyLoggedIn) return;
                    
                    // Try to get the other party's connection ID
                    if (!userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId)) return;
                    
                    // Return the other party's items to them
                    sendCompleteTradePacket(otherPartyConnectionId, trade.TradeId, true, packet.Uuid, otherPartyItems);
                    
                    // Remove the trade
                    activeTrades.Remove(packet.TradeId);
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
                        // Try to get the sender's items on offer, returning on failure
                        if (!trade.TryGetItemsOnOffer(packet.Uuid, out ProtoThing[] items)) return;
                        
                        // Try to get the other party's items on offer, returning on failure
                        if (!trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] otherPartyItems)) return;
                    
                        // Give the other party's items to the sender
                        sendCompleteTradePacket(connectionId, trade.TradeId, false, otherPartyUuid, otherPartyItems);
                    
                        // Check if the other party is logged in
                        if (!userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) || !otherPartyLoggedIn) return;
                    
                        // Try to get the other party's connection ID
                        if (!userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId)) return;
                    
                        // Give the sender's items to the other party
                        sendCompleteTradePacket(otherPartyConnectionId, trade.TradeId, false, packet.Uuid, items);
                    
                        // Remove the trade
                        activeTrades.Remove(packet.TradeId);
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
        /// Sends a <c>CompleteTradePacket</c> to the given connection ID.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="cancelled">Whether the trade was cancelled</param>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        /// <param name="items">Collection of items to send</param>
        private void sendCompleteTradePacket(string connectionId, string tradeId, bool cancelled, string otherPartyUuid, IEnumerable<ProtoThing> items)
        {
            // Create and pack a response
            CompleteTradePacket packet = new CompleteTradePacket
            {
                TradeId = tradeId,
                Success = !cancelled,
                OtherPartyUuid = otherPartyUuid,
                Items = {items}
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
                
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }
        
        /// <summary>
        /// Sends an <c>UpdateTradeStatusPacket</c> with the given trade ID and both accepted states to the given connection.
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
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }
        
        /// <summary>
        /// Handles incoming <c>UpdateTradeItemsPacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>UpdateTradeItemsPacket</c></param>
        private void updateTradeItemsPacketHandler(string connectionId, UpdateTradeItemsPacket packet)
        {
            // Ignore packets from non-authenticated and non-logged in users
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (activeTradesLock)
            {
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
                    // Try to get the other party's UUID, returning on failure
                    if (!trade.TryGetOtherParty(packet.Uuid, out string otherPartyUuid)) return;
                    
                    // Try to get the other party's items on offer, returning on failure
                    if (!trade.TryGetItemsOnOffer(otherPartyUuid, out ProtoThing[] otherPartyItems)) return;
                    
                    // Send an update to the sender
                    sendUpdateTradeItemsPacket(connectionId, trade.TradeId, packet.Items, otherPartyItems);
                    
                    // Check if the other party is logged in
                    if (!userManager.TryGetLoggedIn(otherPartyUuid, out bool otherPartyLoggedIn) || !otherPartyLoggedIn) return;
                    
                    // Try to get the other party's connection ID
                    if (!userManager.TryGetConnection(otherPartyUuid, out string otherPartyConnectionId)) return;
                    
                    // Send an update to the other party
                    sendUpdateTradeItemsPacket(otherPartyConnectionId, trade.TradeId, otherPartyItems, packet.Items);
                }
            }
        }
        
        /// <summary>
        /// Sends an <c>UpdateTradeItemsPacket</c> with the given items of both parties for the given trade.
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
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }
    }
}