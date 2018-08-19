using NUnit.Framework;
using System;
using System.Net;
using System.Threading;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;

namespace Connections.Tests
{
    [TestFixture()]
    public class NetServerTests
    {
        [Test()]
        public void Server_NewlyInitialisedWithEndpoint_DoesNotImplode()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);

            Assert.That(netServer != null);
        }

        [Test()]
        public void Listening_ServerNewlyInitialised_ReturnsFalse()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);

            Assert.That(netServer.Listening == false);
        }

        [Test()]
        public void Listening_ServerStartedAndStopped_CorrectlyReturnsState()
        {
            NetServer netServer = new NetServer(new IPEndPoint(IPAddress.Any, 16180));
            Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, 16180));

            Assert.That(netServer.Listening == true);

            Connection.StopListening();

            Assert.That(netServer.Listening == false);

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_ZeroPort_StartsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
            NetServer netServer = new NetServer(endpoint);

            Assert.DoesNotThrow(
                () => netServer.Start()
            );
            Assert.That(Connection.Listening(ConnectionType.TCP) == true);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_LegacyPhiPort_StartsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);

            Assert.DoesNotThrow(
                () => netServer.Start()
            );
            Assert.That(Connection.Listening(ConnectionType.TCP) == true);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_AlreadyRunning_ThrowsException()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);
            Connection.StartListening(ConnectionType.TCP, endpoint);

            Assert.That(Connection.Listening(ConnectionType.TCP) == true);
            Assert.Throws<Exception>(
                () => netServer.Start()
            );

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Stop_ServerNotRunning_DoesNotThrow()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);
            
            Assert.DoesNotThrow(
                () => netServer.Stop()
            );
        }

        [Test()]
        public void Stop_ServerRunning_StopsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            NetServer netServer = new NetServer(endpoint);
            Connection.StartListening(ConnectionType.TCP, endpoint);

            netServer.Stop();

            Assert.That(Connection.Listening(ConnectionType.TCP) == false);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Send_ConnectedToClientValidParams_DoesNotThrow()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            NetServer netServer = new NetServer(serverEndpoint);
            string module = "SomeModule";
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            Connection c = TCPConnection.GetConnection(new ConnectionInfo(clientEndpoint));
            Assert.DoesNotThrow(() =>
            {
                netServer.Send(c, module, new byte[1000]);
            });
            
            c.CloseConnection(false);
            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Send_ConnectedToClientNullConnection_ThrowsNullArgumentException()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            NetServer netServer = new NetServer(serverEndpoint);
            string module = "SomeModule";
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            Connection c = TCPConnection.GetConnection(new ConnectionInfo(clientEndpoint));
            Assert.Throws<ArgumentNullException>(() =>
            {
                netServer.Send(null, module, new byte[1000]);
            });

            c.CloseConnection(false);
            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Send_ConnectedToClientNullModule_ThrowsNullArgumentException() // Copy-paste testing, wheeeeeeeeeee!
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            NetServer netServer = new NetServer(serverEndpoint);
            string module = "SomeModule";
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            Connection c = TCPConnection.GetConnection(new ConnectionInfo(clientEndpoint));
            Assert.Throws<ArgumentNullException>(() =>
            {
                netServer.Send(c, null, new byte[1000]);
            });
            
            c.CloseConnection(false);
            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Send_ConnectedToClientNullMessage_ThrowsNullArgumentException() // Copy-paste testing, wheeeeeeeeeee!
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            NetServer netServer = new NetServer(serverEndpoint);
            string module = "SomeModule";
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            Connection c = TCPConnection.GetConnection(new ConnectionInfo(clientEndpoint));
            Assert.Throws<ArgumentNullException>(() =>
            {
                netServer.Send(c, module, null);
            });
            
            c.CloseConnection(false);
            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Send_NotConnected_ThrowsNotConnectedException()
        {
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, 16180);
            IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Loopback, 16180);
            NetClient netClient = new NetClient();
            NetServer netServer = new NetServer(serverEndpoint);
            string module = "SomeModule";
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);
            netClient.Connect(clientEndpoint);

            ConnectionInfo clientConnectionInfo = new ConnectionInfo(clientEndpoint);
            Connection clientConnection = TCPConnection.GetConnection(clientConnectionInfo);
            clientConnection.CloseConnection(false);
            while (NetworkComms.ConnectionExists(clientConnectionInfo)) Thread.Sleep(10);

            Assert.Throws<NotConnectedException>(() =>
            {
                netServer.Send(clientConnection, module, new byte[1000]);
            });
            
            clientConnection.CloseConnection(false);
            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }
    }
}