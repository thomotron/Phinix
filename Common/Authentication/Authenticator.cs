using System;
using System.CodeDom;
using System.Reflection;
using System.Runtime.Serialization;
using ProtoBuf.Meta;

namespace Authentication
{
    /// <summary>
    /// Provides some common properties for <c>ClientAuthenticator</c> and <c>ServerAuthenticator</c> classes.
    /// </summary>
    public abstract class Authenticator
    {
        public const string MODULE_NAME = "auth";
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Authenticator)).GetName().Version;

        public Authenticator()
        {
            RuntimeTypeModel model = RuntimeTypeModel.Default;
            model.Add(typeof(HelloPacket), true);
            model[typeof(HelloPacket)]
                .Add(1, "ServerName")
                .Add(2, "ServerDescription")
                .Add(3, "AuthType")
                .Add(4, "SessionId");
        }
        
        public abstract void packetHandler(string packetType, string connectionId, byte[] data);
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
