using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Collection of connected UUIDs organised by connection ID.
        /// </summary>
        private Dictionary<string, string> connectedUsers;
        /// <summary>
        /// Lock for connected user dictionary operations.
        /// </summary>
        private object connectedUsersLock = new object();
        
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        protected override UserStore userStore { get; set; }
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        protected override object userStoreLock => new object();

        /// <summary>
        /// Creates a new <c>ServerUserManager</c> instance.
        /// </summary>
        /// <param name="netServer"><c>NetServer</c> instance to bind packet handlers to</param>
        /// <param name="authenticator"><c>ServerAuthenticator</c> to check session validity with</param>
        public ServerUserManager(NetServer netServer, ServerAuthenticator authenticator)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            
            this.connectedUsers = new Dictionary<string, string>();
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
            
            this.connectedUsers = new Dictionary<string, string>();
            Load(userStorePath);
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netServer.OnConnectionClosed += connectionClosedHandler;
        }

        private void connectionClosedHandler(object sender, ConnectionEventArgs e)
        {
            lock (connectedUsersLock)
            {
                // Try to log them out
                if (connectedUsers.ContainsKey(e.ConnectionId))
                {
                    TryLogOut(connectedUsers[e.ConnectionId]);
                }

                // Drop them from the connected user dictionary
                connectedUsers.Remove(e.ConnectionId);
            }
        }
        
        /// <summary>
        /// Attempts to log in a user with the given UUID.
        /// Returns whether the user was successfully logged in.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>Whether the user was successfully logged in</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public override bool TryLogIn(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                userStore.Users[uuid].LoggedIn = true;
                
                broadcastUserUpdate(userStore.Users[uuid]);
            }
            
            return true;
        }
        
        /// <summary>
        /// Attempts to log out a user with the given UUID.
        /// Returns whether the user was successfully logged out.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>Whether the user was successfully logged out</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public override bool TryLogOut(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                userStore.Users[uuid].LoggedIn = false;
                
                broadcastUserUpdate(userStore.Users[uuid]);
            }
            
            return true;
        }

        /// <inheritdoc />
        /// <summary>
        /// Updates an existing user's properties and broadcasts the update to all connected users.
        /// Returns true if the update was successful, otherwise false.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="displayName">Display name of the user</param>
        /// <returns>User updated successfully</returns>
        public override bool UpdateUser(string uuid, string displayName = null)
        {
            if (!base.UpdateUser(uuid, displayName)) return false;

            User user;
            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                user = userStore.Users[uuid];
            }
            
            broadcastUserUpdate(user);

            return true;
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
        
        /// <summary>
        /// Checks whether the user with the given UUID is logged in from the given connection ID.
        /// </summary>
        /// <param name="connectionId">User's connection ID</param>
        /// <param name="uuid">User's UUID</param>
        /// <returns>User with the given UUID is logged in from the given connection ID</returns>
        public bool IsLoggedIn(string connectionId, string uuid)
        {
            if (string.IsNullOrEmpty(connectionId)) throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty", nameof(uuid));

            lock (connectedUsersLock)
            {
                if (!connectedUsers.ContainsKey(connectionId)) return false;

                return connectedUsers[connectionId].Equals(uuid);
            }
        }

        /// <summary>
        /// Returns an array of connection IDs that are currently logged in.
        /// </summary>
        /// <returns></returns>
        public string[] GetConnections()
        {
            lock (connectedUsersLock) return connectedUsers.Keys.ToArray();
        }
        
        /// <summary>
        /// Logs out all users.
        /// Used when shutting down.
        /// </summary>
        public void LogOutAll()
        {
            lock (userStoreLock)
            {
                // Try to log out each user in the user store, regardless of login state
                foreach (string uuid in userStore.Users.Keys)
                {
                    TryLogOut(uuid);
                }
            }
        }

        /// <summary>
        /// Handles incoming packets.
        /// </summary>
        /// <param name="module">Destination module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket("UserManagement", MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "LoginPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a LoginPacket from {0}", connectionId)));
                    handleLoginPacket(connectionId, message.Unpack<LoginPacket>());
                    break;
                case "UserUpdatePacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a UserUpdatePacket from {0}", connectionId)));
                    handleUserUpdatePacket(connectionId, message.Unpack<UserUpdatePacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>LoginPacket</c>s, attempting to log in users.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        /// <exception cref="UserNoLongerExistsException">User no longer exists for some reason even though we just checked like a second ago</exception>
        private void handleLoginPacket(string connectionId, LoginPacket packet)
        {
            // Make sure the client is authenticated
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId))
            {
                RaiseLogEntry(new LogEventArgs(string.Format("Failed login attempt for session {0}: Invalid session", packet.SessionId)));
                
                // Fail the login attempt due to an invalid session
                sendFailedLoginResponsePacket(connectionId, LoginFailureReason.SessionId, "Session is not valid, it may have expired. Try reconnecting.");
                
                // Stop here
                return;
            }

            // Try get the username from their session
            if (!authenticator.TryGetUsername(connectionId, packet.SessionId, out string username))
            {
                RaiseLogEntry(new LogEventArgs(string.Format("Failed login attempt for session {0}: Couldn't get username", packet.SessionId)));
                
                // Fail the login attempt due to an invalid session/username
                sendFailedLoginResponsePacket(connectionId, LoginFailureReason.InternalServerError, "Server failed to get the username associated with the session. Try reconnecting.");
                
                // Stop here
                return;
            }
            
            // Passed authentication checks, time to accept the login attempt
            
            // Try to get the user by their username
            if (TryGetUserUuid(username, out string uuid))
            {
                // Try to log them in
                if (!TryLogIn(uuid))
                {
                    // This shouldn't happen because we just got their UUID but ok.
                    throw new UserNoLongerExistsException(uuid);
                }
            }
            else
            {
                // Otherwise create a new user
                uuid = CreateUser(username, packet.DisplayName, true);
            }
                    
            // Check if they want to use the display name stored on the server
            string displayName = packet.DisplayName;
            if (packet.UseServerDisplayName)
            {
                // Try to get the display name stored server-side
                if (!TryGetDisplayName(uuid, out displayName))
                {
                    // This should never happen because we just made sure that the user existed no more than 30 lines ago, but just in case...
                    throw new UserNoLongerExistsException(uuid);
                }
            }
            else
            {
                // Check the length of their username without markup
                if (TextHelper.StripRichText(packet.DisplayName).Length > 100)
                {
                    // Fail the login attempt due to display name length
                    sendFailedLoginResponsePacket(connectionId, LoginFailureReason.DisplayName, "Display name is too long.");
                    
                    // Stop here
                    return;
                }
                
                // Update the user's display name on the server with the one they've provided
                UpdateUser(uuid, TextHelper.SanitiseRichText(packet.DisplayName));
            }
            
            // Add their UUID/Session ID pair to connectedUsers
            lock (connectedUsersLock)
            {
                // Remove an existing session, if it exists
                connectedUsers.Remove(connectionId);
                
                // Add this session with the freshly-logged in UUID
                connectedUsers.Add(connectionId, uuid);
            }
            
            // Log the event
            RaiseLogEntry(new LogEventArgs(string.Format("User {0} successfully logged in as \"{1}\" (SessionID: {2}, UUID: {3})", username, displayName, packet.SessionId, uuid)));
            
            // Send a successful login response
            sendSuccessfulLoginResponsePacket(connectionId, uuid, displayName);
            
            // Send a sync packet with the current user list
            sendSyncPacket(connectionId);
        }

        /// <summary>
        /// Sends a successful <c>LoginResponsePacket</c> with the given UUID and display name.
        /// </summary>
        /// <param name="connectionId">Connection ID</param>
        /// <param name="uuid">User's UUID</param>
        /// <param name="displayName">User's display name</param>
        private void sendSuccessfulLoginResponsePacket(string connectionId, string uuid, string displayName)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful LoginResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Create and pack a response
            LoginResponsePacket response = new LoginResponsePacket
            {
                Success = true,
                Uuid = uuid,
                DisplayName = displayName
            };
            Any packedResponse = ProtobufPacketHelper.Pack(response);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedResponse.ToByteArray());
        }

        /// <summary>
        /// Sends a failed <c>LoginResponsePacket</c> with the given reason and message.
        /// </summary>
        /// <param name="connectionId">Connection ID</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="failureMessage">Failure message</param>
        private void sendFailedLoginResponsePacket(string connectionId, LoginFailureReason failureReason, string failureMessage)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed LoginResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Create and pack a response
            LoginResponsePacket response = new LoginResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            Any packedResponse = ProtobufPacketHelper.Pack(response);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedResponse.ToByteArray());
        }

        /// <summary>
        /// Packs and sends the given user to all currently-logged in users.
        /// Used when a user's state changes.
        /// </summary>
        /// <param name="user">User to broadcast</param>
        private void broadcastUserUpdate(User user)
        {
            // Create and pack the user update packet
            UserUpdatePacket packet = new UserUpdatePacket
            {
                User = user
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            lock (connectedUsersLock)
            {
                // Send it to every logged-in user
                foreach (string connectionId in connectedUsers.Keys)
                {
                    try
                    {
                        netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
                    }
                    catch (NotConnectedException) {}
                }
            }
        }

        /// <summary>
        /// Sends a <c>UserSyncPacket</c> containing all users to the given connection.
        /// Usernames are stripped from users for security.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        private void sendSyncPacket(string connectionId)
        {
            // Create a blank sync packet
            UserSyncPacket packet = new UserSyncPacket();

            lock (userStoreLock)
            {
                // Add users to the sync packet
                foreach (User userRef in userStore.Users.Values)
                {
                    // Get a non-reference copy of the user so we can blank out the username without affecting the original
                    User user = userRef.Clone();
                    
                    user.Username = "";
                    packet.Users.Add(user);
                }
            }

            // Pack it
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }
        
        /// <summary>
        /// Handles incoming <c>UserUpdatePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void handleUserUpdatePacket(string connectionId, UserUpdatePacket packet)
        {
            // Discard packets from non-authenticated sessions
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;

            // Discard packets from non-logged in sessions
            if (!IsLoggedIn(connectionId, packet.Uuid)) return;

            User user = packet.User;

            lock (connectedUsersLock)
            {
                // Discard packets trying to update users other than themselves
                if (user.Uuid != connectedUsers[connectionId]) return;
            }

            // Update the user
            UpdateUser(user.Uuid, user.DisplayName);
        }
    }
}
