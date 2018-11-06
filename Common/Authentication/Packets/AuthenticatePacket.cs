using System.Runtime.Serialization;
using Connections;

namespace Authentication
{
    /// <inheritdoc/>
    /// <summary>
    /// Packet sent to the server to authenticate.
    /// Contains credentials, authentication type, and some state information.
    /// </summary>
    [DataContract]
    public class AuthenticatePacket : Packet
    {
        /// <summary>
        /// Authentication type.
        /// </summary>
        [DataMember] public AuthType AuthType;
        
        /// <summary>
        /// <c>Credentials</c> object containing relevant credentials to authenticate.
        /// </summary>
        [DataMember] public Credentials Credentials;
        
        /// <summary>
        /// Unique session ID sent by the server in the <c>HelloPacket</c> packet. 
        /// </summary>
        [DataMember] public string SessionId;
        
        /// <summary>
        /// Whether the server should update the user's username with the one provided.
        /// </summary>
        [DataMember] public bool UseServerUsername;
        
        /// <summary>
        /// The user's desired username.
        /// </summary>
        [DataMember] public string Username;
        
        /// <summary>
        /// Creates a new <c>AuthenticatePacket</c> ready to be sent to the server for authentication.
        /// </summary>
        /// <param name="authType">Authentication type</param>
        /// <param name="credentials">Credentials for the given authentication type</param>
        /// <param name="sessionId">Session ID provided by the server</param>
        /// <param name="useServerUsername">Whether to use the username the server has on file</param>
        /// <param name="username">User's desired username</param>
        public AuthenticatePacket(AuthType authType, Credentials credentials, string sessionId, bool useServerUsername, string username)
        {
            this.AuthType = authType;
            this.Credentials = credentials;
            this.SessionId = sessionId;
            this.UseServerUsername = useServerUsername;
            this.Username = username;
        }
    }
}