using System;

namespace Utils
{
    /// <summary>
    /// Lets a server module ask for everything to be saved soon, right after it takes or hands over players' items.
    /// </summary>
    /// <remarks>
    /// The server otherwise only saves every few minutes. If it crashed in between, items it had already told a player
    /// it was holding (so the player's own copy was let go) would be lost with it. The server answers a request by
    /// saving about a second later, so a burst of requests turns into one save.
    /// </remarks>
    public static class SaveRequests
    {
        /// <summary>
        /// Raised when a module asks for a save. The server subscribes to this; on a client nothing does.
        /// </summary>
        public static event Action Requested;

        /// <summary>
        /// Asks for a save soon. Safe to call while holding a module's lock: the save itself happens on another thread.
        /// </summary>
        public static void Request() => Requested?.Invoke();
    }
}
