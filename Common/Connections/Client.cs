using System;
using System.IO;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using ProtoBuf;

namespace Connections
{
    public class Client : Common
    {
        public bool Connected => connection != null && connection.ConnectionAlive();

        private TCPConnection connection;

        /// <summary>
        /// Attempts to connect to the given endpoint. This will close an existing connection.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        public void TryConnect(IPEndPoint endpoint)
        {
            // Close the active connection before we make a new one.
            Disconnect();

            ConnectionInfo connectionInfo = new ConnectionInfo(endpoint);
            connection = TCPConnection.GetConnection(connectionInfo);
        }

        /// <summary>
        /// Closes the current connection if it is open.
        /// </summary>
        public void Disconnect()
        {
            // Check if the connection is open
            if (connection != null)
            {
                connection.CloseConnection(false);
                
                connection = null;
            }
        }
    }
}