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
        public void ConnectHostname_LocalServerRunning_ConnectionSuccessful()
        {
            string address = "localhost";
            int port = 16180;
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            NetClient netClient = new NetClient();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);

            Assert.That(NetworkComms.TotalNumConnections() == 0);

            netClient.Connect(address, port);

            Assert.That(netClient.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections() > 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void ConnectAddress_LocalServerRunning_ConnectionSuccessful()
        {
            string address = "127.0.0.1";
            int port = 16180;
            IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            NetClient netClient = new NetClient();
            Connection.StartListening(ConnectionType.TCP, serverEndpoint);

            Assert.That(NetworkComms.TotalNumConnections() == 0);

            netClient.Connect(address, port);

            Assert.That(netClient.Connected == true);
            Assert.That(NetworkComms.TotalNumConnections() > 0);

            Connection.StopListening(ConnectionType.TCP);
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Connect_ValidLocalHostnameAndPort_ThrowsServerNotRunning()
        {
            string address = "localhost";
            int port = 16180;
            NetClient client = new NetClient();

            Assert.Throws<ConnectionSetupException>(() =>
            {
                client.Connect(address, port);
            });

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Connect_ValidRemoteHostnameAndPort_ThrowsServerNotRunning()
        {
            string address = "example.com";
            int port = 16180;
            NetClient client = new NetClient();

            Assert.Throws<ConnectionSetupException>(() =>
            {
                client.Connect(address, port);
            });

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Connect_ValidLocalAddressAndPort_ThrowsServerNotRunning()
        {
            string address = "127.0.0.1";
            int port = 16180;
            NetClient client = new NetClient();

            Assert.Throws<ConnectionSetupException>(() =>
            {
                client.Connect(address, port);
            });

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Connect_ValidRemoteAddressAndPort_ThrowsServerNotRunning()
        {
            string address = "8.8.8.8"; // Unlikely Google's DNS will be down
            int port = 16180;
            NetClient client = new NetClient();

            Assert.Throws<ConnectionSetupException>(() =>
            {
                client.Connect(address, port);
            });

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Connect_InvalidAddressValidPort_ThrowsInvalidAddressException()
        {
            string address = "this_is_not_a_valid_hostname.not_a_valid_tld";
            int port = 16180;
            NetClient client = new NetClient();

            Assert.Throws<InvalidAddressException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_InvalidAddressNegativePort_ThrowsArgumentOutOfRangeException()
        {
            string address = "this_is_not_a_valid_hostname.not_a_valid_tld";
            int port = -1;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_InvalidAddressTooHighPort_ThrowsArgumentOutOfRangeException()
        {
            string address = "this_is_not_a_valid_hostname.not_a_valid_tld";
            int port = 65536;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidLocalAddressNegativePort_ThrowsArgumentOutOfRangeException()
        {
            string address = "127.0.0.1";
            int port = -1;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidLocalAddressTooHighPort_ThrowsArgumentOutOfRangeException()
        {
            string address = "127.0.0.1";
            int port = 65536;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidLocalHostnameNegativePort_ThrowsArgumentOutOfRangeException()
        {
            string address = "localhost";
            int port = -1;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidLocalHostnameTooHighPort_ThrowsArgumentOutOfRangeException()
        {
            string address = "localhost";
            int port = 65536;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidRemoteAddressNegativePort_ThrowsArgumentOutOfRangeException()
        {
            string address = "8.8.8.8";
            int port = -1;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidRemoteAddressTooHighPort_ThrowsArgumentOutOfRangeException()
        {
            string address = "8.8.8.8";
            int port = 65536;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidRemoteHostnameNegativePort_ThrowsArgumentOutOfRangeException()
        {
            string address = "example.com";
            int port = -1;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
        }

        [Test()]
        public void Connect_ValidRemoteHostnameTooHighPort_ThrowsArgumentOutOfRangeException()
        {
            string address = "example.com";
            int port = 65536;
            NetClient client = new NetClient();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                client.Connect(address, port);
            });
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