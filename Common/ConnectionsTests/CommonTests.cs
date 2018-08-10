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
    public class CommonTests
    {
        [Test()]
        public void RegisterPacketHandler_UniquePacketType_RegisteredSuccessfully()
        {
            Common common = new Common();

            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void RegisterPacketHandler_MultipleUniquePacketTypes_AllRegisteredSuccessfully()
        {
            Common common = new Common();

            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Chat");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Authentication");
        }

        [Test()]
        public void RegisterPacketHandler_DuplicatePacketType_ThrowsException()
        {
            Common common = new Common();

            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(() =>
                common.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );
            Assert.Throws<PacketHandlerAlreadyRegisteredException>(() =>
                common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
            NetworkComms.RemoveGlobalIncomingPacketHandler("Authentication");
        }

        [Test()]
        public void UnregisterPacketHandler_RegisteredPacketType_UnregisteredSuccessfully()
        {
            Common common = new Common();
            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("BaloneyPacketType", (header, connection, incomingObject) => { });

            common.UnregisterPacketHandler("BaloneyPacketType");

            Assert.DoesNotThrow(() =>
                NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void UnregisterPacketHandler_UnregisteredPacketType_DoesNotThrow()
        {
            Common common = new Common();

            Assert.DoesNotThrow(
                () => common.UnregisterPacketHandler("BaloneyPacketType")
            );
        }

        [Test()]
        public void UnregisterAllPacketHandlers_NoRegisteredHandlers_DoesNotThrow()
        {
            Common common = new Common();

            Assert.DoesNotThrow(
                () => common.UnregisterAllPacketHandlers()
            );
        }

        [Test()]
        public void UnregisterAllPacketHandlers_OneRegisteredHandler_UnregisteredSuccessfully()
        {
            Common common = new Common();

            common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { });

            common.UnregisterAllPacketHandlers();

            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );

            // Remove global packet handler to avoid interference with other tests
            NetworkComms.RemoveGlobalIncomingPacketHandler("BaloneyPacketType");
        }

        [Test()]
        public void UnregisterAllPacketHandlers_SeveralRegisteredHandlers_UnregisteredAllSuccessfully()
        {
            Common common = new Common();

            common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { });
            common.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { });
            common.RegisterPacketHandler("Trading", (header, connection, incomingObject) => { });
            common.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { });
            common.RegisterPacketHandler("SuperSecretPacketType", (header, connection, incomingObject) => { });

            common.UnregisterAllPacketHandlers();

            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("BaloneyPacketType", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("Chat", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("Trading", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("Authentication", (header, connection, incomingObject) => { })
            );
            Assert.DoesNotThrow(
                () => common.RegisterPacketHandler("SuperSecretPacketType", (header, connection, incomingObject) => { })
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