using System.IO;
using ProtoBuf;

namespace Connections
{
    /// <summary>
    /// A generic class that sets a standard format for client-server communication.
    /// </summary>
    public abstract class Packet
    {
        public byte[] Serialise(Packet packet)
        {
            byte[] serialisedPacket;
            
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.Serialize(ms, packet);
                serialisedPacket = ms.ToArray();
            }

            return serialisedPacket;
        }

        public static Packet Deserialise(byte[] data)
        {
            Packet deserialisedPacket;

            using (MemoryStream ms = new MemoryStream(data))
            {
                deserialisedPacket = Serializer.Deserialize<Packet>(ms);
            }

            return deserialisedPacket;
        }
    }
}