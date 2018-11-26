using System;
using System.Linq;
using System.Reflection;
using Utils;

namespace UserManagement
{
    /// <inheritdoc />
    /// <summary>
    /// Module that organises users, their credentials, and their login states.
    /// Anything and everything pertaining to a particular user's state is accessed through this.
    /// </summary>
    public abstract class UserManager : ILoggable
    {
        public const string MODULE_NAME = "usermgmt";
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(UserManager)).GetName().Version;
        
        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);

        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        private UserStore userStore;
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        private object userStoreLock = new object();

        /// <summary>
        /// Tries to get a user's UUID by their username and returns whether it was retrieved successfully.
        /// </summary>
        /// <param name="username">Username of the user</param>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>Successfully retrieved user</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetUserUuid(string username, out string uuid)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentException("Username cannot be null or empty.", nameof(uuid));

            // Initialise UUID to something arbitrary
            uuid = null;

            lock (userStoreLock)
            {
                User matchingUser;
                try
                {
                    matchingUser = userStore.Users.Values.Single(user => user.Username == username);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                
                uuid = matchingUser.Uuid;
            }
            
            return true;
        }
        
        /// <summary>
        /// Creates a new user with the given display name, optionally logged-in, and adds them to the user list.
        /// Returns the UUID of the newly-created user.
        /// </summary>
        /// <param name="username">Username of the user</param>
        /// <param name="displayName">Display name of the user</param>
        /// <param name="loggedIn">Whether the user should be logged in</param>
        /// <returns>UUID of created user</returns>
        /// <exception cref="ArgumentException">Username cannot be null or empty</exception>
        /// <exception cref="ArgumentException">Display name cannot be null or empty</exception>
        public string CreateUser(string username, string displayName, bool loggedIn = false)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));

            User user = new User
            {
                Uuid = Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = displayName,
                LoggedIn = loggedIn
            };

            lock (userStoreLock)
            {
                userStore.Users.Add(user.Uuid, user);
            }

            return user.Uuid;
        }

        /// <summary>
        /// Updates an existing user's properties.
        /// Returns true if the update was successful, otherwise false.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="displayName">Display name of the user</param>
        /// <returns>User updated successfully</returns>
        /// <exception cref="ArgumentNullException">UUID cannot be null or empty</exception>
        public bool UpdateUser(string uuid, string displayName = null)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                if (displayName != null) userStore.Users[uuid].DisplayName = displayName;
            }

            return true;
        }

        /// <summary>
        /// Removes an existing user by their UUID.
        /// Returns true if the user was removed successfully, otherwise false.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>User removed successfully</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool RemoveUser(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                return userStore.Users.Remove(uuid);
            }
        }

        /// <summary>
        /// Attempts to log in a user with the given UUID.
        /// Returns whether the user was successfully logged in.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>Whether the user was successfully logged in</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryLogIn(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                userStore.Users[uuid].LoggedIn = true;
            }

            // TODO: Broadcast the state change
            return true;
        }
        
        /// <summary>
        /// Attempts to log out a user with the given UUID.
        /// Returns whether the user was successfully logged out.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <returns>Whether the user was successfully logged out</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryLogOut(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                userStore.Users[uuid].LoggedIn = false;
            }

            // TODO: Broadcast the state change
            return true;
        }

        /// <summary>
        /// Attempts to get the display name of a user with the given UUID.
        /// Returns whether the display name was retrieved successfully.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="displayName">Output display name</param>
        /// <returns>Display name was retrieved successfully</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetDisplayName(string uuid, out string displayName)
        {
            // Initialise display name to something arbitrary
            displayName = null;
            
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                displayName = userStore.Users[uuid].DisplayName;
            }

            return true;
        }
    }
}
