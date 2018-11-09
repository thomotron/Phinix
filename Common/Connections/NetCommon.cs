using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

namespace Connections
{
    public class NetCommon
    {
        public static readonly Version Version = Assembly.GetAssembly(typeof(NetCommon)).GetName().Version;

        /// <summary>
        /// Packet handler callback delegate. Used when registering packet handlers as the callback method.
        /// Exposes basic information about the incoming packet.
        /// </summary>
        /// <param name="packetType">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="incomingObject">Data payload</param>
        public delegate void PacketHandlerDelegate(string module, string connectionId, byte[] incomingObject);

        /// <summary>
        /// Dictionary containing packet types and the handlers registered to them.
        /// </summary>
        private Dictionary<string, PacketHandlerDelegate> registeredPacketHandlers = new Dictionary<string, PacketHandlerDelegate>();

        /// <summary>
        /// Registers a callback to be run whenever a given packet type is received.
        /// </summary>
        /// <param name="packetType">Type of packets handled by handler</param>
        /// <param name="callback">Callback delegate</param>
        public void RegisterPacketHandler(string packetType, PacketHandlerDelegate callback)
        {
            if (NetworkComms.GlobalIncomingPacketHandlerExists(packetType) || registeredPacketHandlers.ContainsKey(packetType))
            {
                throw new PacketHandlerAlreadyRegisteredException(packetType);
            }
            
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>(packetType, packetHandlerCallbackWrapper);
            registeredPacketHandlers.Add(packetType, callback);
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
            }

            if (registeredPacketHandlers.ContainsKey(packetType))
            {
                registeredPacketHandlers.Remove(packetType);
            }
        }

        /// <summary>
        /// Unregisters all currently registered packet handlers.
        /// </summary>
        public void UnregisterAllPacketHandlers()
        {
            // Get a local snapshot to avoid issues when updating the collection
            string[] registeredPacketTypes = registeredPacketHandlers.Keys.ToArray();
            foreach (string packetType in registeredPacketTypes)
            {
                UnregisterPacketHandler(packetType);
            }
        }
        
        /// <summary>
        /// Wrapper callback for incoming packets to translate from NetworkComms.Net-specific classes to more generic ones.
        /// Called whenever any packet type with a registered handler is received to adapt the callback values.
        /// </summary>
        /// <param name="header">Packet header</param>
        /// <param name="connection">Connection originated from</param>
        /// <param name="incomingObject">Data payload</param>
        private void packetHandlerCallbackWrapper(PacketHeader header, Connection connection, byte[] incomingObject)
        {
            string packetType = header.PacketType;
            string connectionId = connection.ConnectionInfo.NetworkIdentifier;
            
            if (registeredPacketHandlers.ContainsKey(packetType))
            {
                registeredPacketHandlers[packetType].Invoke(packetType, connectionId, incomingObject);
            }
        }
    }
}