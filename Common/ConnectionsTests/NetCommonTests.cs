using NUnit.Framework;
using Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using NUnit.Framework.Internal;

namespace Connections.Tests
{
    [TestFixture()]
    public class NetCommonTests
    {
        [Test()]
        public void RegisterPacketHandler_UniquePacketType_RegisteredSuccessfully()
        {
            NetCommon netCommon = new NetCommon();

            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void RegisterPacketHandler_MultipleUniquePacketTypes_AllRegisteredSuccessfully()
        {
            NetCommon netCommon = new NetCommon();

            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Chat");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Authentication");
        }

        [Test()]
        public void RegisterPacketHandler_DuplicatePacketType_ThrowsException()
        {
            NetCommon netCommon = new NetCommon();

            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                netCommon.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );
            Assert.Throws<PacketHandlerAlreadyRegisteredException>(() =>
                netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Authentication");
        }

        [Test()]
        public void UnregisterPacketHandler_RegisteredPacketType_UnregisteredSuccessfully()
        {
            NetCommon netCommon = new NetCommon();
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("BaloneyPacketType", (header, connection, incomingObject) => { });

            netCommon.UnregisterPacketHandler("BaloneyPacketType");

            Assert.DoesNotThrow(() =>
                NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void UnregisterPacketHandler_UnregisteredPacketType_DoesNotThrow()
        {
            NetCommon netCommon = new NetCommon();

            Assert.DoesNotThrow(
                () => netCommon.UnregisterPacketHandler("BaloneyPacketType")
            );
        }

        [Test()]
        public void UnregisterAllPacketHandlers_NoRegisteredHandlers_DoesNotThrow()
        {
            NetCommon netCommon = new NetCommon();

            Assert.DoesNotThrow(
                () => netCommon.UnregisterAllPacketHandlers()
            );
        }

        [Test()]
        public void UnregisterAllPacketHandlers_OneRegisteredHandler_UnregisteredSuccessfully()
        {
            NetCommon netCommon = new NetCommon();

            netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { });

            netCommon.UnregisterAllPacketHandlers();

            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void UnregisterAllPacketHandlers_SeveralRegisteredHandlers_UnregisteredAllSuccessfully()
        {
            NetCommon netCommon = new NetCommon();

            netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { });
            netCommon.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { });
            netCommon.RegisterPacketHandler("Trading", (header, connection, incomingObject) => { });
            netCommon.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { });
            netCommon.RegisterPacketHandler("SuperSecretPacketType", (header, connection, incomingObject) => { });

            netCommon.UnregisterAllPacketHandlers();

            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("Trading", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => netCommon.RegisterPacketHandler("SuperSecretPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Chat");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Trading");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Authentication");
            NetworkComms.RemoveGlobalIncomingPacketHandler("SuperSecretPacketType");
        }
    }
}