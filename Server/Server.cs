using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Timers;
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

        // Save timer
        private static Timer saveTimer;

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
                authType: Config.AuthType
            );
            UserManager = new ServerUserManager(Connections, Authenticator, Config.MaxDisplayNameLength);
            Chat = new ServerChat(Connections, Authenticator, UserManager, Config.ChatHistoryLength);
            Trading = new ServerTrading(Connections, Authenticator, UserManager);

            // Add handler for ILoggable modules
            Authenticator.OnLogEntry += ILoggableHandler;
            UserManager.OnLogEntry += ILoggableHandler;
            Chat.OnLogEntry += ILoggableHandler;
            Trading.OnLogEntry += ILoggableHandler;

            // Load saved data
            Authenticator.Load(Config.CredentialDatabasePath);
            UserManager.Load(Config.UserDatabasePath);
            Chat.Load(Config.ChatHistoryPath);
            Trading.Load(Config.TradeDatabasePath);

            // Set up the save timer
            saveTimer = new Timer
            {
                AutoReset = true,
                Enabled = true,
                Interval = Config.SaveInterval
            };
            saveTimer.Elapsed += saveHandler;
            saveTimer.Start();

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
                if (command == "exit" || command == "quit" || command == "stop")
                {
                    shutdownHandler();
                    break;
                }

                // Interpret the command and its arguments
                interpreter.Run(command, arguments);
            }
        }

        /// <summary>
        /// Handles save events.
        /// Used by <see cref="saveTimer"/> to save each module state periodically.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void saveHandler(object sender, ElapsedEventArgs e)
        {
            Logger.Log(Verbosity.INFO, "Saving module states...");

            // Save module states
            Authenticator.Save(Config.CredentialDatabasePath);
            UserManager.Save(Config.UserDatabasePath);
            Chat.Save(Config.ChatHistoryPath);
            Trading.Save(Config.TradeDatabasePath);

            // Save config too
            Config.Save(CONFIG_FILE);

            Logger.Log(Verbosity.INFO, "Saved module states");
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

            // Stop the save timer
            saveTimer.Stop();

            // Log everyone out
            UserManager.LogOutAll();

            // Close all connections
            Connections.Stop();

            // Save module states
            Authenticator.Save(Config.CredentialDatabasePath);
            UserManager.Save(Config.UserDatabasePath);
            Chat.Save(Config.ChatHistoryPath);
            Trading.Save(Config.TradeDatabasePath);

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
