using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Authentication
{
    public class Authenticator
    {
        
    }

    /// <summary>
    /// An authentication type.
    /// </summary>
    [DataContract]
    public enum AuthType
    {
        [EnumMember] PhiKey
    }
}
