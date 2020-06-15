using System;

namespace UserManagement
{
    /// <summary>
    /// Event args for when a user's display name changes.
    /// Contains both their previous and current display names.
    /// </summary>
    public class UserDisplayNameChangedEventArgs : EventArgs
    {
        /// <summary>
        /// UUID of the user.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// The user's previous display name.
        /// </summary>
        public string OldDisplayName;

        /// <summary>
        /// The user's new display name.
        /// </summary>
        public string NewDisplayName;

        /// <summary>
        /// Creates a new <see cref="UserDisplayNameChangedEventArgs"/> with the given previous and current states.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="oldDisplayName">Previous display name</param>
        /// <param name="newDisplayName">Current display name</param>
        public UserDisplayNameChangedEventArgs(string uuid, string oldDisplayName, string newDisplayName)
        {
            Uuid = uuid;
            OldDisplayName = oldDisplayName;
            NewDisplayName = newDisplayName;
        }
    }
}