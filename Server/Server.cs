using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Timers;
using Agora;
using Hermes;
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
        public static ServerAgora Agora;
        public static ServerHermes Hermes;
        public static ServerScreenshots Screenshots;

        // Exiting flag to stop the main run loop
        private static bool exiting = false;

        // Save timer
        private static Timer saveTimer;

        /// <summary>
        /// One-shot timer for saves modules ask for after items change hands (see <see cref="SaveRequests"/>). Waiting
        /// a second turns a burst of requests into one save.
        /// </summary>
        private static Timer requestedSaveTimer;

        /// <summary>
        /// Only one save at a time, so the regular save and a requested one can't write the same file at once.
        /// </summary>
        private static readonly object saveLock = new object();

        // Called by Windows when the console window is closed (see SetConsoleCtrlHandler below)
        private delegate bool ConsoleCtrlHandler(int ctrlType);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);
        // Kept in a field so the garbage collector doesn't throw it away while Windows still needs it
        private static ConsoleCtrlHandler consoleCtrlHandler;

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
            UserManager = new ServerUserManager(Connections, Authenticator, Config.MaxDisplayNameLength) { CompanionMode = Config.CompanionMode };

            // Tell the mod which features are switched off, so it hides them
            if (!Config.EnableMarket) UserManager.DisabledFeatures.Add(UserManagement.Features.MARKET);
            if (!Config.EnableBounties) UserManager.DisabledFeatures.Add(UserManagement.Features.BOUNTIES);
            if (!Config.EnableGifts) UserManager.DisabledFeatures.Add(UserManagement.Features.GIFTS);
            if (!Config.EnablePawnTrading) UserManager.DisabledFeatures.Add(UserManagement.Features.PAWN_TRADING);
            if (!Config.EnableProfiles) UserManager.DisabledFeatures.Add(UserManagement.Features.PROFILES);
            if (!Config.EnableFriends) UserManager.DisabledFeatures.Add(UserManagement.Features.FRIENDS);
            if (!Config.EnableEmojis) UserManager.DisabledFeatures.Add(UserManagement.Features.EMOJIS);
            Chat = new ServerChat(Connections, Authenticator, UserManager, Config.ChatHistoryLength, Config.ChatRateLimitMessages, Config.ChatRateLimitSeconds);
            Trading = new ServerTrading(Connections, Authenticator, UserManager, Config.TradeRateLimitTrades, Config.TradeRateLimitSeconds);
            Agora = new ServerAgora(Connections, Authenticator, UserManager, Config.MarketMaxStockPerShop, Config.MarketPostRateLimitPosts, Config.MarketPostRateLimitSeconds, Config.BountyMaxPerUser, Config.BountyExpiryDays, Config.ContractMaxPerUser, Config.EnableContracts);
            // Private messages share chat's rate limit settings and gifts share the market's, so there's nothing new to configure
            Hermes = new ServerHermes(Connections, Authenticator, UserManager, Config.ChatRateLimitMessages, Config.ChatRateLimitSeconds, Config.MarketPostRateLimitPosts, Config.MarketPostRateLimitSeconds, Config.EnableDirectMessages);
            // Screenshots share the market's rate limit settings, like gifts
            Screenshots = new ServerScreenshots(Connections, Authenticator, UserManager, Config.EnableScreenshots, Config.ScreenshotLifetimeMinutes, Config.MarketPostRateLimitPosts, Config.MarketPostRateLimitSeconds);

            // Add handler for ILoggable modules
            Authenticator.OnLogEntry += ILoggableHandler;
            UserManager.OnLogEntry += ILoggableHandler;
            Chat.OnLogEntry += ILoggableHandler;
            Trading.OnLogEntry += ILoggableHandler;
            Agora.OnLogEntry += ILoggableHandler;
            Hermes.OnLogEntry += ILoggableHandler;
            Screenshots.OnLogEntry += ILoggableHandler;

            // Load saved data
            Authenticator.Load(Config.CredentialDatabasePath);
            UserManager.Load(Config.UserDatabasePath);
            if (Config.SaveChatHistory) Chat.Load(Config.ChatHistoryPath);
            Trading.Load(Config.TradeDatabasePath);
            Agora.Load(Config.BountyDatabasePath);
            Hermes.Load(Config.HermesDatabasePath);

            // Set up the save timer
            saveTimer = new Timer
            {
                AutoReset = true,
                Enabled = true,
                Interval = Config.SaveInterval
            };
            saveTimer.Elapsed += saveHandler;
            saveTimer.Start();

            // Save about a second after a module takes or hands over items, instead of waiting for the regular save
            requestedSaveTimer = new Timer { AutoReset = false, Interval = 1000 };
            requestedSaveTimer.Elapsed += (sender, e) => saveModules();
            SaveRequests.Requested += () => requestedSaveTimer.Start();

            // Closing the console window used to skip the final save. Windows gives a few seconds to finish, which is
            // plenty. Other systems don't have this call, and there Ctrl+C or "stop" still save
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                consoleCtrlHandler = ctrlType =>
                {
                    // 2 is CTRL_CLOSE_EVENT; Ctrl+C (0) is already handled by CancelKeyPress
                    if (ctrlType == 2 && !exiting) shutdownHandler();
                    return false;
                };
                try
                {
                    SetConsoleCtrlHandler(consoleCtrlHandler, true);
                }
                catch (Exception e)
                {
                    Logger.Log(Verbosity.WARN, "Couldn't watch for the console window closing: " + e.Message);
                }
            }

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
            saveModules();

            // Save config too
            Config.Save(CONFIG_FILE);

            Logger.Log(Verbosity.INFO, "Saved module states");
        }

        /// <summary>
        /// Saves every module's data, one save at a time. A module that fails to save is logged and the rest still save.
        /// </summary>
        private static void saveModules()
        {
            lock (saveLock)
            {
                trySave("credentials", () => Authenticator.Save(Config.CredentialDatabasePath));
                trySave("users", () => UserManager.Save(Config.UserDatabasePath));
                if (Config.SaveChatHistory) trySave("chat history", () => Chat.Save(Config.ChatHistoryPath));
                trySave("trades", () => Trading.Save(Config.TradeDatabasePath));
                trySave("markets and bounties", () => Agora.Save(Config.BountyDatabasePath));
                trySave("gifts", () => Hermes.Save(Config.HermesDatabasePath));
            }
        }

        private static void trySave(string what, Action save)
        {
            try
            {
                save();
            }
            catch (Exception e)
            {
                Logger.Log(Verbosity.ERROR, string.Format("Couldn't save {0}: {1}", what, e.Message));
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

            // Stop the save timer
            saveTimer.Stop();

            // Log everyone out
            UserManager.LogOutAll();

            // Close all connections
            Connections.Stop();

            // Save module states
            requestedSaveTimer.Stop();
            saveModules();

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
