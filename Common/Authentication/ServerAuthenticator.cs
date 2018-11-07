using System;
using Connections;

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

        public override void packetHandler(string packetType, string connectionId, byte[] data)
        {
            if (!packetType.Equals(MODULE_NAME)) throw new Exception();

            Packet packet = Packet.Deserialise(data);
            if (packet is AuthenticatePacket)
            {
                // TODO: AuthenticatePacket handling
                Console.WriteLine("Got an AuthenticatePacket");
            }
            else
            {
                // TODO: Discard packet
                Console.WriteLine("Got some other packet");
            }
        }
    }
}
