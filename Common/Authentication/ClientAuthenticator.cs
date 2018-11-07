using System;
using Connections;

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
        
        public override void packetHandler(string packetType, string connectionId, byte[] data)
        {
            if (!packetType.Equals(MODULE_NAME))
            {
                Console.WriteLine("Got a packet for a different module wtf");
            }

            Packet packet = Packet.Deserialise(data);
            if (packet is HelloPacket)
            {
                // TODO: HelloPacket handling
                Console.WriteLine("Got a hello packet");
            }
            else if (packet is AuthResponsePacket)
            {
                // TODO: AuthResponsePacket handling
                Console.WriteLine("Got and AuthResponsePacket");
            }
            else
            {
                // TODO: Discard packet
                Console.WriteLine("Got some other packet");
            }
        }
    }
}
