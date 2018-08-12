using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections.TCP;
using ProtoBuf;

namespace Connections
{
    public class NetClient : NetCommon
    {
        public bool Connected => connection != null && connection.ConnectionAlive();

        private TCPConnection connection;

        /// <summary>
        /// Attempts to connect to the given endpoint. This will close an existing connection.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        public void Connect(IPEndPoint endpoint)
        {
            // Close the active connection before we make a new one.
            Disconnect();

            ConnectionInfo connectionInfo = new ConnectionInfo(endpoint);
            connection = TCPConnection.GetConnection(connectionInfo);
        }

        /// <summary>
        /// Attempts to parse the given 
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool TryConnect(string hostname, int port)
        {
            IPAddress address;
            if (!IPAddress.TryParse(hostname, out address))
            {
                if (!TryQueryHostname(hostname, out address))
                {
                    return false;
                }
            }
            
            Connect(new IPEndPoint(address, port));
            return true;
        }

        private bool TryQueryHostname(string hostname, out IPAddress address)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(hostname);

            // TODO: Get some creamy IPv6 support up in here
            address = addresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            return address != null;
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