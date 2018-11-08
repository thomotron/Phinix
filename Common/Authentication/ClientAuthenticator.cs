using System;
using System.IO;
using System.Reflection;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Client authentication module.
    /// Handles incoming greetings and attempts to authenticate a server.
    /// </summary>
    public class ClientAuthenticator : Authenticator
    {
        private NetClient netClient;
        
        public ClientAuthenticator(NetClient netClient)
        {
            this.netClient = netClient;
            
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
            
            Console.WriteLine("Authentication module initialised");
        }

        protected override void packetHandler(string packetType, string connectionId, byte[] data)
        {
            if (!packetType.Equals(MODULE_NAME))
            {
                Console.WriteLine("Got a packet for a different module wtf");
            }

            Any message = Any.Parser.ParseFrom(data);
            TypeUrl typeUrl = new TypeUrl(message.TypeUrl);

            if (typeUrl.Namespace != "Authentication")
            {
            }
            
            switch (typeUrl.Type)
            {
                case "HelloPacket":
                    // TODO: HelloPacket handling
                    break;
                case "AuthResponsePacket":
                    // TODO: AuthResponsePacket handling
                    break;
                default:
                    // TODO: Discard packet
                    break;
            }
        }
    }
}
