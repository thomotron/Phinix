using System;

namespace UserManagement
{
    /// <summary>
    /// The login name a client uses on a companion server: its own secret key, plus the server and UUID it's claiming
    /// to be on its main (OG) Phinix server.
    /// </summary>
    /// <remarks>
    /// A companion server runs features the main server doesn't have, for players who stay on the main server for
    /// chat and trades. It can't ask the main server who anyone is, so it trusts each client's claim and locks it to
    /// the first client that makes it ("first claim wins"): a later client with a different key claiming the same
    /// UUID is turned away.
    /// <para>
    /// The claim rides in the ClientKey credential's username, so no packets had to change and a normal server just
    /// sees an unusual username. Format: <c>key|server|uuid</c>. Keys are Base64 and UUIDs are GUIDs, so neither can
    /// contain '|'; the server address is everything between the first and last '|'.
    /// </para>
    /// </remarks>
    public static class CompanionClaim
    {
        private const char SEPARATOR = '|';

        /// <summary>
        /// Builds the companion login name for a client.
        /// </summary>
        /// <param name="clientKey">The client's secret key for the companion</param>
        /// <param name="server">Main server, as <see cref="NormaliseServer"/> returns it</param>
        /// <param name="uuid">The client's UUID on the main server</param>
        /// <returns>Login name to authenticate with</returns>
        public static string Build(string clientKey, string server, string uuid)
        {
            return clientKey + SEPARATOR + server + SEPARATOR + uuid;
        }

        /// <summary>
        /// Splits a companion login name back into its parts.
        /// </summary>
        /// <param name="username">Login name from authentication</param>
        /// <param name="clientKey">The client's secret key</param>
        /// <param name="server">Main server the claim is for</param>
        /// <param name="uuid">UUID claimed on that server</param>
        /// <returns>Whether the username is a well-formed claim</returns>
        public static bool TryParse(string username, out string clientKey, out string server, out string uuid)
        {
            clientKey = server = uuid = null;
            if (string.IsNullOrEmpty(username)) return false;

            int first = username.IndexOf(SEPARATOR);
            int last = username.LastIndexOf(SEPARATOR);
            if (first <= 0 || last <= first + 1 || last == username.Length - 1) return false;

            clientKey = username.Substring(0, first);
            server = username.Substring(first + 1, last - first - 1);
            uuid = username.Substring(last + 1);

            // Only accept real UUIDs, written the standard way, so one person can't claim two spellings of the same ID
            return Guid.TryParse(uuid, out Guid parsed) && parsed.ToString() == uuid;
        }

        /// <summary>
        /// Writes a main server's address the same way every time, so "Phinix.chat" and "phinix.chat:16200" are the
        /// same server.
        /// </summary>
        /// <param name="address">Server address from the settings</param>
        /// <param name="port">Server port from the settings</param>
        /// <returns>Address in lower case with its port</returns>
        public static string NormaliseServer(string address, int port)
        {
            return (address ?? "").Trim().ToLowerInvariant() + ":" + port;
        }
    }
}
