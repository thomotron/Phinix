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
    public class ServerTests
    {
        [Test()]
        public void Server_NewlyInitialisedWithEndpoint_DoesNotImplode()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);

            Assert.That(server != null);
        }

        [Test()]
        public void Listening_ServerNewlyInitialised_ReturnsFalse()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);

            Assert.That(server.Listening == false);
        }

        [Test()]
        public void Listening_ServerStartedAndStopped_CorrectlyReturnsState()
        {
            Server server = new Server(new IPEndPoint(IPAddress.Any, 16180));
            Connection.StartListening(ConnectionType.TCP, new IPEndPoint(IPAddress.Any, 16180));

            Assert.That(server.Listening == true);

            Connection.StopListening();

            Assert.That(server.Listening == false);

            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_ZeroPort_StartsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
            Server server = new Server(endpoint);

            Assert.DoesNotThrow(
                () => server.Start()
            );
            Assert.That(Connection.Listening(ConnectionType.TCP) == true);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_LegacyPhiPort_StartsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);

            Assert.DoesNotThrow(
                () => server.Start()
            );
            Assert.That(Connection.Listening(ConnectionType.TCP) == true);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Start_AlreadyRunning_ThrowsException()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);
            Connection.StartListening(ConnectionType.TCP, endpoint);

            Assert.That(Connection.Listening(ConnectionType.TCP) == true);
            Assert.Throws<Exception>(
                () => server.Start()
            );

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }

        [Test()]
        public void Stop_ServerNotRunning_DoesNotThrow()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);
            
            Assert.DoesNotThrow(
                () => server.Stop()
            );
        }

        [Test()]
        public void Stop_ServerRunning_StopsSuccessfully()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 16180);
            Server server = new Server(endpoint);
            Connection.StartListening(ConnectionType.TCP, endpoint);

            server.Stop();

            Assert.That(Connection.Listening(ConnectionType.TCP) == false);

            Connection.StopListening();
            NetworkComms.Shutdown(0);
        }
    }
}