using System;
using System.Linq;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Tools;

namespace Connections
{
    public class NetServer : NetCommon
    {
        /// <summary>
        /// Whether the server is currently listening for connections.
        /// </summary>
        public bool Listening => Connection.Listening(ConnectionType.TCP);

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

        public NetServer(IPEndPoint endpoint)
        {
            this.Endpoint = endpoint;
        }

        /// <summary>
        /// Starts listening for connections on the given endpoint.
        /// </summary>
        public void Start()
        {
            if (!Listening)
            {
                registerConnectionEvents();
                Connection.StartListening(ConnectionType.TCP, Endpoint);
            }
            else
            {
                throw new Exception("Cannot start listening while already doing so.");
            }
        }

        /// <summary>
        /// Stops listening for new connections and terminates any active ones.
        /// </summary>
        public void Stop()
        {
            Connection.StopListening();
            NetworkComms.CloseAllConnections(ConnectionType.TCP);
            unregisterConnectionEvents();
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
            ShortGuid connectionGuid = new ShortGuid(connectionId);
            if (!NetworkComms.ConnectionExists(connectionGuid, ConnectionType.TCP))
            {
                throw new NotConnectedException();
            }
            
            // Get the connection by it's ID
            Connection connection = NetworkComms.GetExistingConnection(new ShortGuid(connectionId), ConnectionType.TCP).First();
            
            // Make sure the connection is open
            if (connection.ConnectionInfo.ConnectionState != ConnectionState.Established || !connection.ConnectionAlive())
            {
                throw new NotConnectedException(connection);
            }
            
            // Send the message
            connection.SendObject(module, serialisedMessage);
        }

        /// <summary>
        /// Registers event handler wrappers for connection established and closed into NetworkComms.Net.
        /// Used to event-ify the callbacks.
        /// </summary>
        private void registerConnectionEvents()
        {
            NetworkComms.AppendGlobalConnectionEstablishHandler(connectionEstablishWrapperDelegate);
            NetworkComms.AppendGlobalConnectionCloseHandler(connectionCloseWrapperDelegate);
        }
        
        /// <summary>
        /// Unregisters the event handler wrappers for connection established and closed.
        /// </summary>
        private void unregisterConnectionEvents()
        {
            NetworkComms.RemoveGlobalConnectionEstablishHandler(connectionEstablishWrapperDelegate);
            NetworkComms.RemoveGlobalConnectionCloseHandler(connectionCloseWrapperDelegate);
        }
        
        /// <summary>
        /// Wraps NetworkComms.Net's <c>ConnectionEstablishShutdownDelegate</c> to something more generic and raises the
        /// <c>OnConnectionEstablished</c> event.
        /// </summary>
        /// <param name="connection">Establishing connection</param>
        private void connectionEstablishWrapperDelegate(Connection connection)
        {
            OnConnectionEstablished?.Invoke(this, new ConnectionEventArgs(connection.ConnectionInfo.NetworkIdentifier));
        }
        
        /// <summary>
        /// Wraps NetworkComms.Net's <c>ConnectionEstablishShutdownDelegate</c> to something more generic and raises the
        /// <c>OnConnectionClosed</c> event.
        /// </summary>
        /// <param name="connection">Closing connection</param>
        private void connectionCloseWrapperDelegate(Connection connection)
        {
            OnConnectionClosed?.Invoke(this, new ConnectionEventArgs(connection.ConnectionInfo.NetworkIdentifier));
        }
    }
}
