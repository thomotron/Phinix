using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using Connections;
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
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <c>NetServer</c> to send packets and bind events to.
        /// </summary>
        private NetServer netServer;
        
        /// <summary>
        /// Stores each user in an easily-serialisable format.
        /// </summary>
        private UserStore userStore;
        /// <summary>
        /// Lock for user store operations.
        /// </summary>
        private object userStoreLock = new object();

        /// <summary>
        /// Creates a new <c>ServerUserManager</c> instance.
        /// </summary>
        /// <param name="netServer"><c>NetServer</c> instance to bind packet handlers to</param>
        public ServerUserManager(NetServer netServer)
        {
            this.netServer = netServer;
            
            this.userStore = new UserStore();
        }

        /// <summary>
        /// Creates a new <c>ServerUserManager</c> instance and loads in the user store from the given path.
        /// </summary>
        /// <param name="netServer"><c>NetServer</c> instance to bind packet handlers to</param>
        /// <param name="userStorePath">Path to user store</param>
        public ServerUserManager(NetServer netServer, string userStorePath)
        {
            this.netServer = netServer;
            
            Load(userStorePath);
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
        /// Loads the user store from the given file path.
        /// Will create a new one if the file does not exist.
        /// </summary>
        /// <param name="filePath">User store file path</param>
        /// <exception cref="ArgumentException">File path cannot be null or empty</exception>
        public void Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Generate a fresh user store if it doesn't exist
            if (!File.Exists(filePath))
            {
                // Create the user store
                lock (userStoreLock) this.userStore = new UserStore();
                
                // Save it
                Save(filePath);
            }

            // Load the user store
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (CodedInputStream cis = new CodedInputStream(fs))
                {
                    lock (userStoreLock) this.userStore = UserStore.Parser.ParseFrom(cis);
                }
            }
        }
    }
}
