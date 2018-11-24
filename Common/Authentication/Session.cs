using System;

namespace Authentication
{
    /// <summary>
    /// A session from the server's perspective.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// The session ID.
        /// </summary>
        public string SessionId;

        /// <summary>
        /// Connection ID of the client.
        /// </summary>
        public string ConnectionId;

        /// <summary>
        /// Time after which the session will be considered stale and be marked for removal.
        /// </summary>
        public DateTime Expiry;

        /// <summary>
        /// Username of the user this session is assigned to.
        /// Set after successful authentication.
        /// </summary>
        public string Username;

        /// <summary>
        /// UUID of  the user this session is assigned to.
        /// Set after successful login.
        /// </summary>
        public string Uuid;
    }
}