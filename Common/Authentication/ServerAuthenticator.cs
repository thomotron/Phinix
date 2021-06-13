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
    public class ServerAuthenticator : Authenticator, IPersistent
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <see cref="NetServer"/> to send packets and bind events to.
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
        /// Lock object to prevent race conditions when accessing <see cref="sessions"/>.
        /// </summary>
        private object sessionsLock = new object();
        /// <summary>
        /// Timer for clearing out old or invalidated sessions.
        /// </summary>
        private Timer sessionCleanupTimer;

        /// <summary>
        /// Stores credentials for each client.
        /// </summary>
        private CredentialStore credentialStore;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <see cref="credentialStore"/>.
        /// </summary>
        private object credentialStoreLock = new object();

        public ServerAuthenticator(NetServer netServer, string serverName, string serverDescription, AuthTypes authType)
        {
            this.netServer = netServer;
            this.serverName = serverName;
            this.serverDescription = serverDescription;
            this.authType = authType;
            this.credentialStore = new CredentialStore();

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

        public ServerAuthenticator(NetServer netServer, string serverName, string serverDescription, AuthTypes authType, string credentialStorePath) : this(netServer, serverName, serverDescription, authType)
        {
            Load(credentialStorePath);
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
        /// Attempts to get the connection ID from the session with the given session ID.
        /// Returns whether the connection ID was retrieved successfully.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="connectionId">Output connection ID</param>
        /// <returns>Connection ID was retrieved successfully</returns>
        public bool TryGetConnectionId(string sessionId, out string connectionId)
        {
            // Initialise connection ID to something arbitrary
            connectionId = null;

            lock (sessionsLock)
            {
                try
                {
                    // Try to get the connection ID (key) from the entry containing the given session ID (value)
                    connectionId = sessions.Single(pair => pair.Value.SessionId == sessionId).Key;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                return true;
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
        /// Loads the credential store from disk, overwriting the currently loaded credential store.
        /// </summary>
        /// <param name="path">Credential store path</param>
        public void Load(string path)
        {
            lock (credentialStoreLock)
            {
                // Create a new credential store if one doesn't already exist
                if (!File.Exists(path))
                {
                    RaiseLogEntry(new LogEventArgs("No credentials database, generating a new one"));

                    // Create a new credential store
                    credentialStore = new CredentialStore();

                    // Save the store to disk
                    Save(path);

                    // Stop here
                    return;
                }

                // Pull the store from disk
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    using (CodedInputStream cis = new CodedInputStream(fs))
                    {
                        lock (credentialStoreLock)
                        {
                            credentialStore = CredentialStore.Parser.ParseFrom(cis);
                        }
                    }
                }

                RaiseLogEntry(new LogEventArgs(string.Format("Loaded {0} credential{1}", credentialStore.Credentials.Count, credentialStore.Credentials.Count != 1 ? "s" : "")));
            }
        }

        /// <summary>
        /// Saves the credential store to disk, overwriting an existing one.
        /// </summary>
        /// <param name="path">Credential store path</param>
        public void Save(string path)
        {
            lock (credentialStoreLock)
            {
                // Create or truncate the credentials file
                FileStream fs = File.Exists(path)
                    ? File.Open(path, FileMode.Truncate, FileAccess.Write)
                    : File.Create(path);
                using (fs)
                {
                    using (CodedOutputStream cos = new CodedOutputStream(fs))
                    {
                        // Write the credential store to disk
                        credentialStore.WriteTo(cos);
                    }
                }

                RaiseLogEntry(new LogEventArgs(string.Format("Saved {0} credential{1}", credentialStore.Credentials.Count, credentialStore.Credentials.Count != 1 ? "s" : "")));
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
            RaiseLogEntry(new LogEventArgs("Sending HelloPacket to incoming connection " + e.ConnectionId.Highlight(HighlightType.ConnectionID), LogLevel.DEBUG));

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
            Any packedHello = ProtobufPacketHelper.Pack(hello);

            // Try send it
            if (!netServer.TrySend(e.ConnectionId, MODULE_NAME, packedHello.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send HelloPacket to connection " + e.ConnectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        private void ConnectionClosedHandler(object sender, ConnectionEventArgs e)
        {
            RaiseLogEntry(new LogEventArgs("Closing connection from " + e.ConnectionId.Highlight(HighlightType.ConnectionID), LogLevel.DEBUG));

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
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerAuthenticator).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an AuthenticatePacket for session {0}", message.Unpack<AuthenticatePacket>().SessionId.Highlight(HighlightType.SessionID)), LogLevel.DEBUG));
                    authenticatePacketHandler(connectionId, message.Unpack<AuthenticatePacket>());
                    break;
                case "ExtendSessionPacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an ExtendSessionPacket for session {0}", message.Unpack<ExtendSessionPacket>().SessionId.Highlight(HighlightType.SessionID)), LogLevel.DEBUG));
                    extendSessionPacketHandler(connectionId, message.Unpack<ExtendSessionPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <see cref="AuthenticatePacket"/>s from clients trying to authenticate.
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

                RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0}: Wrong credential type", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

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

                    RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0}: No session found for this connection", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

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

                    RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Session expired", connectionId.Highlight(HighlightType.ConnectionID), session.SessionId.Highlight(HighlightType.SessionID)), LogLevel.DEBUG));

                    // Stop here
                    return;
                }

                lock (credentialStoreLock)
                {
                    // Check if user does not have an existing credential
                    if (!credentialStore.Credentials.ContainsKey(packet.Username))
                    {
                        // Exception for PhiKey, it should be a 'zero-configuration' solution
                        if (authType == AuthTypes.ClientKey)
                        {
                            // Create a new credential and add it to the credential store
                            Credential newCredential = new Credential
                            {
                                AuthType = AuthTypes.ClientKey,
                                Username = packet.Username,
                                Password = packet.Password
                            };
                            credentialStore.Credentials.Add(newCredential.Username, newCredential);
                        }
                        else
                        {
                            // Fail the authentication attempt due to missing credential
                            sendFailedAuthResponsePacket(connectionId, AuthFailureReason.Credentials, string.Format("No credential found for the username \"{0}\".", packet.Username.Highlight(HighlightType.Username)));

                            RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): No credentials found for {2}", connectionId.Highlight(HighlightType.ConnectionID), session.SessionId.Highlight(HighlightType.SessionID), packet.Username.Highlight(HighlightType.Username)), LogLevel.DEBUG));

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

                        RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Stored credentials for {2} are of wrong type ({3})", connectionId.Highlight(HighlightType.ConnectionID), session.SessionId.Highlight(HighlightType.SessionID), packet.Username.Highlight(HighlightType.Username), credential.AuthType.ToString()), LogLevel.DEBUG));

                        // Stop here
                        return;
                    }

                    // Check if the password does not match the stored credential
                    // This check is ignored if the auth type is 'ClientKey' as the password does not matter
                    if (authType == AuthTypes.ClientKey && packet.Password != credential.Password)
                    {
                        // Fail the authentication attempt due to mismatching password
                        sendFailedAuthResponsePacket(connectionId, AuthFailureReason.Credentials, "Invalid password provided.");

                        RaiseLogEntry(new LogEventArgs(string.Format("Auth failure for {0} (SessID: {1}): Wrong password provided for {2}", connectionId.Highlight(HighlightType.ConnectionID), session.SessionId.Highlight(HighlightType.SessionID), packet.Username.Highlight(HighlightType.Username)), LogLevel.DEBUG));

                        // Stop here
                        return;
                    }

                    // Auth attempt made it through the gauntlet, time to accept it as a valid request

                    // Mark the session as authenticated
                    session.Authenticated = true;

                    // Extend their session by 30 minutes
                    TimeSpan expiresIn = TimeSpan.FromMinutes(30);
                    session.Expiry = DateTime.UtcNow + expiresIn;

                    // Approve the authentication attempt
                    sendSuccessfulAuthResponsePacket(connectionId, session.SessionId, (int) expiresIn.TotalMilliseconds);

                    // Log this momentous occasion
                    RaiseLogEntry(new LogEventArgs(string.Format("User \"{0}\" (ConnID: {1}, SessID: {2}) successfully authenticated", session.Username.Highlight(HighlightType.Username), session.ConnectionId.Highlight(HighlightType.ConnectionID), session.SessionId.Highlight(HighlightType.SessionID))));
                }
            }
        }

        /// <summary>
        /// Sends a successful <see cref="AuthResponsePacket"/> to a connection.
        /// </summary>
        /// <param name="connectionId">Recipient's connection ID</param>
        /// <param name="sessionId">New session ID</param>
        /// <param name="expiresIn">Milliseconds until session expiry</param>
        private void sendSuccessfulAuthResponsePacket(string connectionId, string sessionId, int expiresIn)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful AuthResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Construct an AuthResponsePacket in success configuration
            AuthResponsePacket response = new AuthResponsePacket
            {
                Success = true,
                SessionId = sessionId,
                ExpiresIn = expiresIn
            };

            // Pack it into an Any for transmission
            Any packedResponse = ProtobufPacketHelper.Pack(response);

            // Send it
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedResponse.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send AuthResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a failed <see cref="AuthResponsePacket"/> to a connection.
        /// </summary>
        /// <param name="connectionId">Recipient's connection ID</param>
        /// <param name="failureReason">Failure reason enum</param>
        /// <param name="failureMessage">Failure reason message</param>
        private void sendFailedAuthResponsePacket(string connectionId, AuthFailureReason failureReason, string failureMessage)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed AuthResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Construct an AuthResponsePacket in failure configuration
            AuthResponsePacket response = new AuthResponsePacket
            {
                Success = false,
                FailureReason = failureReason,
                FailureMessage = failureMessage
            };

            // Pack it into an Any for transmission
            Any packedResponse = ProtobufPacketHelper.Pack(response);

            // Send it
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedResponse.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send AuthResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Handles incoming <see cref="ExtendSessionPacket"/>s from clients trying to extend their session expiry.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="ExtendSessionPacket"/></param>
        private void extendSessionPacketHandler(string connectionId, ExtendSessionPacket packet)
        {
            // Lock the sessions dictionary to prevent other threads from messing with it
            lock (sessionsLock)
            {
                // Make sure the session is still valid
                if (IsAuthenticated(connectionId, packet.SessionId))
                {
                    // Extend the session expiry to 30 minutes from now
                    sessions[connectionId].Expiry = DateTime.UtcNow + TimeSpan.FromMinutes(30);

                    // Send a successful response
                    sendSuccessfulExtendSessionResponsePacket(connectionId, sessions[connectionId].Expiry);
                }
                else
                {
                    // Send a failed response
                    sendFailedExtendSessionResponsePacket(connectionId);
                }
            }
        }

        /// <summary>
        /// Sends a successful <see cref="ExtendSessionResponsePacket"/> to a connection with the given expiry.
        /// </summary>
        /// <param name="connectionId">Recipient's connection ID</param>
        /// <param name="expiry">New session expiry</param>
        private void sendSuccessfulExtendSessionResponsePacket(string connectionId, DateTime expiry)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending successful ExtendSessionResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Create and pack a response
            ExtendSessionResponsePacket packet = new ExtendSessionResponsePacket
            {
                Success = true,
                ExpiresIn = (int) (expiry - DateTime.UtcNow).TotalMilliseconds
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send ExtendSessionResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends a failed <see cref="ExtendSessionResponsePacket"/> to a connection.
        /// </summary>
        /// <param name="connectionId">Recipient's connection ID</param>
        private void sendFailedExtendSessionResponsePacket(string connectionId)
        {
            RaiseLogEntry(new LogEventArgs(string.Format("Sending failed ExtendSessionResponsePacket to connection {0}", connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Create and pack a response
            ExtendSessionResponsePacket packet = new ExtendSessionResponsePacket
            {
                Success = false
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            if (!netServer.TrySend(connectionId, MODULE_NAME, packedPacket.ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send ExtendSessionResponsePacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
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
    }
}
