using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using Google.Protobuf;
using Utils;

namespace UserManagement
{
    /// <inheritdoc />
    /// <summary>
    /// Server-side variant of <c>UserManager</c>.
    /// Used to store details of each user and oversees user login.
    /// </summary>
    public class ServerUserManager : UserManager
    {
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        private UserStore userStore;
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        private object userStoreLock = new object();

        public ServerUserManager()
        {
            this.userStore = new UserStore();
        }

        private ServerUserManager(UserStore userStore)
        {
            this.userStore = userStore;
        }
        
        /// <summary>
        /// Saves the user store at the given path.
        /// This will overwrite the file if it already exists.
        /// </summary>
        /// <param name="filePath">Destination file path</param>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Write the user store
            using (FileStream fs = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (CodedOutputStream cos = new CodedOutputStream(fs))
                {
                    lock (userStoreLock)
                    {
                        userStore.WriteTo(cos);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the user store from the given file path into a new <c>ServerUserManager</c> instance.
        /// Will return a default <c>ServerUserManager</c> if the file does not exist.
        /// </summary>
        /// <param name="filePath">User store file path</param>
        /// <returns>Loaded <c>ServerUserManager</c> object</returns>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public static ServerUserManager Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Generate a fresh new UserManager class if it doesn't exist
            if (!File.Exists(filePath))
            {
                ServerUserManager newUserManager = new ServerUserManager();
                newUserManager.Save(filePath);
                return newUserManager;
            }

            // Load the user store
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (CodedInputStream cis = new CodedInputStream(fs))
                {
                    return new ServerUserManager(UserStore.Parser.ParseFrom(cis));
                }
            }
        }
    }
}
