using System;

namespace UserManagement
{
    public class UserChangedEventArgs : EventArgs
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
        /// Display name of the user.
        /// </summary>
        public string DisplayName;

        public UserChangedEventArgs(string uuid, bool loggedIn, string displayName)
        {
            this.Uuid = uuid;
            this.LoggedIn = loggedIn;
            this.DisplayName = displayName;
        }
    }
}