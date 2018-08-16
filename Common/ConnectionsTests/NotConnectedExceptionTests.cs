using NUnit.Framework;
using Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.TCP;
using NetworkCommsDotNet.Connections.UDP;

namespace Connections.Tests
{
    [TestFixture()]
    public class NotConnectedExceptionTests
    {
        [Test()]
        public void NotConnectedException_EmptyConstructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                NotConnectedException exception = new NotConnectedException();
                Assert.That(exception.Message == "Connection for message to be sent through is not open.");
            });
        }

        [Test()]
        public void NotConnectedException_ConnectionConstructor_InitialisedSuccessfully()
        {
            // Use a UDP connection so we don't have to set up a listener
            Connection c = UDPConnection.GetConnection(new ConnectionInfo("127.0.0.1", 16180), UDPOptions.None);
            NotConnectedException exception;

            Assert.DoesNotThrow(() =>
            {
                exception = new NotConnectedException(c);
                Assert.That(exception.Connection == c);
                Assert.That(exception.Message == "Connection for message to be sent through is not open.");
            });

            NetworkComms.Shutdown(0);
        }
    }
}