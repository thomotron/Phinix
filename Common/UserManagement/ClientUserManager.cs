using System;
using Connections;
using Utils;

namespace UserManagement
{
    /// <inheritdoc />
    /// <summary>
    /// Client-side variant of <c>UserManager</c>.
    /// Used to store states of other users for easy lookup with other modules.
    /// </summary>
    public class ClientUserManager : UserManager
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Whether the client is logged in to the server.
        /// </summary>
        public bool LoggedIn { get; private set; }
        
        /// <summary>
        /// UUID used by other users to identify the user.
        /// </summary>
        public string Uuid { get; private set; }

        /// <summary>
        /// <c>NetClient</c> to send packets through and bind events to.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// Display name of the user.
        /// </summary>
        private string displayName;

        /// <summary>
        /// Whether the server should update it's copy of the user's display name with the one provided on login.
        /// </summary>
        private bool useServerDisplayName;

        /// <summary>
        /// Session identifier to use when sending packets to the server.
        /// Set through <c>SendLoginPacket</c>.
        /// </summary>
        private string sessionId;
        
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        private UserStore userStore;
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        private object userStoreLock = new object();

        public ClientUserManager(NetClient netClient, string displayName, bool useServerDisplayName = false)
        {
            this.netClient = netClient;
            this.displayName = displayName;
            this.useServerDisplayName = useServerDisplayName;
            
            this.userStore = new UserStore();
        }
    }
}
