using System;
using System.Collections.Generic;
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
                case "Thing":
                    thingHandler(connectionId, message.Unpack<Thing>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        private void thingHandler(string cId, Thing packet)
        {
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it to each logged in user
            foreach (string connectionId in userManager.GetConnections())
            {
                netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
            }
        }
    }
}