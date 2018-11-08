using System;
using Connections;
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
        public override event EventHandler<LogEventArgs> OnLogEntry;
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        private NetServer netServer;
        
        public ServerAuthenticator(NetServer netServer)
        {
            this.netServer = netServer;
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);

            Console.WriteLine("Authentication module initialised");
        }

        protected override void packetHandler(string packetType, string connectionId, byte[] data)
        {
            if (!packetType.Equals(MODULE_NAME))
            {
                RaiseLogEntry(new LogEventArgs("Got a packet destined for a different module (" + packetType + ")", LogLevel.WARNING));
                return;
            }

            Any message = Any.Parser.ParseFrom(data);
            TypeUrl typeUrl = new TypeUrl(message.TypeUrl);

            if (typeUrl.Namespace != "Authentication")
            {
                RaiseLogEntry(new LogEventArgs("Got a packet type from a different namespace than we're expecting (" + typeUrl.Namespace + ")", LogLevel.WARNING));
            }
            
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    // TODO: AuthenticatPacket handling
                    RaiseLogEntry(new LogEventArgs("Got an AuthenticatePacket"));
                    break;
                default:
                    // TODO: Discard packet
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + ")", LogLevel.WARNING));
                    break;
            }
        }
    }
}
