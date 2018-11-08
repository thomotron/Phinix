using System;
using System.IO;
using System.Reflection;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Client authentication module.
    /// Handles incoming greetings and attempts to authenticate a server.
    /// </summary>
    public class ClientAuthenticator : Authenticator
    {
        public override event EventHandler<LogEventArgs> OnLogEntry;
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        private NetClient netClient;
        
        public ClientAuthenticator(NetClient netClient)
        {
            this.netClient = netClient;
            
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
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
                case "HelloPacket":
                    // TODO: HelloPacket handling
                    RaiseLogEntry(new LogEventArgs("Got a HelloPacket"));
                    break;
                case "AuthResponsePacket":
                    // TODO: AuthResponsePacket handling
                    RaiseLogEntry(new LogEventArgs("Got an AuthResponsePacket"));
                    break;
                default:
                    // TODO: Discard packet
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + ")", LogLevel.WARNING));
                    break;
            }
        }
    }
}
