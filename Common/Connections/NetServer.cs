using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Connections
{
    public class NetServer : NetCommon
    {
        /// <summary>
        /// Whether the server is currently listening for connections.
        /// </summary>
        public bool Listening => server != null && server.IsRunning;

        /// <summary>
        /// <c>IPEndpoint</c> the server is listening on.
        /// </summary>
        public readonly IPEndPoint Endpoint;
        
        /// <summary>
        /// Raised when a new connection is established.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> OnConnectionEstablished;
        /// <summary>
        /// Raised when an existing connection is closed.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> OnConnectionClosed;

        /// <summary>
        /// Server that piggy-backs the listener and communicates with clients.
        /// </summary>
        private NetManager server;
        /// <summary>
        /// Collection of currently-connected peers organised by their connection ID.
        /// </summary>
        private Dictionary<string, NetPeer> connectedPeers;

        /// <summary>
        /// Thread that polls the server backend for incoming packets.
        /// </summary>
        private Thread pollThread;

        public NetServer(IPEndPoint endpoint, int maxConnections)
        {
            this.Endpoint = endpoint;
            
            this.server = new NetManager(listener, maxConnections, "Phinix")
            {
                PingInterval = 5000
            };
            this.connectedPeers = new Dictionary<string, NetPeer>();
            
            // Forward events
            listener.PeerConnectedEvent += (peer) =>
            {
                // Add or update the peer's connection
                string connectionId = peer.ConnectId.ToString("X");
                if (connectedPeers.ContainsKey(connectionId))
                {
                    connectedPeers[connectionId] = peer;
                }
                else
                {
                    connectedPeers.Add(connectionId, peer);
                }
                
                // Raise the connection established event for this peer
                OnConnectionEstablished?.Invoke(this, new ConnectionEventArgs(connectionId));
            };
            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                // Remove the peer's connection
                string connectionId = peer.ConnectId.ToString("X");
                connectedPeers.Remove(connectionId);
                
                // Raise the connection established event for this peer
                OnConnectionClosed?.Invoke(this, new ConnectionEventArgs(connectionId));
            };
        }

        /// <summary>
        /// Starts listening for connections on the given endpoint.
        /// </summary>
        public void Start()
        {
            if (Listening) throw new Exception("Cannot start listening while already doing so.");

            server.Start(Endpoint.Port);
            
            // Start a polling thread to check for incoming packets
            pollThread = new Thread(() =>
            {
                while (true)
                {
                    server.PollEvents();
                    Thread.Sleep(10);
                }
            });
            pollThread.Start();
        }

        /// <summary>
        /// Stops listening for new connections and terminates any active ones.
        /// </summary>
        public void Stop()
        {
            server.Stop();
            connectedPeers.Clear();
            
            // Kill the poll thread and clear the variable
            if (pollThread != null)
            {
                pollThread.Abort();
                pollThread = null;
            }
        }

        /// <summary>
        /// Sends a message to a module through the given connection.
        /// </summary>
        /// <param name="connectionId">Connection ID of recipient</param>
        /// <param name="module">Target module</param>
        /// <param name="serialisedMessage">Serialised message</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotConnectedException"></exception>
        public void Send(string connectionId, string module, byte[] serialisedMessage)
        {
            // Disallow null parameters
            if (connectionId == null) throw new ArgumentNullException(nameof(connectionId));
            if (string.IsNullOrEmpty(module)) throw new ArgumentNullException(nameof(module));
            if (serialisedMessage == null) throw new ArgumentNullException(nameof(serialisedMessage));
            
            // Check the connection exists
            if (!connectedPeers.ContainsKey(connectionId))
            {
                throw new NotConnectedException();
            }
            
            
            // Get the connection by it's ID
            NetPeer peer = connectedPeers[connectionId];
            
            // Make sure the connection is open
            if (peer.ConnectionState != ConnectionState.Connected)
            {
                throw new NotConnectedException(peer);
            }
            
            // Write the module and message data to a NetDataWriter stream
            NetDataWriter writer = new NetDataWriter();
            writer.Put(module);
            writer.Put(serialisedMessage);

            peer.Send(writer, SendOptions.ReliableOrdered);
        }
    }
}
