using System.Runtime.Serialization;
using Connections;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// An authentication response sent by the server.
    /// Contains whether the authentication attempt was successful and relevant details.
    /// </summary>
    [DataContract]
    public class AuthResponsePacket : Packet
    {
        /// <summary>
        /// Whether the authentication attempt was successful.
        /// </summary>
        [DataMember] public bool Success;
        
        /// <summary>
        /// Reason for authentication failure. 
        /// </summary>
        [DataMember] public FailureReason FailureReason;
        
        /// <summary>
        /// Descriptive message as to why authentication failed.
        /// </summary>
        [DataMember] public string FailureMessage;
        
        /// <summary>
        /// Authenticated session ID.
        /// </summary>
        [DataMember] public string SessionId;
        
        /// <summary>
        /// Authenticated username.
        /// </summary>
        [DataMember] public string Username;
    }
    
    /// <summary>
    /// Identifies a field in an <c>AuthenticatePacket</c> that caused an authentication failure.
    /// </summary>
    [DataContract]
    public enum FailureReason
    {
        [EnumMember] AuthType,
        [EnumMember] Credentials,
        [EnumMember] SessionId
    }
}