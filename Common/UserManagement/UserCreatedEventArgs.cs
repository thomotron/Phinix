using System;

namespace UserManagement
{
    /// <summary>
    /// Event args for when a user is created.
    /// Contains their UUID, login state, and display name.
    /// </summary>
    public class UserCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// UUID of the user.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// Whether the user is logged in.
        /// </summary>
        public bool LoggedIn;

        /// <summary>
        /// The user's display name.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Creates a new <see cref="UserCreatedEventArgs"/> with the given logged-in state and display name.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="loggedIn">Logged in state</param>
        /// <param name="displayName">User's display name</param>
        public UserCreatedEventArgs(string uuid, bool loggedIn, string displayName)
        {
            Uuid = uuid;
            LoggedIn = loggedIn;
            DisplayName = displayName;
        }
    }
}