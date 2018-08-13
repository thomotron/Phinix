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

        /// <summary>
        /// Attempts to parse or resolve the given string to an <c>IPAddress</c>. Returns true if parsing or resolution was successful.
        /// </summary>
        /// <param name="hostname">Hostname or IP address to parse</param>
        /// <param name="address">Parsed address</param>
        /// <returns>Parsed successfully</returns>
        private bool TryParseHostnameOrAddress(string hostname, out IPAddress address)
        {
            if (IPAddress.TryParse(hostname, out address)) return true;
            if (TryQueryHostname(hostname, out address)) return true;
            return false;
        }

        /// <summary>
        /// Attempts to resolve the given string to an <c>IPAddress</c>. Returns true if resolution was successful.
        /// </summary>
        /// <param name="hostname">Hostname to resolve</param>
        /// <param name="address">Resolved address</param>
        /// <returns>Resolved successfully</returns>
        private bool TryQueryHostname(string hostname, out IPAddress address)
        {
            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(hostname);
            }
            catch (SocketException e)
            {
                address = IPAddress.None;
                return false;
            }

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