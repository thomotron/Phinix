using System;
using LiteNetLib;

namespace Connections
{
    /// <summary>
    /// Should be thrown whenever a message cannot be sent because there is no open connection available.
    /// </summary>
    public class NotConnectedException : Exception
    {
        public override string Message => "No open connection to send message through.";
        public NetPeer Peer;

        public NotConnectedException()
        {
            // This space is intentionally left blank.
        }

        public NotConnectedException(NetPeer peer)
        {
            this.Peer = peer;
        }
    }
}