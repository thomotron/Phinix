using System;

namespace UserManagement
{
    /// <summary>
    /// Event args for when a user's login state changes.
    /// Contains both their previous and current login states.
    /// </summary>
    public class UserLoginStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// UUID of the user.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// The user's previous login state.
        /// </summary>
        public bool OldLoggedInState;

        /// <summary>
        /// The user's new login state.
        /// </summary>
        public bool NewLoggedInState;

        /// <summary>
        /// Creates a new <see cref="UserLoginStateChangedEventArgs"/> with the given previous and current states.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="oldLoggedInState">Previous login state</param>
        /// <param name="newLoggedInState">Current login state</param>
        public UserLoginStateChangedEventArgs(string uuid, bool oldLoggedInState, bool newLoggedInState)
        {
            Uuid = uuid;
            OldLoggedInState = oldLoggedInState;
            NewLoggedInState = newLoggedInState;
        }
    }
}