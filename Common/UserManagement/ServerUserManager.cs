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
                // Update the user's display name on the server with the one they've provided
                UpdateUser(uuid, packet.DisplayName);
            }
            
            // Log the event
            RaiseLogEntry(new LogEventArgs(string.Format("User {0} successfully logged in as \"{1}\" (SessionID: {2}, UUID: {3})", username, displayName, packet.SessionId, uuid)));
            
            // Send a successful login response
            sendSuccessfulLoginResponsePacket(connectionId, uuid, displayName);
        }

        /// <summary>
        /// Sends a successful <c>LoginResponsePacket</c> with the given UUID and display name.
        /// </summary>
        /// <param name="connectionId">Connection ID</param>
        /// <param name="uuid">User's UUID</param>
        /// <param name="displayName">User's display name</param>
        private void sendSuccessfulLoginResponsePacket(string connectionId, string uuid, string displayName)
        {
            // Create and pack a response
            LoginResponsePacket response = new LoginResponsePacket
            {
                Success = true,
                Uuid = uuid,
                DisplayName = displayName
            };
            Any packedResponse = Any.Pack(response, "Phinix");
            
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
            // Create and pack a response
            LoginResponsePacket response = new LoginResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            Any packedResponse = Any.Pack(response, "Phinix");
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedResponse.ToByteArray());
        }
    }
}
