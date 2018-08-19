using NUnit.Framework;

namespace Connections.Tests
{
    [TestFixture()]
    public class InvalidAddressExceptionTests
    {
        [Test()]
        public void InvalidAddressException_ValidAddress_ContainsAddressInMessageAndException()
        {
            string address = "example.not_a_real_tld";
            InvalidAddressException e = new InvalidAddressException(address);

            Assert.That(e.Message.Contains(address));
            Assert.That(e.Address == address);
        }

        [Test()]
        public void InvalidAddressException_EmptyAddress_ContainsBlankAddressInMessageAndException()
        {
            string address = "";
            InvalidAddressException e = new InvalidAddressException(address);

            Assert.That(e.Message == "The address \'\' could not be resolved as an IP address or hostname.");
            Assert.That(e.Address == "");
        }
    }
}