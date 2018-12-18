using System;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using Authentication;
using Chat;
using Connections;
using HugsLib;
using HugsLib.Settings;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient
{
    public class Client : ModBase
    {
        public static Client Instance;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Client)).GetName().Version;

        public override string ModIdentifier => "Phinix";

        private NetClient netClient;
        public bool Connected => netClient.Connected;
        public void Send(string module, byte[] serialisedMessage) => netClient.Send(module, serialisedMessage);
        public event EventHandler OnConnecting;
        public event EventHandler OnDisconnect;

        private ClientAuthenticator authenticator;
        public bool Authenticated => authenticator.Authenticated;
        public string SessionId => authenticator.SessionId;
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationSuccess;
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationFailure;

        private ClientUserManager userManager;
        public bool LoggedIn => userManager.LoggedIn;
        public string Uuid => userManager.Uuid;
        public bool TryGetDisplayName(string uuid, out string displayName) => userManager.TryGetDisplayName(uuid, out displayName);
        public string[] GetUserUuids(bool loggedIn = false) => userManager.GetUuids(loggedIn); 
        public event EventHandler<LoginEventArgs> OnLoginSuccess;
        public event EventHandler<LoginEventArgs> OnLoginFailure;

        public bool Online => Connected && Authenticated && LoggedIn;

        private ClientChat chat;
        public void SendMessage(string message) => chat.Send(message);
        public event EventHandler<ChatMessageEventArgs> OnChatMessageReceived;

        private SettingHandle<string> serverAddressHandle;
        public string ServerAddress
        {
            get => serverAddressHandle.Value;
            set
            {
                serverAddressHandle.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<int> serverPortHandle;
        public int ServerPort
        {
            get => serverPortHandle.Value;
            set
            {
                serverPortHandle.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<string> displayNameHandle;
        public string DisplayName
        {
            get => displayNameHandle.Value;
            set
            {
                displayNameHandle.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Called by HugsLib shortly after the mod is loaded.
        /// Used for initial setup only.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Client.Instance = this;

            // Load in Settings
            serverAddressHandle = Settings.GetHandle(
                settingName: "serverAddress",
                title: "Phinix_hugslibsettings_serverAddressTitle".Translate(),
                description: null,
                defaultValue: "localhost"
            );
            serverPortHandle = Settings.GetHandle(
                settingName: "serverPort",
                title: "Phinix_hugslibsettings_serverPortTitle".Translate(),
                description: null,
                defaultValue: 16180,
                validator: value => int.TryParse(value, out _)
            );
            displayNameHandle = Settings.GetHandle(
                settingName: "displayName",
                title: "Phinix_hugslibsettings_displayNameTitle".Translate(),
                description: null,
                defaultValue: SteamUtility.SteamPersonaName
            );

            // Set up our module instances
            this.netClient = new NetClient();
            this.authenticator = new ClientAuthenticator(netClient, getCredentials);
            this.userManager = new ClientUserManager(netClient, authenticator, DisplayName);
            this.chat = new ClientChat(netClient, authenticator, userManager);
            
            // Subscribe to log events
            authenticator.OnLogEntry += ILoggableHandler;
            userManager.OnLogEntry += ILoggableHandler;
            chat.OnLogEntry += ILoggableHandler;
            
            // Subscribe to authentication events
            authenticator.OnAuthenticationSuccess += (sender, args) =>
            {
                Logger.Message("Successfully authenticated with server.");
                userManager.SendLogin();
            };
            authenticator.OnAuthenticationFailure += (sender, args) =>
            {
                Logger.Message("Failed to authenticate with server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString());
            };
            
            // Subscribe to user management events
            userManager.OnLoginSuccess += (sender, args) =>
            {
                Logger.Message("Successfully logged in with UUID {0}", userManager.Uuid);
            };
            userManager.OnLoginFailure += (sender, args) =>
            {
                Logger.Message("Failed to log in to server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString());
            };
            userManager.OnUserChanged += (sender, args) =>
            {
                // Check if we have been updated
                if (userManager.LoggedIn && args.Uuid == userManager.Uuid)
                {
                    // Update our display name
                    DisplayName = args.DisplayName;
                }
            };
            
            // Subscribe to chat events
            chat.OnChatMessageReceived += (sender, args) =>
            {
                Logger.Trace("Received chat message from UUID " + args.OriginUuid);
            };
            
            // Forward events so the UI can handle them
            netClient.OnConnecting += (sender, e) => { OnConnecting?.Invoke(sender, e); };
            netClient.OnDisconnect += (sender, e) => { OnDisconnect?.Invoke(sender, e); };
            authenticator.OnAuthenticationSuccess += (sender, e) => { OnAuthenticationSuccess?.Invoke(sender, e); };
            authenticator.OnAuthenticationFailure += (sender, e) => { OnAuthenticationFailure?.Invoke(sender, e); };
            userManager.OnLoginSuccess += (sender, e) => { OnLoginSuccess?.Invoke(sender, e); };
            userManager.OnLoginFailure += (sender, e) => { OnLoginFailure?.Invoke(sender, e); };
            chat.OnChatMessageReceived += (sender, e) => { OnChatMessageReceived?.Invoke(sender, e); };
            
            // Connect to the server set in the config
            Connect(ServerAddress, ServerPort);
        }

        /// <summary>
        /// Attempts to connect to the server at the given address and port.
        /// This will disconnect from the current server, if any.
        /// </summary>
        /// <param name="address">Server address</param>
        /// <param name="port">Server port</param>
        public void Connect(string address, int port)
        {
            if (Connected) Disconnect();

            try
            {
                netClient.Connect(address, port);
            }
            catch
            {
                Logger.Message("Could not connect to {0}:{1}", ServerAddress, ServerPort);
            }
        }

        /// <summary>
        /// If connected, disconnects from the current server.
        /// </summary>
        public void Disconnect()
        {
            netClient.Disconnect();
        }

        /// <summary>
        /// Updates the user's display name locally and on the server.
        /// </summary>
        /// <param name="displayName">Display name</param>
        public void UpdateDisplayName(string displayName)
        {
            // Try to update within the user manager
            userManager.UpdateSelf(displayName);
        }
        
        /// <summary>
        /// Handler for <c>ILoggable</c> <c>OnLogEvent</c> events.
        /// Raised by modules as a way to hook into the HugsLib log.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="args">Event arguments</param>
        private void ILoggableHandler(object sender, LogEventArgs args)
        {
            switch (args.LogLevel)
            {
                case LogLevel.DEBUG:
                    Logger.Trace(args.Message);
                    break;
                case LogLevel.WARNING:
                    Logger.Warning(args.Message);
                    break;
                case LogLevel.ERROR:
                case LogLevel.FATAL:
                    Logger.Error(args.Message);
                    break;
                case LogLevel.INFO:
                default:
                    Logger.Message(args.Message);
                    break;
            }
        }
        
        /// <summary>
        /// Handles credential requests from the <c>ClientAuthenticator</c> module.
        /// This forwards the server details and a callback to the GUI for user input.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="serverName">Server name</param>
        /// <param name="serverDescription">Server description</param>
        /// <param name="authType">Authentication type</param>
        /// <param name="callback">Callback delegate to pass entered credentials to</param>
        private void getCredentials(string sessionId, string serverName, string serverDescription, AuthTypes authType, ClientAuthenticator.ReturnCredentialsDelegate callback)
        {
            Logger.Trace("Authentication needs more credentials for the server \"{0}\" with authentication type \"{1}\"", serverName, authType.ToString());
            
            Find.WindowStack.Add(new CredentialsWindow
            {
                SessionId = sessionId,
                ServerName = serverName,
                ServerDescription = serverDescription,
                AuthType = authType,
                CredentialsCallback = callback
            });
        }
    }
}
