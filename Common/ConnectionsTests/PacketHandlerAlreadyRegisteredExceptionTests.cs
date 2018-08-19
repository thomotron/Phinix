using NUnit.Framework;

namespace Connections.Tests
{
    [TestFixture()]
    public class PacketHandlerAlreadyRegisteredExceptionTests
    {
        [Test()]
        public void PacketHandlerAlreadyRegisteredException_ValidPacketType_ContainsTypeInMessageAndException()
        {
            string packetType = "BaloneyPacketType";
            PacketHandlerAlreadyRegisteredException e = new PacketHandlerAlreadyRegisteredException(packetType);

            Assert.That(e.Message.Contains(packetType));
            Assert.That(e.PacketType == packetType);
        }

        [Test()]
        public void PacketHandlerAlreadyRegisteredException_EmptyPacketType_ContainsBlankTypeInMessageAndException()
        {
            string packetType = "";
            PacketHandlerAlreadyRegisteredException e = new PacketHandlerAlreadyRegisteredException(packetType);

            Assert.That(e.Message == "Cannot register packet handler for  type packets. Another handler is already registered.");
            Assert.That(e.PacketType == "");
        }
    }
}