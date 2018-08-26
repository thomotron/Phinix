using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace UserManagement
{
    [DataContract]
    public class UserManager
    {
        public static readonly Version Version = Assembly.GetAssembly(typeof(UserManager)).GetName().Version;

        /// <summary>
        /// A dictionary containing every user's UUID and <c>User</c> object.
        /// </summary>
        [DataMember]
        private Dictionary<string, User> userDictionary;

        /// <summary>
        /// Instantiates a new <c>UserManager</c> class.
        /// </summary>
        public UserManager()
        {
            this.userDictionary = new Dictionary<string, User>();
        }

        /// <summary>
        /// Saves the <c>UserManager</c> object to an XML document at the given path.
        /// This will overwrite the file if it already exists.
        /// </summary>
        /// <param name="filePath">Destination file path</param>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                new DataContractSerializer(typeof(UserManager)).WriteObject(writer, this);
            }
        }

        /// <summary>
        /// Loads a <c>UserManager</c> object from the given file path. Will return a default <c>UserManager</c> if the file does not exist.
        /// </summary>
        /// <param name="filePath">UserManager file path</param>
        /// <returns>Loaded <c>UserManager</c> object</returns>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public static UserManager Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Generate a fresh new UserManager class if it doesn't exist
            if (!File.Exists(filePath))
            {
                UserManager userManager = new UserManager();
                userManager.Save(filePath); // Save it first to make sure it's present next time
                return userManager;
            }

            UserManager result;

            using (XmlReader reader = XmlReader.Create(filePath))
            {
                result = new DataContractSerializer(typeof(UserManager)).ReadObject(reader) as UserManager;
            }

            return result;
        }

        /// <summary>
        /// Tries to get a user from the user list by their UUID and returns whether the user was retrieved successfully.
        /// <c>User</c>s returned by this method should not be modified as the changes will not be saved. Please use <see cref="UpdateUser"/> instead.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        /// <param name="user">User output</param>
        /// <returns>Successfully retrieved user</returns>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public bool TryGetUser(string uuid, out User user)
        {
            if (string.IsNullOrEmpty(uuid)) throw new ArgumentException("UUID cannot be null or empty.", nameof(uuid));

            return userDictionary.TryGetValue(uuid, out user);
        }

        /// <summary>
        /// Registers a new user.
        /// Returns true if the user was added successfully, otherwise false.
        /// </summary>
        /// <param name="user">User</param>
        /// <returns>User added successfully</returns>
        /// <exception cref="ArgumentNullException">User cannot be null</exception>
        public bool AddUser(User user)
        {
            if (user is null) throw new ArgumentNullException(nameof(user), "User cannot be null.");

            if (userDictionary.ContainsKey(user.Uuid)) return false;

            userDictionary.Add(user.Uuid, user);
            return true;
        }

        /// <summary>
        /// Replaces an existing user.
        /// Returns true if the update was successful, otherwise false.
        /// </summary>
        /// <param name="user">User</param>
        /// <returns>User updated successfully</returns>
        /// <exception cref="ArgumentNullException">User cannot be null</exception>
        public bool UpdateUser(User user)
        {
            if (user is null) throw new ArgumentNullException(nameof(user), "User cannot be null.");

            if (userDictionary.ContainsKey(user.Uuid))
            {
                userDictionary[user.Uuid] = user;
                return true;
            }

            return false;
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

            return userDictionary.Remove(uuid);
        }
    }
}
