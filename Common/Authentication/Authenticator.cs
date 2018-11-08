using System;
using System.CodeDom;
using System.Reflection;
using Google.Protobuf.Reflection;
using Utils;

namespace Authentication
{
    /// <summary>
    /// Provides some common properties for <c>ClientAuthenticator</c> and <c>ServerAuthenticator</c> classes.
    /// </summary>
    public abstract class Authenticator : ILoggable
    {
        public const string MODULE_NAME = "auth";
        
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        public abstract void RaiseLogEntry(LogEventArgs e);
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Authenticator)).GetName().Version;
        
        protected abstract void packetHandler(string packetType, string connectionId, byte[] data);
    }
}
