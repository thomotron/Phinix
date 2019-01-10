using System;

namespace UserManagement
{
    public class ServerLoginEventArgs : EventArgs
    {
        /// <summary>
        /// Connection ID of the user.
        /// </summary>
        public string ConnectionId;
        
        /// <summary>
        /// UUID of the user.
        /// </summary>
        public string Uuid;

        public ServerLoginEventArgs(string connectionId, string uuid)
        {
            this.ConnectionId = connectionId;
            this.Uuid = uuid;
        }
    }
}