using System.Runtime.Serialization;
using Connections;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// The hello packet sent by the server upon connecting. It contains all the metadata required to identify the
    /// server and authenticate.
    /// </summary>
    public class HelloPacket : Packet
    {
        /// <summary>
        /// Name of the server.
        /// </summary>
        public string ServerName;
        
        /// <summary>
        /// Description of the server.
        /// </summary>
        public string ServerDescription;
        
        /// <summary>
        /// Accepted authentication type.
        /// </summary>
        public AuthType AuthType;
        
        /// <summary>
        /// A unique session ID generated specifically for this authentication attempt.
        /// </summary>
        public string SessionId;

        public HelloPacket(string serverName, string serverDescription, AuthType authType, string sessionId)
        {
            this.ServerName = serverName;
            this.ServerDescription = serverDescription;
            this.AuthType = authType;
            this.SessionId = sessionId;
        }
    }
}