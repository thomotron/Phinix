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
        /// <exception cref="ConnectionSetupException"></exception>
        public void Connect(IPEndPoint endpoint)
        {
            // Close the active connection before we make a new one.
            Disconnect();

            ConnectionInfo connectionInfo = new ConnectionInfo(endpoint);
            connection = TCPConnection.GetConnection(connectionInfo);
        }

        /// <summary>
        /// Attempts to connect to the given address and port. This will close an existing connection.
        /// </summary>
        /// <param name="address">Address to connect to</param>
        /// <param name="port">Port the server is listening on</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidAddressException"></exception>
        /// <exception cref="ConnectionSetupException"></exception>
        public void Connect(string address, int port)
        {
            // Ensure the port is within the valid range
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    port,
                    $"Cannot assign a port below {IPEndPoint.MinPort} or above {IPEndPoint.MaxPort}."
                );
            }

            // Close the active connection before we make a new one.
            Disconnect();

            // Parse the given hostname
            IPAddress resolvedAddress;
            if (TryParseHostnameOrAddress(address, out resolvedAddress))
            {
                Connect(new IPEndPoint(resolvedAddress, port));
            }
            else
            {
                throw new InvalidAddressException(address);
            }
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
            if (TryResolveHostname(hostname, out address)) return true;
            return false;
        }

        /// <summary>
        /// Attempts to resolve the given string to an <c>IPAddress</c>. Returns true if resolution was successful.
        /// </summary>
        /// <param name="hostname">Hostname to resolve</param>
        /// <param name="address">Resolved address</param>
        /// <returns>Resolved successfully</returns>
        private bool TryResolveHostname(string hostname, out IPAddress address)
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

        /// <summary>
        /// Sends a message to a module through the current connection.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="serialisedMessage">Serialised message</param>
        /// <exception cref="NotConnectedException"></exception>
        public void Send(string module, byte[] serialisedMessage)
        {
            if (!Connected) throw new NotConnectedException(connection);

            connection.SendObject(module, serialisedMessage);
        }
    }
}