using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Utils
{
    public static class ProtobufPacketHelper
    {
        public const string PREFIX = "Phinix";
        
        /// <summary>
        /// Attempts to validate a serialised packet.
        /// If this method succeeds it can be assumed safe to process the packet further.
        /// </summary>
        /// <param name="_namespace">Namespace the packet is handled in</param>
        /// <param name="module">Module the packet is handled in</param>
        /// <param name="targetModule">Target module of the packet</param>
        /// <param name="data">Serialised packet</param>
        /// <param name="parsedMessage">Parsed packet as an <c>Any</c> message</param>
        /// <param name="typeUrl">Parsed TypeUrl</param>
        /// <returns>Packet was validated successfully</returns>
        public static bool ValidatePacket(string _namespace, string module, string targetModule, byte[] data, out Any parsedMessage, out TypeUrl typeUrl)
        {
            // Initialise out variables
            parsedMessage = null;
            typeUrl = null;
            
            // Make sure the packet is destined for this module, just in case
            if (!targetModule.Equals(module)) return false;
            
            // Parse the incoming message
            parsedMessage = Any.Parser.ParseFrom(data);
            
            // Get the TypeUrl from the message to help determine what it actually is
            try
            {
                typeUrl = new TypeUrl(parsedMessage.TypeUrl);
            }
            catch (ArgumentException)
            {
                return false;
            }

            // Check that the prefix matches
            if (typeUrl.Prefix != PREFIX) return false;
            
            // Check that the message's namespace matches the one we will be using with our packets
            if (!typeUrl.Namespace.Equals(_namespace)) return false;
            
            // Nothing bad has happened so far, so the packet is clear for further processing
            return true;
        }

        /// <summary>
        /// Packs the given packet into an <c>Any</c> with the Phinix prefix.
        /// </summary>
        /// <param name="packet">Packet to pack</param>
        /// <returns>Packed packet</returns>
        public static Any Pack(IMessage packet)
        {
            return Any.Pack(packet, PREFIX);
        }
    }
}