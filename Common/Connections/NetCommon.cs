using System.Collections.Generic;
using NetworkCommsDotNet;

namespace Connections
{
    public class NetCommon
    {
        private List<string> registeredPacketTypes = new List<string>();

        /// <summary>
        /// Registers a callback to be run whever a given packet type is received.
        /// </summary>
        /// <param name="packetType">Type of packets handled by handler</param>
        /// <param name="callback">Callback delegate</param>
        public void RegisterPacketHandler(string packetType, NetworkComms.PacketHandlerCallBackDelegate<byte[]> callback)
        {
            if (NetworkComms.GlobalIncomingPacketHandlerExists(packetType))
            {
                throw new PacketHandlerAlreadyRegisteredException(packetType);
            }
            else
            {
                NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>(packetType, callback);
                registeredPacketTypes.Add(packetType);
            }
        }

        /// <summary>
        /// Unregisters a packet handler callback by the type of packets it handles.
        /// </summary>
        /// <param name="packetType">Type of packets handled by handler</param>
        public void UnregisterPacketHandler(string packetType)
        {
            if (NetworkComms.GlobalIncomingPacketHandlerExists(packetType))
            {
                NetworkComms.RemoveGlobalIncomingPacketHandler(packetType);
                registeredPacketTypes.Remove(packetType);
            }
        }

        /// <summary>
        /// Unregisters all currently registered packet handlers.
        /// </summary>
        public void UnregisterAllPacketHandlers()
        {
            // Get a local snapshot to avoid issues when updating the collection
            string[] _registeredPacketTypes = registeredPacketTypes.ToArray();
            foreach (string packetType in _registeredPacketTypes)
            {
                UnregisterPacketHandler(packetType);
            }
        }
    }
}