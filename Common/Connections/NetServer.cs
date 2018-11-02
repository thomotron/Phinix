using System;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

namespace Connections
{
    public class NetServer : NetCommon
    {
        public bool Listening => Connection.Listening(ConnectionType.TCP);

        public readonly IPEndPoint Endpoint;

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
        }

        /// <summary>
        /// Registers a delegate to be called when a new connection is established.
        /// </summary>
        /// <param name="d">Handler delegate</param>
        public void RegisterConnectionEstablishedHandler(NetworkComms.ConnectionEstablishShutdownDelegate d)
        {
            NetworkComms.AppendGlobalConnectionEstablishHandler(d);
        }

        /// <summary>
        /// Unregisters an existing connection established handler delegate.
        /// </summary>
        /// <param name="d">Handler delegate</param>
        public void UnregisterConnectionEstablishedHandler(NetworkComms.ConnectionEstablishShutdownDelegate d)
        {
            NetworkComms.RemoveGlobalConnectionEstablishHandler(d);
        }

        /// <summary>
        /// Registers a delegate to be called when an existing connection is closed.
        /// </summary>
        /// <param name="d">Handler delegate</param>
        public void RegisterConnectionClosedHandler(NetworkComms.ConnectionEstablishShutdownDelegate d)
        {
            NetworkComms.AppendGlobalConnectionCloseHandler(d);
        }

        /// <summary>
        /// Unregisters an existing connection closed handler delegate.
        /// </summary>
        /// <param name="d">Handler delegate</param>
        public void UnregisterConnectionClosedHandler(NetworkComms.ConnectionEstablishShutdownDelegate d)
        {
            NetworkComms.RemoveGlobalConnectionCloseHandler(d);
        }

        /// <summary>
        /// Sends a message to a module through the given connection.
        /// </summary>
        /// <param name="connection">Connection to recipient</param>
        /// <param name="module">Target module</param>
        /// <param name="serialisedMessage">Serialised message</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotConnectedException"></exception>
        public void Send(Connection connection, string module, byte[] serialisedMessage)
        {
            // Disallow null parameters
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrEmpty(module)) throw new ArgumentNullException(nameof(module));
            if (serialisedMessage == null) throw new ArgumentNullException(nameof(serialisedMessage));

            if (connection.ConnectionInfo.ConnectionState != ConnectionState.Established || !connection.ConnectionAlive()) throw new NotConnectedException(connection);

            connection.SendObject(module, serialisedMessage);
        }
    }
}
