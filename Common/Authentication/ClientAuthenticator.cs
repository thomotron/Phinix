using System;
using System.IO;
using System.Security.Cryptography;
using System.Timers;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Client authentication module.
    /// Handles incoming greetings and attempts to authenticate with a server.
    /// </summary>
    public class ClientAuthenticator : Authenticator
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        /// <summary>
        /// Raised on a successful authentication attempt.
        /// </summary>
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationSuccess;
        /// <summary>
        /// Raised on a failed authentication attempt.
        /// </summary>
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationFailure;
        
        /// <summary>
        /// Delegate for requesting credentials.
        /// Used to collect login information when authentication fails or there are no existing credentials for the server.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="serverName">Server name</param>
        /// <param name="serverDescription">Server description</param>
        /// <param name="authType">Authentication type server accepts</param>
        /// <param name="callback">Callback for asynchronicity</param>
        public delegate void GetCredentialsDelegate(
            string sessionId,
            string serverName,
            string serverDescription,
            AuthTypes authType,
            ReturnCredentialsDelegate callback
        );
        /// <summary>
        /// Delegate for consuming returned credentials.
        /// Used as a callback for credentials requests.
        /// </summary>
        /// <param name="credentialsProvided">Whether credentials have been provided or the callback should be ignored</param>
        /// <param name="sessionId">Session ID</param>
        /// <param name="authType">Authentication type</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        public delegate void ReturnCredentialsDelegate(
            bool credentialsProvided,
            string sessionId,
            AuthTypes authType,
            string username,
            string password
        );
        /// <summary>
        /// Delegate called when new credentials are required for authentication.
        /// </summary>
        private GetCredentialsDelegate getCredentials;
        
        /// <summary>
        /// Whether the client has authenticated with the server.
        /// </summary>
        public bool Authenticated { get; private set; }
        
        /// <summary>
        /// Session identifier used by the server to confirm we are allowed to speak to it.
        /// Use only if <see cref="Authenticated"/> is true.
        /// </summary>
        public string SessionId { get; private set; }
        
        /// <summary>
        /// When the session ID will expire.
        /// Used by an internal timer to refresh the session periodically.
        /// </summary>
        private DateTime sessionExpiry;
        /// <summary>
        /// Timer to extend the current session.
        /// </summary>
        private Timer sessionExtendTimer;

        /// <summary>
        /// Path to the credential store file.
        /// </summary>
        public readonly string CredentialStorePath;
        /// <summary>
        /// Stores credentials for each server.
        /// </summary>
        private CredentialStore credentialStore;
        /// <summary>
        /// Lock for credential store operations.
        /// </summary>
        private static readonly object credentialStoreLock = new object();

        /// <summary>
        /// <see cref="NetClient"/> to send packets and bind events to.
        /// </summary>
        private NetClient netClient;
        
        /// <summary>
        /// Creates a new <see cref="ClientAuthenticator"/> bound to the given <see cref="NetClient"/> with credentials stored in <see cref="CredentialStorePath"/>.
        /// The <see cref="getCredentialsDelegate"/> is called when credentials are requested.
        /// </summary>
        /// <param name="netClient"><see cref="NetClient"/> to bind to</param>
        /// <param name="getCredentialsDelegate"><see cref="GetCredentialsDelegate"/> to call when credentials are required from the user</param>
        /// <param name="credentialStorePath">Path to the credential store file</param>
        public ClientAuthenticator(NetClient netClient, GetCredentialsDelegate getCredentialsDelegate, string credentialStorePath = "PhinixCredentials.bin")
        {
            this.netClient = netClient;
            this.getCredentials = getCredentialsDelegate;
            this.CredentialStorePath = credentialStorePath;
            this.credentialStore = getCredentialStore();
            
            // Set up the session extension timer
            sessionExtendTimer = new Timer
            {
                Enabled = false,
                AutoReset = false
            };
            sessionExtendTimer.Elapsed += (sender, args) =>
            {
                // Only attempt to extend the session if we have one
                if (Authenticated) sendExtendSessionPacket(SessionId);
                
                // Stop the timer from firing again
                sessionExtendTimer.Stop();
            };

            netClient.OnDisconnect += disconnectHandler;
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
        }

        /// <summary>
        /// Adds or replaces an existing credential for the given server address.
        /// </summary>
        /// <param name="serverAddress">Server address</param>
        /// <param name="credential">Credential to add</param>
        public void AddOrUpdateCredential(string serverAddress, Credential credential)
        {
            // Prevent other threads from modifying the credential store while we're using it
            lock (credentialStoreLock)
            {
                // Check if the server already has a credential
                if (credentialStore.Credentials.ContainsKey(serverAddress))
                {
                    // Replace it
                    credentialStore.Credentials[serverAddress] = credential;
                }
                else
                {
                    // Add it
                    credentialStore.Credentials.Add(serverAddress, credential);
                }
                
                // Save the credential store to file
                saveCredentialStore(credentialStore);
            }
        }

        /// <summary>
        /// Attempts to get the corresponding credential for the given server address.
        /// Returns true if the credential was found successfully.
        /// </summary>
        /// <param name="serverAddress">Server address</param>
        /// <param name="credential">Credential output</param>
        /// <returns>Credential was retrieved successfully</returns>
        public bool TryGetCredential(string serverAddress, out Credential credential)
        {
            // Initialise the credential to something arbitrary
            credential = null;
            
            // Prevent other threads from modifying the credential store while we're using it
            lock (credentialStoreLock)
            {
                if (credentialStore.Credentials.ContainsKey(serverAddress))
                {
                    // Set the output to the desired credential and return successfully
                    credential = credentialStore.Credentials[serverAddress];
                    return true;
                }
            }
            
            // Couldn't find the credential you're looking for
            return false;
        }
        
        /// <summary>
        /// Returns the existing credential store from disk or a new one if it doesn't exist.
        /// </summary>
        /// <returns>New or existing credential store</returns>
        private CredentialStore getCredentialStore()
        {
            // Prevent other threads from modifying the credential store while we're using it
            lock (credentialStoreLock)
            {
                // Create a new credential store if one doesn't already exist
                if (!File.Exists(CredentialStorePath))
                {
                    // Generate a random PhiKey for this instance
                    CredentialStore newCredentialStore = new CredentialStore
                    {
                        ClientKey = generateClientKey()
                    };
                
                    // Save the store to disk
                    saveCredentialStore(newCredentialStore);
                
                    // Finally return the new store
                    return newCredentialStore;
                }
            
                // Pull the store from disk as a pre-packed Any.
                CredentialStore credentialStore;
                using (FileStream fs = new FileStream(CredentialStorePath, FileMode.Open))
                {
                    using (CodedInputStream cis = new CodedInputStream(fs))
                    {
                        credentialStore = CredentialStore.Parser.ParseFrom(cis);
                    }
                }
                
                // Return the credential store
                return credentialStore;
            }
        }

        /// <summary>
        /// Saves the given credential store to disk, overwriting an existing one.
        /// </summary>
        /// <param name="credentialStore">Credential store to save</param>
        private void saveCredentialStore(CredentialStore credentialStore)
        {
            // Prevent other threads from modifying the credential store while we're using it
            lock (credentialStoreLock)
            {
                // Create or truncate the credentials file
                using (FileStream fs = File.Open(CredentialStorePath, FileMode.Create, FileAccess.Write))
                {
                    using (CodedOutputStream cos = new CodedOutputStream(fs))
                    {
                        // Write the credential store to disk.
                        credentialStore.WriteTo(cos);
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a a random 512-bit client key encoded in Base64.
        /// </summary>
        /// <returns>Random Base64-encoded 512-bit client key</returns>
        private static string generateClientKey()
        {
            byte[] randomBytes = new byte[64];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(randomBytes);

            return Convert.ToBase64String(randomBytes);
        }

        /// <inheritdoc />
        protected override void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientAuthenticator).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;
            
            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "HelloPacket":
                    RaiseLogEntry(new LogEventArgs("Got a HelloPacket", LogLevel.DEBUG));
                    helloPacketHandler(connectionId, message.Unpack<HelloPacket>());
                    break;
                case "AuthResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got an AuthResponsePacket", LogLevel.DEBUG));
                    authResponsePacketHandler(connectionId, message.Unpack<AuthResponsePacket>());
                    break;
                case "ExtendSessionResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got an ExtendSessionResponsePacket", LogLevel.DEBUG));
                    extendSessionResponsePacketHandler(connectionId, message.Unpack<ExtendSessionResponsePacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
        
        /// <summary>
        /// Handler for incoming <see cref="HelloPacket"/>s.
        /// Responds with an <see cref="AuthenticatePacket"/>, requesting new credentials if necessary.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="HelloPacket"/></param>
        private void helloPacketHandler(string connectionId, HelloPacket packet)
        {
            // Nullify authenticated state
            // TODO: Reset authenticated state on disconnect
            Authenticated = false;
            SessionId = null;
            
            // Try to get an existing credential for this server and ensure it corresponds to the server's accepted authentication type
            // TODO: Use something better than server name that is unique to each (username problems all over again)
            if (!TryGetCredential(packet.ServerName, out Credential credential) || credential.AuthType != packet.AuthType)
            {
                // Exception for client key authentication, it should be a 'zero-configuration' solution
                if (packet.AuthType == AuthTypes.ClientKey)
                {
                    lock (credentialStore)
                    {
                        credential = new Credential
                        {
                            AuthType = AuthTypes.ClientKey,
                            Username = credentialStore.ClientKey,
                            Password = null
                        };
                    }
                }
                else
                {
                    // Request for credentials
                    getCredentials(
                        packet.SessionId,
                        packet.ServerName,
                        packet.ServerDescription,
                        packet.AuthType,
                        (credentialsProvided, sessionId, authType, username, password) =>
                        {
                            if (credentialsProvided)
                            {
                                // Create a new credential and save it
                                credential = new Credential
                                {
                                    AuthType = authType,
                                    Username = username,
                                    Password = password
                                };
                                AddOrUpdateCredential(packet.ServerName, credential);

                                // Send an AuthenticatePacket as a response
                                sendAuthenticatePacket(sessionId, credential);
                            }
                            else
                            {
                                // Close the connection
                                netClient.Disconnect();
                            }
                        }
                    );
                    
                    // Stop here as we don't have any credentials to send yet.
                    // The connection can remain open so that the callback can send credentials when it gets them.
                    return;
                }
            }
            
            // Send an AuthenticatePacket as a response
            sendAuthenticatePacket(packet.SessionId, credential);
        }

        /// <summary>
        /// Creates and sends an <see cref="AuthenticatePacket"/>.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="credential">Credentials</param>
        private void sendAuthenticatePacket(string sessionId, Credential credential)
        {
            RaiseLogEntry(new LogEventArgs("Sending AuthenticatePacket", LogLevel.DEBUG));
            
            // Create and populate an authentication packet
            AuthenticatePacket authPacket = new AuthenticatePacket
            {
                AuthType = credential.AuthType,
                SessionId = sessionId,
                Username = credential.Username,
                Password = credential.Password
            };

            // Pack it into an Any for transmission
            Any packedAuthPacket = ProtobufPacketHelper.Pack(authPacket);
            
            // Send it on its way
            netClient.Send(MODULE_NAME, packedAuthPacket.ToByteArray());
        }
        
        private void authResponsePacketHandler(string connectionId, AuthResponsePacket packet)
        {
            // Was authentication successful?
            if (packet.Success)
            {
                // Set authenticated state, session ID, and expiry
                Authenticated = true;
                SessionId = packet.SessionId;
                sessionExpiry = packet.Expiry.ToDateTime();
                
                // Reset the session extension timer for halfway between now and the expiry
                sessionExtendTimer.Stop();
                sessionExtendTimer.Interval = (sessionExpiry - DateTime.UtcNow).TotalMilliseconds / 2;
                sessionExtendTimer.Start();
                
                // Raise successful auth event
                OnAuthenticationSuccess?.Invoke(this, new AuthenticationEventArgs());
            }
            else
            {
                // Set authenticated state
                Authenticated = false;
                
                // Raise failed auth event
                OnAuthenticationFailure?.Invoke(this, new AuthenticationEventArgs(packet.FailureReason, packet.FailureMessage));
            }
        }

        /// <summary>
        /// Sends an <see cref="ExtendSessionPacket"/> to the server to extend the current session.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        private void sendExtendSessionPacket(string sessionId)
        {
            RaiseLogEntry(new LogEventArgs("Sending ExtendSessionPacket", LogLevel.DEBUG));
            
            // Create and pack a session extension packet
            ExtendSessionPacket packet = new ExtendSessionPacket
            {
                SessionId = sessionId
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }
        
        /// <summary>
        /// Handles incoming <see cref="ExtendSessionResponsePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="ExtendSessionResponsePacket"/></param>
        private void extendSessionResponsePacketHandler(string connectionId, ExtendSessionResponsePacket packet)
        {
            if (packet.Success)
            {
                // Update the expiry with the newly-extended one
                sessionExpiry = packet.NewExpiry.ToDateTime();
                
                // Reset the session extension timer for halfway between now and the expiry
                sessionExtendTimer.Stop();
                sessionExtendTimer.Interval = (sessionExpiry - DateTime.UtcNow).TotalMilliseconds / 2;
                sessionExtendTimer.Start();
            }
        }
        
        /// <summary>
        /// Handles the OnDisconnect event from <see cref="NetClient"/> and invalidates any connection-specific fields.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void disconnectHandler(object sender, EventArgs e)
        {
            Authenticated = false;
            SessionId = null;
        }
    }
}
