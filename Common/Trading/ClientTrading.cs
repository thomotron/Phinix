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
        /// <c>NetClient</c> instance to bind events and send data through.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <c>ClientAuthenticator</c> instance to get a session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <c>ClientUserManager</c> instance to get a UUID from.
        /// </summary>
        private ClientUserManager userManager;

        /// <summary>
        /// Collection of active trades with other users organised by trade ID.
        /// </summary>
        private Dictionary<string, Trade> activeTrades;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>activeTrades</c>.
        /// </summary>
        private object activeTradesLock = new object();

        public ClientTrading(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;
            
            this.activeTrades = new Dictionary<string, Trade>();
            
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
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
            CompleteTradePacket packet = new CompleteTradePacket
            {
                Success = false,
                TradeId = tradeId
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
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
        /// Handles incoming packets from <c>NetCommon</c>.
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
                    createTradeResponsePacketHandler(connectionId, message.Unpack<CreateTradeResponsePacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>CreateTradeResponsePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>CreateTradeResponsePacket</c></param>
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
    }
}