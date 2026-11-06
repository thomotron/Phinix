using System;
using System.Reflection;
using Utils;

namespace Agora
{
    /// <summary>
    /// Shared base for the market board's server and client halves, like <c>Chat.Chat</c> is for chat.
    /// </summary>
    public abstract class Agora : ILoggable
    {
        /// <summary>
        /// Name packets are routed by. Every module has its own, so the server knows which module a packet is for.
        /// </summary>
        public readonly string MODULE_NAME = typeof(Agora).Namespace;

        public static readonly Version Version = Assembly.GetAssembly(typeof(Agora)).GetName().Version;

        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
    }
}
