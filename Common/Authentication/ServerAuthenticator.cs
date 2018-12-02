using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Server authentication module.
    /// Handles incoming authentication attempts and greets new connections.
    /// </summary>
    public class ServerAuthenticator : Authenticator
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
        /// Name of the server displayed to connecting clients.
        /// </summary>
        private string serverName;
        /// <summary>
        /// Description of the server displayed to connecting clients.
        /// </summary>
        private string serverDescription;
        /// <summary>
        /// Accepted authentication method for incoming connection attempts.
        /// </summary>
        private AuthTypes authType;

        /// <summary>
        /// Sessions organised by their connection ID.
        /// </summary>
        private Dictionary<string, Session> sessions;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>sessions</c>.
        /// </summary>
        private object sessionsLock = new object();
        /// <summary>
        /// Timer for clearing out old or invalidated sessions.
        /// </summary>
        private Timer sessionCleanupTimer;

        /// <summary>
        /// Path to the credential store file.
        /// </summary>
        private string credentialStorePath;
        /// <summary>
        /// Stores credentials for each client.
        /// </summary>
        private CredentialStore credentialStore;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>credentialStore</c>.
        /// </summary>
        private object credentialStoreLock = new object();
        
        public ServerAuthenticator(NetServer netServer, string serverName, string serverDescription, AuthTypes authType, string credentialStorePath)
        {
            this.netServer = netServer;
            this.serverName = serverName;
            this.serverDescription = serverDescription;
            this.authType = authType;
            this.credentialStorePath = credentialStorePath;

            // Prevent other threads from modifying the credential store while it is read in
            lock (credentialStoreLock) this.credentialStore = getCredentialStore();

            this.sessions = new Dictionary<string, Session>();
            this.sessionCleanupTimer = new Timer
            {
                Interval = 10000.00, // 10 seconds
                AutoReset = true
            };
            this.sessionCleanupTimer.Elapsed += onSessionCleanup;
            this.sessionCleanupTimer.Start();
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netServer.OnConnectionEstablished += ConnectionEstablishedHandler;
            netServer.OnConnectionClosed += ConnectionClosedHandler;
        }

        /// <summary>
        /// Checks whether the given session ID is valid and has authenticated successfully
        /// </summary>
        /// <param name="connectionId">Connection ID</param>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Session is valid and authenticated</returns>
        public bool IsAuthenticated(string connectionId, string sessionId)
        {
            lock (sessionsLock)
            {
                // Make sure the connection has a session
                if (!sessions.ContainsKey(connectionId)) return false;
                
                Session session = sessions[connectionId];
                
                // Return whether the session ID matches, is not expired, and is authenticated
                return session.SessionId == sessionId &&
                       session.Expiry.CompareTo(DateTime.UtcNow) > 0 &&
                       sessions[connectionId].Authenticated;
            }
        }

        /// <summary>
        /// Attempts to get the username for the given connection and session.
        /// Returns whether the username was retrieved successfully.
        /// </summary>
        /// <param name="connectionId">Connection ID</param>
        /// <param name="sessionId">Session ID</param>
        /// <param name="username">Username output</param>
        /// <returns>Username retrieved successfully</returns>
        public bool TryGetUsername(string connectionId, string sessionId, out string username)
        {
            // Initialise username to something arbitrary
            username = null;

            lock (sessionsLock)
            {
                // Make sure the connection has a session
                if (!sessions.ContainsKey(connectionId)) return false;

                Session session = sessions[connectionId];

                // Session ID should match
                if (session.SessionId != sessionId) return false;

                // Session shouldn't have a username if it hasn't been authenticated
                if (!session.Authenticated) return false;

                // Set the output
                username = session.Username;

                return true;
            }
        }

        /// <summary>
        /// Collects all (optionally authenticated) sessions and returns the session ID for each as a string array.
        /// </summary>
        /// <param name="mustBeAuthenticated">Only collect authenticated sessions</param>
        /// <returns>Array of session IDs</returns>
        public string[] GetSessions(bool mustBeAuthenticated = false)
        {
            lock (sessionsLock)
            {
                if (mustBeAuthenticated)
                {
                    // Add only session IDs that are authenticated
                    return sessions.Values.Where(session => session.Authenticated).Select(session => session.SessionId).ToArray();
                }
                else
                {
                    // Add all session IDs
                    return sessions.Values.Select(session => session.SessionId).ToArray();
                }
            }
        }
        
        /// <summary>
        /// Collects all (optionally authenticated) connections and returns the connection ID for each as a string array.
        /// </summary>
        /// <param name="mustBeAuthenticated">Only collect authenticated sessions</param>
        /// <returns>Array of connection IDs</returns>
        public string[] GetConnections(bool mustBeAuthenticated = false)
        {
            lock (sessionsLock)
            {
                if (mustBeAuthenticated)
                {
                    // Add only session IDs that are authenticated
                    return sessions.Values.Where(session => session.Authenticated).Select(session => session.ConnectionId).ToArray();
                }
                else
                {
                    // Add all session IDs
                    return sessions.Values.Select(session => session.ConnectionId).ToArray();
                }
            }
        }

        private void ConnectionEstablishedHandler(object sender, ConnectionEventArgs e)
        {
            RaiseLogEntry(new LogEventArgs("Sending HelloPacket to incoming connection " + e.ConnectionId, LogLevel.DEBUG));
            
            // Create a new session for this connection
            Session session = new Session
            {
                SessionId = Guid.NewGuid().ToString(),
                ConnectionId = e.ConnectionId,
                Expiry = DateTime.UtcNow + TimeSpan.FromMinutes(5),
                Authenticated = false
            };
            
            lock (sessionsLock)
            {
                // Remove any existing sessions for this connection
                if (sessions.ContainsKey(e.ConnectionId)) sessions.Remove(e.ConnectionId);
                
                // Add it to the session dictionary
                sessions.Add(e.ConnectionId, session);
            }
            
            // Construct a HelloPacket
            HelloPacket hello = new HelloPacket
            {
                AuthType = authType,
                ServerName = serverName,
                ServerDescription = serverDescription,
                SessionId = session.SessionId
            };
            
            // Pack it into an Any message
            Any packedHello = Any.Pack(hello, "Phinix");
            
            // Send it
            netServer.Send(e.ConnectionId, MODULE_NAME, packedHello.ToByteArray());
        }

        private void ConnectionClosedHandler(object sender, ConnectionEventArgs e)
        {
            RaiseLogEntry(new LogEventArgs("Closing connection from " + e.ConnectionId, LogLevel.DEBUG));

            lock (sessionsLock)
            {
                // Check if a session exists for the connection
                if (sessions.ContainsKey(e.ConnectionId))
                {
                    Session session = sessions[e.ConnectionId];
                    
                    // Remove the session
                    sessions.Remove(session.SessionId);
                }
            }
        }

        /// <inheritdoc />
        protected override void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket("Authentication", MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;
            
            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    AuthenticatePacket packet = message.Unpack<AuthenticatePacket>();
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an AuthenticatePacket for session {0}", packet.SessionId), LogLevel.DEBUG));
                    authenticatePacketHandler(connectionId, packet);
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>AuthenticatePacket</c>s from clients trying to authenticate.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void authenticatePacketHandler(string connectionId, AuthenticatePacket packet)
        {
            // Check the supplied credentials are of the appropriate type
            if (packet.AuthType != authType)
            {
                // Fail the authentication attempt due to mismatched credential type
                sendFailedAuthResponsePacket(connectionId, AuthFailureReason.AuthType, string.Format("Wrong type of credentials supplied. The server only accepts \"{0}\" credentials.", authType.ToString()));
                
                RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0}: Wrong credential type", connectionId), LogLevel.DEBUG));
                
                // Stop here
                return;
            }
            
            lock (sessionsLock)
            {
                // Check whether a session exists for this connection or the supplied session ID is assigned to this connection
                if (!sessions.ContainsKey(connectionId) || sessions[connectionId].SessionId != packet.SessionId)
                {
                    // Fail the authentication attempt due to invalid session ID
                    sendFailedAuthResponsePacket(connectionId, AuthFailureReason.SessionId, "Could not find session for your connection. It may have expired. Try logging in again.");
                    
                    RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0}: No session found for this connection", connectionId), LogLevel.DEBUG));

                    // Stop here
                    return;
                }

                // Get an alias of the session for simplicity
                Session session = sessions[connectionId];

                // Check if the session has expired
                if (session.Expiry.CompareTo(DateTime.UtcNow) <= 0)
                {
                    // Fail the authentication attempt due to expired session
                    sendFailedAuthResponsePacket(connectionId, AuthFailureReason.SessionId, "Session has expired. Try logging in again.");
                    
                    RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Session expired", connectionId, session.SessionId), LogLevel.DEBUG));
                    
                    // Stop here
                    return;
                }

                lock (credentialStoreLock)
                {
                    // Check if user does not have an existing credential
                    if (!credentialStore.Credentials.ContainsKey(packet.Username))
                    {
                        // Exception for PhiKey, it should be a 'zero-configuration' solution
                        if (authType == AuthTypes.PhiKey)
                        {
                            // Create a new credential and add it to the credential store
                            Credential newCredential = new Credential
                            {
                                AuthType = AuthTypes.PhiKey,
                                Username = packet.Username,
                                Password = packet.Password
                            };
                            credentialStore.Credentials.Add(newCredential.Username, newCredential);
                        }
                        else
                        {
                            // Fail the authentication attempt due to missing credential
                            sendFailedAuthResponsePacket(connectionId, AuthFailureReason.Credentials, string.Format("No credential found for the username \"{0}\".", packet.Username));
                            
                            RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): No credentials found for {2}", connectionId, session.SessionId, packet.Username), LogLevel.DEBUG));
                    
                            // Stop here
                            return;
                        }
                    }

                    // Add username to the session
                    session.Username = packet.Username;

                    // Get an alias of the user's credential for simplicity
                    Credential credential = credentialStore.Credentials[session.Username];

                    // Check if the credential is not for our current auth type
                    if (credential.AuthType != authType)
                    {
                        // Delete the offending credential
                        credentialStore.Credentials.Remove(session.Username);
                        
                        // Fail the authentication attempt due to invalid stored credential
                        sendFailedAuthResponsePacket(connectionId, AuthFailureReason.InternalServerError, "Server's stored credential did not match its accepted authentication type. Try logging in again.");
                        
                        RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Stored credentials for {2} are of wrong type ({3})", connectionId, session.SessionId, packet.Username, credential.AuthType.ToString()), LogLevel.DEBUG));
                        
                        // Stop here
                        return;
                    }
                    
                    // Check if the password does not match the stored credential
                    if (packet.Password != credential.Password)
                    {
                        // Fail the authentication attempt due to mismatching password
                        sendFailedAuthResponsePacket(connectionId, AuthFailureReason.Credentials, "Invalid password provided.");
                        
                        RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Wrong password provided for {2}", connectionId, session.SessionId, packet.Username), LogLevel.DEBUG));
                        
                        // Stop here
                        return;
                    }

                    // Auth attempt made it through the gauntlet, time to accept it as a valid request
                    
                    // Mark the session as authenticated
                    session.Authenticated = true;

                    // Extend their session by 30 minutes
                    session.Expiry = DateTime.UtcNow + TimeSpan.FromMinutes(30);
                    
                    // Approve the authentication attempt
                    sendSuccessfulAuthResponsePacket(connectionId, session.SessionId);
                    
                    // Log this momentous occasion
                    RaiseLogEntry(new LogEventArgs(string.Format("User \"{0}\" (ConnID: {1}, SessID: {2}) successfully authenticated", session.Username, session.ConnectionId, session.SessionId)));
                }
            }
        }

        private void sendSuccessfulAuthResponsePacket(string connectionId, string sessionId)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful AuthResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Construct an AuthResponsePacket in success configuration
            AuthResponsePacket response = new AuthResponsePacket
            {
                Success = true,
                SessionId = sessionId
            };
            
            // Pack it into an Any for transmission
            Any packedResponse = Any.Pack(response, "Phinix");
            
            // Send it
            netServer.Send(connectionId, MODULE_NAME, packedResponse.ToByteArray());
        }

        private void sendFailedAuthResponsePacket(string connectionId, AuthFailureReason failureReason, string failureMessage)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed AuthResponsePacket to connection {0}", connectionId), LogLevel.DEBUG));
            
            // Construct an AuthResponsePacket in failure configuration
            AuthResponsePacket response = new AuthResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };
            
            // Pack it into an Any for transmission
            Any packedResponse = Any.Pack(response, "Phinix");
            
            // Send it
            netServer.Send(connectionId, MODULE_NAME, packedResponse.ToByteArray());
        }
        
        /// <summary>
        /// Callback for the session cleanup timer. Cleans out expired sessions.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onSessionCleanup(object sender, EventArgs e)
        {
            // Lock the sessions dictionary to prevent other threads from messing with it
            lock (sessionsLock)
            {
                List<string> keysToRemove = new List<string>();

                // Iterate over each session
                foreach (KeyValuePair<string, Session> sessionPair in sessions)
                {
                    string key = sessionPair.Key;
                    Session session = sessionPair.Value;

                    // Check if the expiry has passed
                    if (session.Expiry.CompareTo(DateTime.UtcNow) <= 0)
                    {
                        // Queue the session for removal
                        keysToRemove.Add(key);
                    }
                }

                // Remove sessions in the removal list 
                foreach (string key in keysToRemove)
                {
                    sessions.Remove(key);
                }
            }
        }

        /// <summary>
        /// Returns the existing credential store from disk or a new one if it doesn't exist.
        /// NOTE: This method does not feature an internal lock, care should be taken to lock it externally.
        /// </summary>
        /// <returns>New or existing credential store</returns>
        private CredentialStore getCredentialStore()
        {
            // Create a new credential store if one doesn't already exist
            if (!File.Exists(credentialStorePath))
            {
                // Create a new credential store
                CredentialStore newCredentialStore = new CredentialStore();
                
                // Save the store to disk
                saveCredentialStore(newCredentialStore);
                
                // Finally return the new store
                return newCredentialStore;
            }
            
            // Pull the store from disk
            CredentialStore credentialStore;
            using (FileStream fs = new FileStream(credentialStorePath, FileMode.Open))
            {
                using (CodedInputStream cis = new CodedInputStream(fs))
                {
                    credentialStore = CredentialStore.Parser.ParseFrom(cis);
                }
            }

            // Return the credential store
            return credentialStore;
        }

        /// <summary>
        /// Saves the given credential store to disk, overwriting an existing one.
        /// NOTE: This method does not feature an internal lock, care should be taken to lock it externally.
        /// </summary>
        /// <param name="credentialStore">Credential store to save</param>
        private void saveCredentialStore(CredentialStore credentialStore)
        {
            // Create or truncate the credentials file
            using (FileStream fs = File.Open(credentialStorePath, FileMode.Create, FileAccess.Write))
            {
                using (CodedOutputStream cos = new CodedOutputStream(fs))
                {
                    // Write the credential store to disk
                    credentialStore.WriteTo(cos);
                }
            }
        }
    }
}
