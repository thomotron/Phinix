using System;
using System.Reflection;
using Utils;

namespace Trading
{
    public abstract class Trading : ILoggable
    {
        public static readonly Version Version = Assembly.GetAssembly(typeof(Trading)).GetName().Version;

        public const string MODULE_NAME = "trade";

        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
    }
}