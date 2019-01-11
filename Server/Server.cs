using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Authentication;
using Chat;
using Connections;
using Trading;
using UserManagement;
using Utils;

namespace PhinixServer
{
    class Server
    {
        private const string CONFIG_FILE = "server.conf";

        public static Config Config;
        public static Logger Logger;
        public static NetServer Connections;
        public static ServerAuthenticator Authenticator;
        public static ServerUserManager UserManager;
        public static ServerChat Chat;
        public static ServerTrading Trading;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Server)).GetName().Version;

        private static bool exiting = false;

        static void Main()
        {
            Config = Config.Load(CONFIG_FILE);
            Logger = new Logger(Config.LogPath, Config.DisplayVerbosity, Config.LogVerbosity);
            
            // Set up module instances
            Connections = new NetServer(new IPEndPoint(Config.Address, Config.Port));
            Authenticator = new ServerAuthenticator(
                netServer: Connections,
                serverName: Config.ServerName,
                serverDescription: Config.ServerDescription,
                authType: Config.AuthType,
                credentialStorePath: Config.CredentialDatabasePath
            );
            UserManager = new ServerUserManager(Connections, Authenticator, Config.UserDatabasePath, Config.MaxDisplayNameLength);
            Chat = new ServerChat(Connections, Authenticator, UserManager, Config.ChatHistoryLength);
            Trading = new ServerTrading(Connections, Authenticator, UserManager);
            
            // Add handler for ILoggable modules
            Authenticator.OnLogEntry += ILoggableHandler;
            UserManager.OnLogEntry += ILoggableHandler;
            Chat.OnLogEntry += ILoggableHandler;
            Trading.OnLogEntry += ILoggableHandler;
            
            Connections.Start();

            Logger.Log(Verbosity.INFO, string.Format("Accepting auth type \"{0}\"", Config.AuthType.ToString()));
            Logger.Log(Verbosity.INFO, string.Format("Phinix server version {0} listening on port {1}", Version, Connections.Endpoint.Port));

            // Set up an exit condition
            Console.CancelKeyPress += shutdownHandler;
            
            CommandInterpreter interpreter = new CommandInterpreter();
            while (!exiting)
            {
                string line = Console.ReadLine();

                if (string.IsNullOrEmpty(line)) continue;

                List<string> arguments = new List<string>(line.Split(' '));
                string command = arguments.First();
                arguments.RemoveAt(0); // Remove the command from the argument list

                if (command == "exit") // Check this here to avoid other weird workarounds
                {
                    shutdownHandler();
                }
                else
                {
                    interpreter.Run(command, arguments);
                }
            }
        }

        /// <summary>
        /// Handles server shutdown events.
        /// Used to safely save and shut down modules before closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void shutdownHandler(object sender = null, EventArgs e = null)
        {
            Logger.Log(Verbosity.INFO, "Server shutting down");
            
            // Close all connections
            Connections.Stop();
            
            // Log everyone out and save user data
            UserManager.LogOutAll();
            UserManager.Save(Config.UserDatabasePath);
            
            // Save the config
            Config.Save(CONFIG_FILE);
            
            // Exit the main run loop
            exiting = true;
        }

        /// <summary>
        /// Handler for <c>ILoggable</c> <c>OnLogEvent</c> events.
        /// Raised by modules as a way to hook into the server log.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="args">Event arguments</param>
        private static void ILoggableHandler(object sender, LogEventArgs args)
        {
            switch (args.LogLevel)
            {
                case LogLevel.DEBUG:
                    Logger.Log(Verbosity.DEBUG, args.Message);
                    break;
                case LogLevel.WARNING:
                    Logger.Log(Verbosity.WARN, args.Message);
                    break;
                case LogLevel.ERROR:
                    Logger.Log(Verbosity.ERROR, args.Message);
                    break;
                case LogLevel.FATAL:
                    Logger.Log(Verbosity.FATAL, args.Message);
                    break;
                case LogLevel.INFO:
                default:
                    Logger.Log(Verbosity.INFO, args.Message);
                    break;
            }
        }
    }
}
