using System.Runtime.Serialization;
using Connections;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// The hello packet sent by the server upon connecting. It contains all the metadata required to identify the
    /// server and authenticate.
    /// </summary>
    [DataContract]
    public class HelloPacket : Packet
    {
        /// <summary>
        /// Name of the server.
        /// </summary>
        [DataMember] public string ServerName;
        
        /// <summary>
        /// Description of the server.
        /// </summary>
        [DataMember] public string ServerDescription;
        
        /// <summary>
        /// Accepted authentication type.
        /// </summary>
        [DataMember] public AuthType AuthType;
        
        /// <summary>
        /// A unique session ID generated specifically for this authentication attempt.
        /// </summary>
        [DataMember] public string SessionId;

        public HelloPacket(string serverName, string serverDescription, AuthType authType, string sessionId)
        {
            this.ServerName = serverName;
            this.ServerDescription = serverDescription;
            this.AuthType = authType;
            this.SessionId = sessionId;
        }
    }
}