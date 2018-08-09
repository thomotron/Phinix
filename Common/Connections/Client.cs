using System;
using System.IO;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using ProtoBuf;

namespace Connections
{
    public static class Client
    {
        public static bool Connected => connection.ConnectionAlive();

        private static TCPConnection connection;

        /// <summary>
        /// Attempts to connect to the given endpoint. This will close an existing connection.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        public static void Connect(IPEndPoint endpoint)
        {
            // Close the active connection before we make a new one.
            Disconnect();

            ConnectionInfo connectionInfo = new ConnectionInfo(endpoint);
            connection = TCPConnection.GetConnection(connectionInfo);
        }

        /// <summary>
        /// Closes the current connection if it is open.
        /// </summary>
        public static void Disconnect()
        {
            // Check if the connection is open
            if (connection != null && connection.ConnectionAlive())
            {
                connection.CloseConnection(false);
                connection.Dispose();
            }
        }

        /// <summary>
        /// Placeholder for processing opened connections.
        /// </summary>
        /// <param name="connection"></param>
        private static void ConnectionEstablishedHandler(Connection connection)
        {

        }

        /// <summary>
        /// Placeholder for processing closed connecctions.
        /// </summary>
        /// <param name="connection"></param>
        private static void ConnectionClosedHandler(Connection connection)
        {

        }

        /// <summary>
        /// Placeholder for processing incoming packets.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="connection"></param>
        /// <param name="incomingObject"></param>
        private static void IncomingPacketHandler(PacketHeader header, Connection connection, byte[] incomingObject)
        {

        }
    }
}