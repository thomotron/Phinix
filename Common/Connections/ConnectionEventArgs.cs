using System;

namespace Connections
{
    public class ConnectionEventArgs : EventArgs
    {
        public string ConnectionId;

        public ConnectionEventArgs(string connectionId)
        {
            this.ConnectionId = connectionId;
        }
    }
}