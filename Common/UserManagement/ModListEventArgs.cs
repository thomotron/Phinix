using System;
using System.Collections.Generic;

namespace UserManagement
{
    /// <summary>
    /// Event args for when the server answers a request for another player's mod list.
    /// </summary>
    public class ModListEventArgs : EventArgs
    {
        /// <summary>
        /// UUID of the player the list belongs to.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// Whether the server knew their mods. False if they haven't logged in since it started, or use an older mod.
        /// </summary>
        public bool Known;

        /// <summary>
        /// Their mods, empty if not <see cref="Known"/>.
        /// </summary>
        public List<ModInfo> Mods;

        public ModListEventArgs(string uuid, bool known, List<ModInfo> mods)
        {
            Uuid = uuid;
            Known = known;
            Mods = mods;
        }
    }
}
