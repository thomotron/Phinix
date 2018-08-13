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
    public class NetClientTests
    {
        [Test()]
        public void Connect_ServerNotRunning_ThrowsException()
        {
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();

            Assert.Throws<ConnectionSetupException>(
                () => netClient.Connect(clientEndpoint)
            );

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void ConnectEndpoint_LocalServerRunning_ConnectionSuccessful()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);

            Assert.That(NetworkComms.TotalNumConnections() == 0);

            netClient.Connect(clientEndpoint);

            Assert.That(netClient.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections() > 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Disconnect_NotConnected_DoesNotThrow()
        {
            NetClient netClient = new NetClient();

            Assert.DoesNotThrow(
                () => netClient.Disconnect()
            );
        }

        [Test()]
        public void Disconnect_ConnectedToServer_DisconnectionSuccessful()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            Assert.That(netClient.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections(ConnectionType.TCP) > 0);
            Assert.DoesNotThrow(
                () => netClient.Disconnect()
            );
            Assert.That(netClient.Connected == false);
            Assert.That(NetworkComms.TotalNumConnections() == 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }
    }
}