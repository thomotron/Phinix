using System;
using System.Reflection;
using Utils;

namespace Authentication
{
    /// <summary>
    /// Provides some common properties for <see cref="ClientAuthenticator"/> and <see cref="ServerAuthenticator"/> classes.
    /// </summary>
    public abstract class Authenticator : ILoggable
    {
        public readonly string MODULE_NAME = typeof(Authenticator).Namespace;
        
        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Authenticator)).GetName().Version;

        /// <summary>
        /// Version of the network protocol. The server only lets in clients with exactly this number.
        /// </summary>
        /// <remarks>
        /// Add one to this whenever a change means an older mod can't safely play with a newer server, or the
        /// other way round (e.g. new fields that trades rely on). Remember to rebuild both the server and the mod.
        /// <para>
        /// The same goes for new modules or packets that carry items. A mod that doesn't know a packet just throws it
        /// away, so if the server sends it items (a gift, a bounty reward), those items are gone for good.
        /// </para>
        /// <para>
        /// History:
        /// 1 = scars and bandages on traded pawns.
        /// 2 = Hermes (messages and gifts), contracts, and settling held items after a dropped connection.
        /// 3 = trade offers get the same receipts, so items put on offer can't vanish if the connection drops.
        /// 4 = player markets (shops with stock held by the server, silver prices and a till) replace the market board.
        /// </para>
        /// </remarks>
        public const int PROTOCOL_VERSION = 4;
        
        /// <summary>
        /// Handles incoming packets for this Authenticator.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        protected abstract void packetHandler(string module, string connectionId, byte[] data);
    }
}
