using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
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
        /// Raised on a successful login attempt.
        /// </summary>
        public event EventHandler<LoginEventArgs> OnLoginSuccess;
        /// <summary>
        /// Raised on a failed login attempt.
        /// </summary>
        public event EventHandler<LoginEventArgs> OnLoginFailure;

        /// <summary>
        /// Raised on a user update.
        /// </summary>
        public event EventHandler<UserChangedEventArgs> OnUserChanged; 

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
        /// <c>ClientAuthenticator</c> to retrieve session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;
        
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        protected override UserStore userStore { get; set; }
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        protected override object userStoreLock => new object();

        public ClientUserManager(NetClient netClient, ClientAuthenticator authenticator)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            
            this.userStore = new UserStore();
            
            netClient.OnDisconnect += disconnectHandler;
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
        }

        /// <summary>
        /// Attempts to log in to the server.
        /// Must be authenticated.
        /// </summary>
        /// <param name="displayName">Display name to log in with</param>
        /// <param name="useServerDisplayName">Use the server's copy of the user's display name if it has one</param>
        /// <param name="acceptingTrades">Whether to accept trades from other users</param>
        public void SendLogin(string displayName, bool useServerDisplayName = false, bool acceptingTrades = true)
        {
            if (!authenticator.Authenticated) return;
            
            // Create and pack a new LoginPacket
            LoginPacket packet = new LoginPacket
            {
                SessionId = authenticator.SessionId,
                DisplayName = displayName,
                UseServerDisplayName = useServerDisplayName,
                AcceptingTrades = acceptingTrades
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            RaiseLogEntry(new LogEventArgs("Sending LoginPacket", LogLevel.DEBUG));
            
            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Returns an array of all (optionally only logged in) user UUIDs.
        /// </summary>
        /// <param name="loggedIn">Only return logged in user UUIDs</param>
        /// <returns>All user UUIDs</returns>
        public string[] GetUuids(bool loggedIn = false)
        {
            lock (userStoreLock)
            {
                if (loggedIn)
                {
                    return userStore.Users.Values.Where(u => u.LoggedIn).Select(u => u.Uuid).ToArray();
                }
                else
                {
                    return userStore.Users.Values.Select(u => u.Uuid).ToArray();
                }
            }
        }

        /// <summary>
        /// Updates the currently-logged in user locally and on the server.
        /// Returns whether the update was successful locally and was sent to server.
        /// </summary>
        /// <param name="displayName">New display name</param>
        /// <param name="acceptingTrades">Whether to accept trades from other users</param>
        /// <returns>User update was successful and sent to server</returns>
        public bool UpdateSelf(string displayName = null, bool? acceptingTrades = null)
        {
            // Don't do anything unless we are logged in
            if (!LoggedIn) return false;
            
            // Don't do anything if the parameters are all null
            if (displayName == null && acceptingTrades == null) return false;

            lock (userStoreLock)
            {
                // Make sure we are in the user store
                if (!userStore.Users.ContainsKey(Uuid)) return false; // This should never return as the server ensures you exist on login

                // Clone the user to avoid editing properties by reference
                User user = userStore.Users[Uuid].Clone();

                // Set the users display name if it is present
                if (displayName != null) user.DisplayName = displayName;

                // Set the users trade acceptance if it is present
                if (acceptingTrades.HasValue) user.AcceptingTrades = acceptingTrades.Value;
                
                // Create and pack a user update packet
                UserUpdatePacket packet = new UserUpdatePacket
                {
                    SessionId = authenticator.SessionId,
                    Uuid = Uuid,
                    User = user
                };
                Any packedPacket = ProtobufPacketHelper.Pack(packet);
                
                // Send it on its way
                netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
            }

            return true;
        }
        
        /// <summary>
        /// Handles incoming packets.
        /// </summary>
        /// <param name="module">Destination module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the packet and discard it if it fails
            if (!ProtobufPacketHelper.ValidatePacket("UserManagement", MODULE_NAME, module, data, out Any message,out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "UserUpdatePacket":
                    RaiseLogEntry(new LogEventArgs("Got a UserUpdatePacket", LogLevel.DEBUG));
                    userUpdatePacketHandler(connectionId, message.Unpack<UserUpdatePacket>());
                    break;
                case "UserSyncPacket":
                    RaiseLogEntry(new LogEventArgs("Got a UserSyncPacket", LogLevel.DEBUG));
                    userSyncPacketHandler(connectionId, message.Unpack<UserSyncPacket>());
                    break;
                case "LoginResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a LoginResponsePacket", LogLevel.DEBUG));
                    loginResponsePacketHandler(connectionId, message.Unpack<LoginResponsePacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>LoginResponsePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>LoginResponsePacket</c></param>
        private void loginResponsePacketHandler(string connectionId, LoginResponsePacket packet)
        {
            if (packet.Success)
            {
                // Set module states
                LoggedIn = true;
                Uuid = packet.Uuid;
                
                // Raise login success event
                OnLoginSuccess?.Invoke(this, new LoginEventArgs());
            }
            else
            {
                // Set module states
                LoggedIn = false;
                Uuid = null;
                
                // Raise login failure event
                OnLoginFailure?.Invoke(this, new LoginEventArgs(packet.FailureReason, packet.FailureMessage));
            }
        }

        /// <summary>
        /// Handles incoming <c>UserUpdatePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>UserUpdatePacket</c></param>
        private void userUpdatePacketHandler(string connectionId, UserUpdatePacket packet)
        {
            User user = packet.User;
            
            lock (userStoreLock)
            {
                if (userStore.Users.ContainsKey(user.Uuid))
                {
                    // Update/replace the user
                    userStore.Users[user.Uuid] = user;
                }
                else
                {
                    // Add the user
                    userStore.Users.Add(user.Uuid, user);
                }
            }

            OnUserChanged?.Invoke(this, new UserChangedEventArgs(user.Uuid, user.LoggedIn, user.DisplayName));
        }

        /// <summary>
        /// Handles incoming <c>UserSyncPacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>UserSyncPacket</c></param>
        private void userSyncPacketHandler(string connectionId, UserSyncPacket packet)
        {
            lock (userStoreLock)
            {
                foreach (User user in packet.Users)
                {
                    if (userStore.Users.ContainsKey(user.Uuid))
                    {
                        // Update/replace the user
                        userStore.Users[user.Uuid] = user;
                    }
                    else
                    {
                        // Add the user
                        userStore.Users.Add(user.Uuid, user);
                    }
                }
            }
        }
        
        /// <summary>
        /// Handles the OnDisconnect event from <c>NetClient</c> and invalidates any connection-specific fields.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void disconnectHandler(object sender, EventArgs e)
        {
            LoggedIn = false;
            Uuid = null;
        }
    }
}
