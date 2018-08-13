using System;

namespace Connections
{
    public class InvalidAddressException : Exception
    {
        public override string Message => $"The address \'{Address}\' could not be resolved as an IP address or hostname.";

        public readonly string Address;

        public InvalidAddressException(string address)
        {
            this.Address = address;
        }
    }
}