using System;
using Connections;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Client authentication module.
    /// Handles incoming greetings and attempts to authenticate with a server.
    /// </summary>
    public class ClientAuthenticator : Authenticator
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        /// <summary>
        /// Raised on a successful authentication attempt.
        /// </summary>
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationSuccess;
        /// <summary>
        /// Raised on a failed authentication attempt.
        /// </summary>
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationFailure; 
        
        /// <summary>
        /// <c>NetClient</c> to send packets and bind events to.
        /// </summary>
        private NetClient netClient;
        
        public ClientAuthenticator(NetClient netClient)
        {
            this.netClient = netClient;
            
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
        }

        /// <inheritdoc />
        protected override void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!validatePacket(module, data, out Any message, out TypeUrl typeUrl)) return;
            
            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "HelloPacket":
                    // TODO: HelloPacket handling
                    RaiseLogEntry(new LogEventArgs("Got a HelloPacket", LogLevel.DEBUG));
                    break;
                case "AuthResponsePacket":
                    // TODO: AuthResponsePacket handling
                    RaiseLogEntry(new LogEventArgs("Got an AuthResponsePacket", LogLevel.DEBUG));
                    break;
                default:
                    // TODO: Discard packet
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
    }
}
