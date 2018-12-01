using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Authentication;
using Connections;
using UserManagement;
using Utils;

namespace PhinixServer
{
    class Server
    {
        private const string CONFIG_FILE = "server.conf";

        public static Config Config;
        public static Logger Logger;
        public static ServerUserManager UserManager;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Server)).GetName().Version;

        private static bool exiting = false;

        static void Main()
        {
            Config = Config.Load(CONFIG_FILE);
            Logger = new Logger(Config.LogPath, Config.DisplayVerbosity, Config.LogVerbosity);
            
            // Set up module instances
            NetServer connections = new NetServer(new IPEndPoint(Config.Address, Config.Port));
            ServerAuthenticator authenticator = new ServerAuthenticator(
                netServer: connections,
                serverName: Config.ServerName,
                serverDescription: Config.ServerDescription,
                authType: Config.AuthType,
                credentialStorePath: Config.CredentialDatabasePath
            );
            UserManager = new ServerUserManager(connections, authenticator, Config.UserDatabasePath);
            
            // Add handler for ILoggable modules
            authenticator.OnLogEntry += ILoggableHandler;
            
            connections.Start();

            Logger.Log(Verbosity.INFO, string.Format("Accepting auth type \"{0}\"", Config.AuthType.ToString()));
            Logger.Log(Verbosity.INFO, string.Format("Phinix server version {0} listening on port {1}", Version, connections.Endpoint.Port));

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

        private static void shutdownHandler(object sender = null, EventArgs e = null)
        {
            Logger.Log(Verbosity.INFO, "Server shutting down");
            UserManager.Save(Config.UserDatabasePath);
            Config.Save(CONFIG_FILE);
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
