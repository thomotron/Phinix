using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
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
                string tradeId = Guid.NewGuid().ToString();
                activeTrades.Add(tradeId, new Trade(new[]{packet.Uuid, packet.OtherPartyUuid}));
                
                // Send both parties a successful trade creation packet
                sendSuccessfulCreateTradeResponsePacket(connectionId, tradeId, packet.OtherPartyUuid); // Sender
                sendSuccessfulCreateTradeResponsePacket(otherPartyConnectionId, tradeId, packet.Uuid); // Other party
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
    }
}