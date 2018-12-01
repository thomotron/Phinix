using System;
using System.IO;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace UserManagement
{
    /// <inheritdoc />
    /// <summary>
    /// Server-side variant of <c>UserManager</c>.
    /// Used to store details of each user and oversees user login.
    /// </summary>
    public class ServerUserManager : UserManager
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <c>NetServer</c> to send packets and bind events to.
        /// </summary>
        private NetServer netServer;

        /// <summary>
        /// <c>ServerAuthenticator</c> to check session validity with.
        /// </summary>
        private ServerAuthenticator authenticator;
        
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        private UserStore userStore;
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        private object userStoreLock = new object();

        /// <summary>
        /// Creates a new <c>ServerUserManager</c> instance.
        /// </summary>
        /// <param name="netServer"><c>NetServer</c> instance to bind packet handlers to</param>
        /// <param name="authenticator"><c>ServerAuthenticator</c> to check session validity with</param>
        public ServerUserManager(NetServer netServer, ServerAuthenticator authenticator)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            
            this.userStore = new UserStore();
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netServer.OnConnectionClosed += connectionClosedHandler;
        }

        /// <summary>
        /// Creates a new <c>ServerUserManager</c> instance and loads in the user store from the given path.
        /// </summary>
        /// <param name="netServer"><c>NetServer</c> instance to bind packet handlers to</param>
        /// <param name="authenticator"><c>ServerAuthenticator</c> to check session validity with</param>
        /// <param name="userStorePath">Path to user store</param>
        public ServerUserManager(NetServer netServer, ServerAuthenticator authenticator, string userStorePath)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            
            Load(userStorePath);
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netServer.OnConnectionClosed += connectionClosedHandler;
        }

        private void connectionClosedHandler(object sender, ConnectionEventArgs e)
        {
            // TODO: Log out user as they disconnect
        }
        
        /// <summary>
        /// Saves the user store at the given path.
        /// This will overwrite the file if it already exists.
        /// </summary>
        /// <param name="filePath">Destination file path</param>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Write the user store
            using (FileStream fs = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (CodedOutputStream cos = new CodedOutputStream(fs))
                {
                    lock (userStoreLock)
                    {
                        userStore.WriteTo(cos);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the user store from the given file path.
        /// Will create a new one if the file does not exist.
        /// </summary>
        /// <param name="filePath">User store file path</param>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public void Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Generate a fresh user store if it doesn't exist
            if (!File.Exists(filePath))
            {
                // Create the user store
                lock (userStoreLock) this.userStore = new UserStore();
                
                // Save it
                Save(filePath);
            }

            // Load the user store
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (CodedInputStream cis = new CodedInputStream(fs))
                {
                    lock (userStoreLock) this.userStore = UserStore.Parser.ParseFrom(cis);
                }
            }
        }

        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket("UserManagement", MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "LoginPacket":
                    // TODO: Handle LoginPackets
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a LoginPacket from {0}", connectionId)));
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
    }
}
