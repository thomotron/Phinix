using System.Runtime.Serialization;

namespace Authentication
{
    /// <summary>
    /// A generic class for sending credentials to a server to authenticate with.
    /// </summary>
    [DataContract]
    public abstract class Credentials
    {
        /// <summary>
        /// The relevant <c>AuthType</c> that this credential is for.
        /// </summary>
        [DataMember] public AuthType AuthType;
    }
}