using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using Utils;

namespace Connections
{
    public class NetServer : NetCommon
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Whether the server is currently listening for connections.
        /// </summary>
        public bool Listening => server != null && server.IsRunning;

        /// <summary>
        /// <see cref="IPEndPoint"/> the server is listening on.
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
                PingInterval = 5000,
                DisconnectTimeout = 30000
            };
            this.connectedPeers = new Dictionary<string, NetPeer>();

            // Subscribe to events
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

                // Log the event
                RaiseLogEntry(new LogEventArgs(string.Format("Opened connection from {0}:{1} (ConnID: {2})", peer.EndPoint.Host, peer.EndPoint.Port, connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

                // Raise the connection established event for this peer
                OnConnectionEstablished?.Invoke(this, new ConnectionEventArgs(connectionId));
            };
            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                // Remove the peer's connection
                string connectionId = peer.ConnectId.ToString("X");
                connectedPeers.Remove(connectionId);

                // Log the event
                RaiseLogEntry(new LogEventArgs(string.Format("Closed connection from {0}:{1} (ConnID: {2})", peer.EndPoint.Host, peer.EndPoint.Port, connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

                // Raise the connection established event for this peer
                OnConnectionClosed?.Invoke(this, new ConnectionEventArgs(connectionId));
            };
        }

        /// <summary>
        /// Starts listening for connections on the given endpoint.
        /// </summary>
        public void Start()
        {
            // Stop listening
            Stop();

            // Start listening
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
        /// <exception cref="ArgumentException"><see cref="connectionId"/> cannot be null or empty</exception>
        /// <exception cref="ArgumentException"><see cref="module"/> cannot be null or empty</exception>
        /// <exception cref="ArgumentNullException"><see cref="serialisedMessage"/> cannot be null</exception>
        /// <exception cref="NotConnectedException">Recipient must be connected to send a message</exception>
        public void Send(string connectionId, string module, byte[] serialisedMessage)
        {
            // Disallow null parameters
            if (string.IsNullOrEmpty(connectionId)) throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            if (string.IsNullOrEmpty(module)) throw new ArgumentException("Module cannot be null or empty", nameof(module));
            if (serialisedMessage == null) throw new ArgumentNullException(nameof(serialisedMessage), "Serialised message cannot be null");

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

            // Send the message in a reliable and ordered fashion
            peer.Send(writer, SendOptions.ReliableOrdered);
        }

        /// <summary>
        /// Tries to send a message to a module through the given connection.
        /// </summary>
        /// <param name="connectionId">Connection ID of recipient</param>
        /// <param name="module">Target module</param>
        /// <param name="serialisedMessage">Serialised message</param>
        /// <returns>Whether the message was sent successfully</returns>
        public bool TrySend(string connectionId, string module, byte[] serialisedMessage)
        {
            try
            {
                // Try to send the message
                Send(connectionId, module, serialisedMessage);

                return true;
            }
            catch (NotConnectedException e)
            {
                // Catch connection exceptions
                RaiseLogEntry(new LogEventArgs("Cannot send message, " + e.Message, LogLevel.ERROR));

                return false;
            }
            catch (ArgumentNullException e)
            {
                // Catch argument exceptions
                RaiseLogEntry(new LogEventArgs(string.Format("Cannot send message, argument {0} is null or empty", e.ParamName), LogLevel.ERROR));

                return false;
            }
            catch (ArgumentException e)
            {
                // Catch more argument exceptions
                RaiseLogEntry(new LogEventArgs(string.Format("Cannot send message, argument {0} is null or empty", e.ParamName), LogLevel.ERROR));

                return false;
            }
            catch (Exception e)
            {
                // Catch anything else
                RaiseLogEntry(new LogEventArgs("Got an unusual exception when sending a message\n" + e, LogLevel.ERROR));

                return false;
            }
        }

        /// <summary>
        /// Disconnects the connection with the given ID.
        /// </summary>
        /// <param name="connectionId">ID of the connection to disconnect</param>
        /// <exception cref="ArgumentException"><see cref="connectionId"/> cannot be null or empty</exception>
        public void Disconnect(string connectionId)
        {
            // Disallow null parameters
            if (string.IsNullOrEmpty(connectionId)) throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

            // Disconnect the client if they are connected
            if (!connectedPeers.ContainsKey(connectionId))
            {
                server.DisconnectPeer(connectedPeers[connectionId]);
            }
        }
    }
}
