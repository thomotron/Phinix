using System;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Server authentication module.
    /// Handles incoming authentication attempts and greets new connections.
    /// </summary>
    public class ServerAuthenticator : Authenticator
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        /// <summary>
        /// <c>NetServer</c> to send packets and bind events to.
        /// </summary>
        private NetServer netServer;
        
        public ServerAuthenticator(NetServer netServer)
        {
            this.netServer = netServer;
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netServer.OnConnectionEstablished += ConnectionEstablishedHandler;    
        }

        private void ConnectionEstablishedHandler(object sender, ConnectionEventArgs e)
        {
            RaiseLogEntry(new LogEventArgs("Sending HelloPacket to incoming connection " + e.ConnectionId, LogLevel.DEBUG));
            
            // Construct a HelloPacket
            HelloPacket hello = new HelloPacket
            {
                AuthType = AuthTypes.PhiKey,
                ServerName = "",
                ServerDescription = "",
                SessionId = Guid.NewGuid().ToString()
            };
            
            // Pack it into an Any message
            Any packedHello = Any.Pack(hello, "Phinix");
            
            // Send it
            netServer.Send(e.ConnectionId, MODULE_NAME, packedHello.ToByteArray());
        }

        /// <inheritdoc />
        protected override void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!validatePacket(module, data, out Any message, out TypeUrl typeUrl)) return;
            
            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    // TODO: HelloPacket handling
                    RaiseLogEntry(new LogEventArgs("Got an AuthenticatePacket"));
                    break;
                default:
                    // TODO: Discard packet
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
    }
}
