using System;
using System.Collections.Generic;
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
        
        public ServerAuthenticator(NetServer netServer, string serverName, string serverDescription, AuthTypes authType)
        {
            this.netServer = netServer;
            this.serverName = serverName;
            this.serverDescription = serverDescription;
            this.authType = authType;
            
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
        }

        private void ConnectionEstablishedHandler(object sender, ConnectionEventArgs e)
        {
            RaiseLogEntry(new LogEventArgs("Sending HelloPacket to incoming connection " + e.ConnectionId, LogLevel.DEBUG));
            
            // Create a new session for this connection
            Session session = new Session
            {
                SessionId = Guid.NewGuid().ToString(),
                ConnectionId = e.ConnectionId,
                Expiry = DateTime.UtcNow + TimeSpan.FromMinutes(5)
            };
            
            // Add it to the session dictionary
            sessions.Add(e.ConnectionId, session);
            
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

        /// <inheritdoc />
        protected override void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!validatePacket(module, data, out Any message, out TypeUrl typeUrl)) return;
            
            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "AuthenticatePacket":
                    // TODO: AuthenticatePacket handling
                    RaiseLogEntry(new LogEventArgs(string.Format("Got an AuthenticatePacket for session \"{0}\"", message.Unpack<AuthenticatePacket>().SessionId), LogLevel.DEBUG));
                    break;
                default:
                    // TODO: Discard packet
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
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
