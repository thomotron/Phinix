using NUnit.Framework;
using Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;

namespace Connections.Tests
{
    [TestFixture()]
    public class ClientTests
    {
        [Test()]
        public void TryConnect_ServerNotRunning_ThrowsException()
        {
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            Client client = new Client();

            Assert.Throws<ConnectionSetupException>(
                () => client.TryConnect(clientEndpoint)
            );

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void TryConnect_LocalServerRunning_ConnectionSuccessful()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            Client client = new Client();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);

            Assert.That(NetworkComms.TotalNumConnections() == 0);

            client.TryConnect(clientEndpoint);

            Assert.That(client.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections() > 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Disconnect_NotConnected_DoesNotThrow()
        {
            Client client = new Client();

            Assert.DoesNotThrow(
                () => client.Disconnect()
            );
        }

        [Test()]
        public void Disconnect_ConnectedToServer_DisconnectionSuccessful()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            Client client = new Client();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            client.TryConnect(clientEndpoint);

            Assert.That(client.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections(ConnectionType.TCP) > 0);
            Assert.DoesNotThrow(
                () => client.Disconnect()
            );
            Assert.That(client.Connected == false);
            Assert.That(NetworkComms.TotalNumConnections() == 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }
    }
}