using System;
using System.Net;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NUnit.Framework;

namespace Connections.Tests
{
    [TestFixture]
    public class ServerTests
    {
        [Test]
        public void RegisterHandlersTest()
        {
            // It doesn't seem possible to verify whether the connection established and closing handlers have been registered or not so this'll have to do.
            // This test is pretty much 1/3rd pointless because of that.
            NetworkComms.RemoveGlobalIncomingPacketHandler("Phinix");

            Assert.IsFalse(NetworkComms.GlobalIncomingPacketHandlerExists("Phinix"));

            Server.RegisterHandlers();

            Assert.IsTrue(NetworkComms.GlobalIncomingPacketHandlerExists("Phinix"));

            NetworkComms.RemoveGlobalIncomingPacketHandler("Phinix");
        }

        [Test]
        public void UnregisterHandlers()
        {
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("Phinix", (header, connection, incomingObject) => { });

            Assert.IsTrue(NetworkComms.GlobalIncomingPacketHandlerExists("Phinix"));

            Server.UnregisterHandlers();

            Assert.IsFalse(NetworkComms.GlobalIncomingPacketHandlerExists("Phinix"));

            NetworkComms.RemoveGlobalIncomingPacketHandler("Phinix");
        }
        
        [Test]
        public void ListeningTest()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);

            Connection.StartListening(ConnectionType.TCP, endpoint);
            
            Assert.AreEqual(Connection.Listening(ConnectionType.TCP), Server.Listening);
            Assert.IsTrue(Server.Listening);

            Connection.StopListening(ConnectionType.TCP);
            
            Assert.AreEqual(Connection.Listening(ConnectionType.TCP), Server.Listening);
            Assert.IsFalse(Server.Listening);
        }

        [Test]
        public void StartTest()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);

            Assert.IsFalse(Connection.Listening(ConnectionType.TCP));

            Server.Start(endpoint);

            Assert.IsTrue(Connection.Listening(ConnectionType.TCP));

            Assert.Catch(() =>
            {
                Server.Start(endpoint);
            });

            Connection.StopListening(ConnectionType.TCP);

            Assert.IsFalse(Connection.Listening(ConnectionType.TCP));
        }

        [Test]
        public void StopTest()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
            Connection.StartListening(ConnectionType.TCP, endpoint);

            Assert.IsTrue(Connection.Listening(ConnectionType.TCP));

            Server.Stop();

            Assert.IsFalse(Connection.Listening(ConnectionType.TCP));
        }
    }
}