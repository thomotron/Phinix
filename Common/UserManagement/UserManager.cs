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
        public readonly string MODULE_NAME = typeof(UserManager).Namespace;
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(UserManager)).GetName().Version;

        /// <summary>
        /// Longest bio kept, in characters. About two or three short sentences, which fits in a tooltip.
        /// </summary>
        public const int MAX_BIO_LENGTH = 200;

        /// <summary>
        /// Tidies a bio the same way on both ends: no colours or formatting, one line, and no longer than
        /// <see cref="MAX_BIO_LENGTH"/>.
        /// </summary>
        /// <param name="bio">Bio as written</param>
        /// <returns>The bio to keep, or an empty string</returns>
        public static string CleanBio(string bio)
        {
            if (string.IsNullOrWhiteSpace(bio)) return "";

            string clean = string.Join(" ", TextHelper.StripRichText(bio).Split((char[]) null, StringSplitOptions.RemoveEmptyEntries));
            return clean.Length > MAX_BIO_LENGTH ? clean.Substring(0, MAX_BIO_LENGTH).TrimEnd() : clean;
        }
        
        /// <summary>
        /// Longest colony name kept, in characters.
        /// </summary>
        public const int MAX_COLONY_NAME_LENGTH = 40;

        /// <summary>
        /// Tidies shared colony stats the same way on both ends: a plain, short name and biome, and no negative or
        /// absurd numbers. An empty <see cref="ColonyStats"/> means the player isn't sharing one.
        /// </summary>
        /// <param name="colony">Stats as sent, or null</param>
        /// <returns>The stats to keep, or null if none were sent</returns>
        public static ColonyStats CleanColony(ColonyStats colony)
        {
            if (colony == null) return null;

            float wealth = colony.Wealth;
            if (float.IsNaN(wealth) || float.IsInfinity(wealth)) wealth = 0f;

            return new ColonyStats
            {
                Name = cleanLine(colony.Name, MAX_COLONY_NAME_LENGTH),
                Biome = cleanLine(colony.Biome, MAX_COLONY_NAME_LENGTH),
                Days = Math.Max(0, Math.Min(colony.Days, 10000000)),
                Colonists = Math.Max(0, Math.Min(colony.Colonists, 100000)),
                Animals = Math.Max(0, Math.Min(colony.Animals, 100000)),
                Wealth = Math.Max(0f, Math.Min(wealth, 1e12f))
            };
        }

        /// <summary>
        /// Text with no colours or formatting, on one line, cut to <paramref name="max"/> characters.
        /// </summary>
        private static string cleanLine(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string plain = string.Join(" ", TextHelper.StripRichText(text).Split((char[]) null, StringSplitOptions.RemoveEmptyEntries));
            return plain.Length > max ? plain.Substring(0, max).TrimEnd() : plain;
        }

        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);

        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        protected abstract UserStore userStore { get; set; }

        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        protected abstract object userStoreLock { get; }

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
        /// <param name="uuid">UUID to give the user, or null for a new random one</param>
        /// <returns>UUID of created user</returns>
        /// <exception cref="ArgumentException">Display name cannot be null or empty</exception>
        public string CreateUser(string username, string displayName, bool loggedIn = false, string uuid = null)
        {
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));

            User user = new User
            {
                Uuid = uuid ?? Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = string.IsNullOrEmpty(displayName) ? "" : displayName,
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
        /// <param name="acceptingTrades">Whether the user is accepting trades</param>
        /// <returns>User updated successfully</returns>
        /// <exception cref="ArgumentNullException">UUID cannot be null or empty</exception>
        /// <param name="bio">The user's bio, already cleaned with <see cref="CleanBio"/></param>
        public virtual bool UpdateUser(string uuid, string displayName = null, bool? acceptingTrades = null, string bio = null, ColonyStats colony = null)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                if (displayName != null) userStore.Users[uuid].DisplayName = displayName;

                if (acceptingTrades.HasValue) userStore.Users[uuid].AcceptingTrades = acceptingTrades.Value;

                if (bio != null) userStore.Users[uuid].Bio = bio;

                if (colony != null) userStore.Users[uuid].Colony = colony;
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
        public virtual bool TryLogIn(string uuid)
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
        public virtual bool TryLogOut(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                userStore.Users[uuid].LoggedIn = false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the logged-in state of a user with the given UUID.
        /// Returns whether the logged-in state was retrieved successfully.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="loggedIn">Output logged-in state</param>
        /// <returns>Logged-in state was retrieved successfully</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetLoggedIn(string uuid, out bool loggedIn)
        {
            // Initialise logged in to something arbitrary
            loggedIn = false;
            
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;
                
                loggedIn = userStore.Users[uuid].LoggedIn;
            }
            
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

        /// <summary>
        /// Attempts to get the accepting trades state of a user with the given UUID.
        /// Returns whether the accepting trades state was retrieved successfully.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="acceptingTrades">Output accepting trades state</param>
        /// <returns>accepting trades state was retrieved successfully</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetAcceptingTrades(string uuid, out bool acceptingTrades)
        {
            // Initialise accepting trades to something arbitrary
            acceptingTrades = false;
            
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                acceptingTrades = userStore.Users[uuid].AcceptingTrades;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the bio of a user with the given UUID.
        /// Returns whether the user was found; their bio is empty if they haven't written one.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="bio">Output bio</param>
        /// <returns>User was found</returns>
        public bool TryGetBio(string uuid, out string bio)
        {
            bio = "";
            if (string.IsNullOrEmpty(uuid)) return false;

            lock (userStoreLock)
            {
                if (!userStore.Users.TryGetValue(uuid, out User user)) return false;

                bio = user.Bio;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get the colony a user has shared.
        /// Returns whether they've shared one.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="colony">Output stats, a copy</param>
        /// <returns>Whether stats were shared</returns>
        public bool TryGetColony(string uuid, out ColonyStats colony)
        {
            colony = null;
            if (string.IsNullOrEmpty(uuid)) return false;

            lock (userStoreLock)
            {
                if (!userStore.Users.TryGetValue(uuid, out User user) || user.Colony == null || string.IsNullOrEmpty(user.Colony.Name)) return false;

                colony = user.Colony.Clone();
            }

            return true;
        }

        /// <summary>
        /// Attempts to get when a user last logged out, in Unix seconds by the server's clock.
        /// Returns whether the server knows; it doesn't for players who haven't logged out since it started recording.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="lastSeen">Output time</param>
        /// <returns>Whether a time is known</returns>
        public bool TryGetLastSeen(string uuid, out long lastSeen)
        {
            lastSeen = 0;
            if (string.IsNullOrEmpty(uuid)) return false;

            lock (userStoreLock)
            {
                if (!userStore.Users.TryGetValue(uuid, out User user)) return false;

                lastSeen = user.LastSeen;
            }

            return lastSeen > 0;
        }

        /// <summary>
        /// Attempts to get the state of a user with the given UUID.
        /// Returns whether the user was retrieved successfully.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="user">Output user state</param>
        /// <returns>User was retrieved successfully</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetUser(string uuid, out ImmutableUser user)
        {
            // Initialise user to something arbitrary
            user = new ImmutableUser();

            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            lock (userStoreLock)
            {
                if (!userStore.Users.ContainsKey(uuid)) return false;

                user = new ImmutableUser(uuid, userStore.Users[uuid].DisplayName, userStore.Users[uuid].LoggedIn, userStore.Users[uuid].AcceptingTrades);
            }

            return true;
        }
    }
}
