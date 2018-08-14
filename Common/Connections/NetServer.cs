using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using ProtoBuf;

namespace Connections
{
    public class NetServer : NetCommon
    {
        public bool Listening => Connection.Listening(ConnectionType.TCP);

        private IPEndPoint endpoint;

        public NetServer(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Starts listening for connections on the given endpoint.
        /// </summary>
        public void Start()
        {
            if (!Listening)
            {
                RegisterConnectionHandlers();
                Connection.StartListening(ConnectionType.TCP, endpoint);
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
            UnregisterConnectionHandlers();
        }

        /// <summary>
        /// Registers NetworkComms.Net event handlers for connection established and connection closed.
        /// </summary>
        private void RegisterConnectionHandlers()
        {
            NetworkComms.AppendGlobalConnectionEstablishHandler(ConnectionEstablishedHandler);
            NetworkComms.AppendGlobalConnectionCloseHandler(ConnectionClosedHandler);
        }

        /// <summary>
        /// Unregisters NetworkComms.Net event handlers. Effectively the opposite of <c>RegisterHandlers()</c>.
        /// </summary>
        private void UnregisterConnectionHandlers()
        {
            NetworkComms.RemoveGlobalConnectionEstablishHandler(ConnectionEstablishedHandler);
            NetworkComms.RemoveGlobalConnectionCloseHandler(ConnectionClosedHandler);
        }

        /// <summary>
        /// Placeholder for processing opened connections.
        /// </summary>
        /// <param name="connection"></param>
        private void ConnectionEstablishedHandler(Connection connection)
        {
            Console.WriteLine($"Got a connection from {connection.ConnectionInfo.NetworkIdentifier}!");
        }

        /// <summary>
        /// Placeholder for processing closed connecctions.
        /// </summary>
        /// <param name="connection"></param>
        private void ConnectionClosedHandler(Connection connection)
        {
            Console.WriteLine($"Lost a connection from {connection.ConnectionInfo.NetworkIdentifier}!");
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

            if (!connection.ConnectionAlive()) throw new NotConnectedException(connection);

            connection.SendObject(module, serialisedMessage);
        }
    }
}
