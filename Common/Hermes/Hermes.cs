using System;
using System.Reflection;
using Utils;

namespace Hermes
{
    /// <summary>
    /// Shared base for Hermes's server and client halves, like <c>Agora.Agora</c> is for the market board.
    /// </summary>
    /// <remarks>
    /// Hermes carries things between two specific players: private messages and gifts.
    /// </remarks>
    public abstract class Hermes : ILoggable
    {
        /// <summary>
        /// Name packets are routed by. Every module has its own, so the server knows which module a packet is for.
        /// </summary>
        public readonly string MODULE_NAME = typeof(Hermes).Namespace;

        public static readonly Version Version = Assembly.GetAssembly(typeof(Hermes)).GetName().Version;

        /// <summary>
        /// Longest a private message can be, in characters.
        /// </summary>
        public const int MAX_MESSAGE_LENGTH = 500;

        /// <summary>
        /// Most different items one gift can hold.
        /// </summary>
        public const int MAX_ITEMS_PER_GIFT = 10;

        /// <summary>
        /// Longest a gift's note can be, in characters.
        /// </summary>
        public const int MAX_NOTE_LENGTH = 200;

        /// <summary>
        /// Most gifts that can wait on the server for one offline player.
        /// </summary>
        /// <remarks>
        /// Waiting gifts are kept in memory and saved to disk, so without a cap someone could fill the server up by
        /// sending gifts to a player who never logs in again.
        /// </remarks>
        public const int MAX_PENDING_GIFTS_PER_RECIPIENT = 20;

        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
    }
}
