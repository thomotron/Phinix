using System;

namespace Connections
{
    public class PacketHandlerAlreadyRegisteredException : Exception
    {
        public string PacketType;
        public override string Message => $"Cannot register packet handler for {PacketType} type packets. Another handler is already registered.";

        public PacketHandlerAlreadyRegisteredException(string packetType) : base()
        {
            this.PacketType = packetType;
        }
    }
}