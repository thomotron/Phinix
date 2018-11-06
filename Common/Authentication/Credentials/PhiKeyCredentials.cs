using System.Runtime.Serialization;

namespace Authentication
{
    /// <inheritdoc />
    /// <summary>
    /// Encapsulates a PhiKey in string form for authenticating with a server.
    /// </summary>
    [DataContract]
    public class PhiKeyCredentials : Credentials
    {
        /// <summary>
        /// The PhiKey string.
        /// </summary>
        [DataMember] public string PhiKey;
        
        /// <summary>
        /// Creates a PhiKey credential for authenticating with a server.
        /// </summary>
        /// <param name="key">PhiKey string</param>
        public PhiKeyCredentials(string key)
        {
            this.AuthType = AuthType.PhiKey;
            this.PhiKey = key;
        }
    }
}