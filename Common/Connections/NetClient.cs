using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using Utils;

namespace Connections
{
    public class NetClient : NetCommon
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        public bool Connected => clientNetManager != null &&                              // We have a NetManager
                                 clientNetManager.IsRunning &&                            // The NetManager is running
                                 serverPeer != null &&                                    // We have connection info about the server
                                 serverPeer.ConnectionState == ConnectionState.Connected; // The connection info identifies as connected

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
        private NetManager clientNetManager;
        /// <summary>
        /// Peer representing the server's side of the connection.
        /// </summary>
        private NetPeer serverPeer;

        /// <summary>
        /// Thread that polls the client backend for incoming packets.
        /// </summary>
        private Thread pollThread;

        /// <summary>
        /// Creates a new <see cref="NetClient"/> instance.
        /// </summary>
        /// <param name="checkInterval">Interval in milliseconds between keepalive transmissions</param>
        /// <param name="timeout">Duration in milliseconds after which the connection will be terminated if no response is received</param>
        public NetClient(int checkInterval = 5000, int timeout = 30000)
        {
            // Set up the client
            clientNetManager = new NetManager(listener, "Phinix")
            {
                PingInterval = checkInterval,
                DisconnectTimeout = timeout
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
            clientNetManager.Start();
            serverPeer = clientNetManager.Connect(endpoint.Address.ToString(), endpoint.Port);
            
            // Start a polling thread to check for incoming packets
            pollThread = new Thread(() =>
            {
                while (true)
                {
                    clientNetManager.PollEvents();
                    Thread.Sleep(10);
                }
            });
            pollThread.Start();
            
            // Raise the connection event
            OnConnecting?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Attempts to connect to the given host on the given port. This will close an existing connection.
        /// </summary>
        /// <param name="host">Address or hostname of the server</param>
        /// <param name="port">Port the server is listening on</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidAddressException"></exception>
        public void Connect(string host, int port)
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

            // Parse the given hostname
            if (TryResolveHostname(host, out IPAddress resolvedAddress))
            {
                // Try to connect using the resolved address
                Connect(new IPEndPoint(resolvedAddress, port));
            }
            else
            {
                throw new InvalidAddressException(host);
            }
        }

        /// <summary>
        /// Attempts to resolve the given string to an <see cref="IPAddress"/>. Returns true if resolution was successful.
        /// </summary>
        /// <param name="hostname">Hostname to resolve</param>
        /// <param name="address">Resolved address</param>
        /// <returns>Resolved successfully</returns>
        private static bool TryResolveHostname(string hostname, out IPAddress address)
        {
            IPAddress[] addresses;
            try
            {
                // Query DNS for a list of addresses
                addresses = Dns.GetHostAddresses(hostname);
            }
            catch (SocketException)
            {
                // Couldn't contact a DNS server, set output to nothing and return a failure
                address = null;
                return false;
            }

            // Use the first address if we got one, otherwise set the output to null
            address = addresses.Length > 0 ? addresses[0] : null;

            // Return if we got an address
            return address != null;
        }

        /// <summary>
        /// Closes the current connection if it is open.
        /// </summary>
        public void Disconnect()
        {
            // Check if the client is running
            if (clientNetManager.IsRunning)
            {
                // Stop the client
                clientNetManager.Stop();
                
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
        /// <param name="module">Target module</param>
        /// <param name="serialisedMessage">Serialised message</param>
        /// <exception cref="ArgumentNullException"><see cref="module"/> cannot be null or empty</exception>
        /// <exception cref="ArgumentNullException"><see cref="serialisedMessage"/> cannot be null or empty</exception>
        /// <exception cref="NotConnectedException">Must be connected to send a message</exception>
        public void Send(string module, byte[] serialisedMessage)
        {
            // Disallow null parameters
            if (string.IsNullOrEmpty(module)) throw new ArgumentException("Module cannot be null or empty.", nameof(module));
            if (serialisedMessage == null) throw new ArgumentNullException(nameof(serialisedMessage), "Serialised message cannot be null.");

            // Make sure we are connected before attempting to send anything
            if (!Connected) throw new NotConnectedException(serverPeer);
            
            // Write the module and message data to a NetDataWriter stream
            NetDataWriter writer = new NetDataWriter();
            writer.Put(module);
            writer.Put(serialisedMessage);

            // Send the message in a reliable and ordered fashion
            serverPeer.Send(writer, SendOptions.ReliableOrdered);
        }
    }
}