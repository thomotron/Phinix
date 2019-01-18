using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;

namespace Connections
{
    public class NetClient : NetCommon
    {
        public bool Connected => client != null && client.IsRunning && serverPeer != null && serverPeer.ConnectionState == ConnectionState.Connected;

        /// <summary>
        /// Raised when connecting to a server.
        /// </summary>
        public event EventHandler OnConnecting; 
        /// <summary>
        /// Raised when disconnecting from a server.
        /// </summary>
        public event EventHandler OnDisconnect;

        /// <summary>
        /// Client that piggy-backs the listener and communicates with the server.
        /// </summary>
        private NetManager client;
        /// <summary>
        /// Peer representing the server's side of the connection.
        /// </summary>
        private NetPeer serverPeer;

        /// <summary>
        /// Thread that polls the client backend for incoming packets.
        /// </summary>
        private Thread pollThread;

        /// <summary>
        /// Creates a new <c>NetClient</c> instance.
        /// </summary>
        /// <param name="checkInterval">Interval in seconds between keepalive transmissions</param>
        public NetClient(int checkInterval = 5)
        {
            // Set up the client
            client = new NetManager(listener, "Phinix")
            {
                PingInterval = checkInterval
            };

            // Forward events
            listener.PeerConnectedEvent += (peer) => { OnConnecting?.Invoke(this, EventArgs.Empty); };
            listener.PeerDisconnectedEvent += (peer, info) => { OnDisconnect?.Invoke(this, EventArgs.Empty); };
        }

        /// <summary>
        /// Attempts to connect to the given endpoint. This will close an existing connection.
        /// </summary>
        /// <param name="endpoint">Endpoint to connect to</param>
        public void Connect(IPEndPoint endpoint)
        {
            // Close the active connection before we make a new one.
            Disconnect();

            // Try to connect
            client.Start();
            serverPeer = client.Connect(endpoint.Address.ToString(), endpoint.Port);
            
            // Start a polling thread to check for incoming packets
            pollThread = new Thread(() =>
            {
                while (true)
                {
                    client.PollEvents();
                    Thread.Sleep(10);
                }
            });
            pollThread.Start();
        }

        /// <summary>
        /// Attempts to connect to the given address and port. This will close an existing connection.
        /// </summary>
        /// <param name="address">Address to connect to</param>
        /// <param name="port">Port the server is listening on</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidAddressException"></exception>
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
                
                OnConnecting?.Invoke(this, EventArgs.Empty);
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
            catch (SocketException)
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
            // Check if the client is running
            if (client.IsRunning)
            {
                // Stop the client
                client.Stop();
                
                // Raise the OnDisconnect event
                OnDisconnect?.Invoke(this, EventArgs.Empty);
            }
            
            // Kill the poll thread and clear the variable
            if (pollThread != null)
            {
                pollThread.Abort();
                pollThread = null;
            }
        }

        /// <summary>
        /// Sends a message to a module through the current connection.
        /// </summary>
        /// <param name="module">Target module. Cannot be null or empty.</param>
        /// <param name="serialisedMessage">Serialised message. Cannot be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotConnectedException"></exception>
        public void Send(string module, byte[] serialisedMessage)
        {
            // Disallow null parameters
            if (string.IsNullOrEmpty(module)) throw new ArgumentNullException(nameof(module));
            if (serialisedMessage == null) throw new ArgumentNullException(nameof(serialisedMessage));

            if (!Connected) throw new NotConnectedException(serverPeer);

            serverPeer.Send(serialisedMessage, SendOptions.ReliableOrdered);
        }
    }
}