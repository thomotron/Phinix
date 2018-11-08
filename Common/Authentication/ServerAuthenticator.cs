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
                return;
            }

            Any message = Any.Parser.ParseFrom(data);
            TypeUrl typeUrl = new TypeUrl(message.TypeUrl);

            if (typeUrl.Namespace != "Authentication")
            {
            }
            
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    // TODO: AuthenticatPacket handling
                    break;
                default:
                    // TODO: Discard packet
                    break;
            }
        }
    }
}
