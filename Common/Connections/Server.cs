using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using ProtoBuf;

namespace Connections
{
    public static class Server
    {
        public static bool Listening => Connection.Listening(ConnectionType.TCP);

        /// <summary>
        /// Registers event handlers for NetworkComms.Net for connection established, connection closed, and incoming data.
        /// </summary>
        public static void RegisterHandlers()
        {
            NetworkComms.AppendGlobalConnectionEstablishHandler(ConnectionEstablishedHandler);
            NetworkComms.AppendGlobalConnectionCloseHandler(ConnectionClosedHandler);
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("Phinix", IncomingPacketHandler);
        }

        /// <summary>
        /// Unregisters event handlers for NetworkComms.Net. Effectively the opposite of <c>RegisterHandlers()</c>.
        /// </summary>
        public static void UnregisterHandlers()
        {
            NetworkComms.RemoveGlobalConnectionEstablishHandler(ConnectionEstablishedHandler);
            NetworkComms.RemoveGlobalConnectionCloseHandler(ConnectionClosedHandler);
            NetworkComms.RemoveGlobalIncomingPacketHandler("Phinix");
        }

        /// <summary>
        /// Starts listening for connections on the given endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint to bind to</param>
        public static void Start(IPEndPoint endpoint)
        {
            if (!Listening)
            {
                Connection.StartListening(ConnectionType.TCP, endpoint);
            }
            else
            {
                throw new Exception("Cannot start listening while already doing so.");
            }
        }

        /// <summary>
        /// Stops listening for new connections and terminates any active ones.
        /// </summary>
        public static void Stop()
        {
            Connection.StopListening();
            NetworkComms.CloseAllConnections(ConnectionType.TCP);
        }

        /// <summary>
        /// Placeholder for processing opened connections.
        /// </summary>
        /// <param name="connection"></param>
        public static void ConnectionEstablishedHandler(Connection connection)
        {

        }

        /// <summary>
        /// Placeholder for processing closed connecctions.
        /// </summary>
        /// <param name="connection"></param>
        public static void ConnectionClosedHandler(Connection connection)
        {

        }

        /// <summary>
        /// Placeholder for processing incoming packets.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="connection"></param>
        /// <param name="incomingObject"></param>
        public static void IncomingPacketHandler(PacketHeader header, Connection connection, byte[] incomingObject)
        {

        }
    }
}
