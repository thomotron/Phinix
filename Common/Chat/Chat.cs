using System;
using System.Reflection;
using Utils;

namespace Chat
{
    public abstract class Chat : ILoggable
    {
        public readonly string MODULE_NAME = typeof(Chat).Namespace;
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Chat)).GetName().Version;
        
        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
    }
}