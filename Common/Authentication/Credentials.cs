using System.Runtime.Serialization;

namespace Authentication
{
    /// <summary>
    /// A generic class for sending credentials to a server to authenticate with.
    /// </summary>
    [DataContract]
    public abstract class Credentials : IExtensibleDataObject
    {
        // This will hold any excess data that doesn't fit in the current version of this class
        public ExtensionDataObject ExtensionData { get; set; }
        
        /// <summary>
        /// The relevant <c>AuthType</c> that this credential is for.
        /// </summary>
        [DataMember] public AuthType AuthType;
    }
}