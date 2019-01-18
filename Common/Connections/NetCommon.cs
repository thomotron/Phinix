using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using LiteNetLib;
using LiteNetLib.Utils;
using Utils;

namespace Connections
{
    public class NetCommon
    {
        public static readonly Version Version = Assembly.GetAssembly(typeof(NetCommon)).GetName().Version;
        
        /// <summary>
        /// Listener that handles communications over the wire.
        /// </summary>
        internal EventBasedNetListener listener = new EventBasedNetListener();

        /// <summary>
        /// Packet handler callback delegate. Used when registering packet handlers as the callback method.
        /// Exposes basic information about the incoming packet.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="incomingObject">Data payload</param>
        public delegate void PacketHandlerDelegate(string module, string connectionId, byte[] incomingObject);

        /// <summary>
        /// Dictionary containing packet types and the handlers registered to them.
        /// </summary>
        private Dictionary<string, PacketHandlerDelegate> registeredPacketHandlers = new Dictionary<string, PacketHandlerDelegate>();

        public NetCommon()
        {
            // Register the packet handler wrapper
            listener.NetworkReceiveEvent += packetHandlerCallbackWrapper;
        }

        /// <summary>
        /// Registers a callback to be run whenever a given packet type is received.
        /// </summary>
        /// <param name="packetType">Type of packets handled by handler</param>
        /// <param name="callback">Callback delegate</param>
        public void RegisterPacketHandler(string packetType, PacketHandlerDelegate callback)
        {
            if (registeredPacketHandlers.ContainsKey(packetType))
            {
                throw new PacketHandlerAlreadyRegisteredException(packetType);
            }
            
            registeredPacketHandlers.Add(packetType, callback);
        }

        /// <summary>
        /// Unregisters a packet handler callback by the type of packets it handles.
        /// </summary>
        /// <param name="packetType">Type of packets handled by handler</param>
        public void UnregisterPacketHandler(string packetType)
        {
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
        /// Wrapper callback for incoming packets to translate from network library-specific classes to more generic ones.
        /// Called whenever any packet type with a registered handler is received to adapt the callback values.
        /// </summary>
        /// <param name="peer">Peer the packet originated from</param>
        /// <param name="reader">Data reader containing the payload</param>
        private void packetHandlerCallbackWrapper(NetPeer peer, NetDataReader reader)
        {
            // Get the connection ID by converting LiteNetLib's connection id long to a hex string
            string connectionId = peer.ConnectId.ToString("X");
            
            // Deserialise the message and get the type URL to identify it's type
            TypeUrl typeUrl = new TypeUrl(Any.Parser.ParseFrom(reader.Data).TypeUrl);
            
            if (registeredPacketHandlers.ContainsKey(typeUrl.Namespace))
            {
                registeredPacketHandlers[typeUrl.Namespace].Invoke(typeUrl.Namespace, connectionId, reader.Data);
            }
        }
    }
}