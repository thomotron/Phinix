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
    }
}