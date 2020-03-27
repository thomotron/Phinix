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
        // Default config file constant
        private const string CONFIG_FILE = "server.conf";

        public static readonly Version Version = Assembly.GetAssembly(typeof(Server)).GetName().Version;

        // Module instance variables
        public static Config Config;
        public static Logger Logger;
        public static NetServer Connections;
        public static ServerAuthenticator Authenticator;
        public static ServerUserManager UserManager;
        public static ServerChat Chat;
        public static ServerTrading Trading;

        // Exiting flag to stop the main run loop
        private static bool exiting = false;

        static void Main()
        {
            // Read in the config and initialise the logging module
            Config = Config.Load(CONFIG_FILE);
            Logger = new Logger(Config.LogPath, Config.DisplayVerbosity, Config.LogVerbosity);

            // Set up module instances
            Connections = new NetServer(new IPEndPoint(Config.Address, Config.Port), Config.MaxConnections);
            Authenticator = new ServerAuthenticator(
                netServer: Connections,
                serverName: Config.ServerName,
                serverDescription: Config.ServerDescription,
                authType: Config.AuthType,
                credentialStorePath: Config.CredentialDatabasePath
            );
            UserManager = new ServerUserManager(Connections, Authenticator, Config.UserDatabasePath, Config.MaxDisplayNameLength);
            Chat = new ServerChat(Connections, Authenticator, UserManager, Config.ChatHistoryLength, Config.ChatHistoryPath);
            Trading = new ServerTrading(Connections, Authenticator, UserManager);

            // Add handler for ILoggable modules
            Authenticator.OnLogEntry += ILoggableHandler;
            UserManager.OnLogEntry += ILoggableHandler;
            Chat.OnLogEntry += ILoggableHandler;
            Trading.OnLogEntry += ILoggableHandler;

            // Start listening for connections
            Connections.Start();

            // State our auth type and where we're listening from
            Logger.Log(Verbosity.INFO, string.Format("Accepting auth type \"{0}\"", Config.AuthType.ToString()));
            Logger.Log(Verbosity.INFO, string.Format("Phinix server version {0} listening on {1}:{2}", Version, Connections.Endpoint.Address, Connections.Endpoint.Port));

            // Set up an exit condition on SIGINT/Ctrl+C
            Console.CancelKeyPress += shutdownHandler;

            // Start interpreting commands
            CommandInterpreter interpreter = new CommandInterpreter();
            while (!exiting)
            {
                // Wait until we've got a command to interpret
                string line = Console.ReadLine();

                // Don't do anything if the command is empty
                if (string.IsNullOrEmpty(line)) continue;

                // Break the line down into the command and its arguments
                List<string> arguments = new List<string>(line.Split(' '));
                string command = arguments.First();
                arguments.RemoveAt(0); // Remove the command from the argument list

                // Check if we've been given the exit command
                // This is checked here to avoid weird workarounds from the interpreter
                if (command == "exit")
                {
                    shutdownHandler();
                    break;
                }

                // Interpret the command and its arguments
                interpreter.Run(command, arguments);
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

            // Save the chat history
            Chat.SaveChatHistory(Config.ChatHistoryPath);

            // Save the config
            Config.Save(CONFIG_FILE);

            // Exit the main run loop
            exiting = true;
        }

        /// <summary>
        /// Handler for <see cref="ILoggable"/> <c>OnLogEvent</c> events.
        /// Raised by modules as a way to hook into the server log.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="args">Event arguments</param>
        private static void ILoggableHandler(object sender, LogEventArgs args)
        {
            Verbosity verbosity;
            switch (args.LogLevel)
            {
                case LogLevel.DEBUG:
                    verbosity = Verbosity.DEBUG;
                    break;
                case LogLevel.WARNING:
                    verbosity = Verbosity.WARN;
                    break;
                case LogLevel.ERROR:
                    verbosity = Verbosity.ERROR;
                    break;
                case LogLevel.FATAL:
                    verbosity = Verbosity.FATAL;
                    break;
                case LogLevel.INFO:
                default:
                    verbosity = Verbosity.INFO;
                    break;
            }

            Logger.Log(verbosity, args.Message, sender.GetType().Namespace);
        }
    }
}
