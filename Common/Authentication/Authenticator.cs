using System;
using System.Reflection;
using Connections;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <summary>
    /// Provides some common properties for <c>ClientAuthenticator</c> and <c>ServerAuthenticator</c> classes.
    /// </summary>
    public abstract class Authenticator : ILoggable
    {
        public const string MODULE_NAME = "auth";
        
        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Authenticator)).GetName().Version;
        
        /// <summary>
        /// Handles incoming packets for this Authenticator.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        protected abstract void packetHandler(string module, string connectionId, byte[] data);
    }
}
