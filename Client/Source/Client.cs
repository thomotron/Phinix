using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using Authentication;
using Chat;
using Connections;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using Trading;
using HugsLib.Utils;
using UserManagement;
using Utils;
using Verse;
using Thing = Verse.Thing;

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
        public ChatMessage[] GetChatMessages() => chat.GetMessages();
        public event EventHandler<ChatMessageEventArgs> OnChatMessageReceived;

        private ClientTrading trading;
        public void CreateTrade(string uuid) => trading.CreateTrade(uuid);
        public void CancelTrade(string tradeId) => trading.CancelTrade(tradeId);
        public string[] GetTrades() => trading.GetTrades();
        public bool TryGetOtherPartyUuid(string tradeId, out string otherPartyUuid) => trading.TryGetOtherPartyUuid(tradeId, out otherPartyUuid);
        public bool TryGetOtherPartyAccepted(string tradeId, out bool otherPartyAccepted) => trading.TryGetOtherPartyAccepted(tradeId, out otherPartyAccepted);
        public bool TryGetPartyAccepted(string tradeId, string partyUuid, out bool accepted) => trading.TryGetPartyAccepted(tradeId, partyUuid, out accepted);
        public bool TryGetItemsOnOffer(string tradeId, string uuid, out IEnumerable<Trading.ProtoThing> items) => trading.TryGetItemsOnOffer(tradeId, uuid, out items);
        public void UpdateTradeItems(string tradeId, IEnumerable<ProtoThing> items) => trading.UpdateItems(tradeId, items);
        public void UpdateTradeStatus(string tradeId, bool? accepted = null, bool? cancelled = null) => trading.UpdateStatus(tradeId, accepted, cancelled);
        public event EventHandler<CreateTradeEventArgs> OnTradeCreationSuccess;
        public event EventHandler<CreateTradeEventArgs> OnTradeCreationFailure;
        public event EventHandler<CompleteTradeEventArgs> OnTradeCompleted;
        public event EventHandler<CompleteTradeEventArgs> OnTradeCancelled;

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

        private SettingHandle<bool> acceptingTradesHandle;
        public bool AcceptingTrades
        {
            get => acceptingTradesHandle.Value;
            set
            {
                acceptingTradesHandle.Value = value;
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
                defaultValue: "phinix.chat"
            );
            serverPortHandle = Settings.GetHandle(
                settingName: "serverPort",
                title: "Phinix_hugslibsettings_serverPortTitle".Translate(),
                description: null,
                defaultValue: 16200,
                validator: value => int.TryParse(value, out _)
            );
            displayNameHandle = Settings.GetHandle(
                settingName: "displayName",
                title: "Phinix_hugslibsettings_displayNameTitle".Translate(),
                description: null,
                defaultValue: SteamUtility.SteamPersonaName
            );
            acceptingTradesHandle = Settings.GetHandle(
                settingName: "acceptingTrades",
                title: "Phinix_hugslibsettings_acceptingTradesTitle",
                description: null,
                defaultValue: true
            );

            // Set up our module instances
            this.netClient = new NetClient();
            this.authenticator = new ClientAuthenticator(netClient, getCredentials);
            this.userManager = new ClientUserManager(netClient, authenticator);
            this.chat = new ClientChat(netClient, authenticator, userManager);
            this.trading = new ClientTrading(netClient, authenticator, userManager);
            
            // Subscribe to log events
            authenticator.OnLogEntry += ILoggableHandler;
            userManager.OnLogEntry += ILoggableHandler;
            chat.OnLogEntry += ILoggableHandler;
            trading.OnLogEntry += ILoggableHandler;
            
            // Subscribe to authentication events
            authenticator.OnAuthenticationSuccess += (sender, args) =>
            {
                Logger.Message("Successfully authenticated with server.");
                userManager.SendLogin(
                    displayName: DisplayName,
                    acceptingTrades: AcceptingTrades
                );
            };
            authenticator.OnAuthenticationFailure += (sender, args) =>
            {
                Logger.Message("Failed to authenticate with server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString());
                
                Find.WindowStack.Add(new Dialog_Message("Phinix_error_authFailedTitle".Translate(), "Phinix_error_authFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));
                
                Disconnect();
            };
            
            // Subscribe to user management events
            userManager.OnLoginSuccess += (sender, args) =>
            {
                Logger.Message("Successfully logged in with UUID {0}", userManager.Uuid);
            };
            userManager.OnLoginFailure += (sender, args) =>
            {
                Logger.Message("Failed to log in to server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString());
                
                Find.WindowStack.Add(new Dialog_Message("Phinix_error_loginFailedTitle".Translate(), "Phinix_error_loginFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));
                
                Disconnect();
            };
            
            // Subscribe to chat events
            chat.OnChatMessageReceived += (sender, args) =>
            {
                Logger.Trace("Received chat message from UUID " + args.OriginUuid);
            };
            
            // Subscribe to trading events
            trading.OnTradeCreationSuccess += (sender, args) =>
            {
                Logger.Trace(string.Format("Created trade {0} with {1}", args.TradeId, args.OtherPartyUuid));
                
                // Try get the other party's display name
                if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                {
                    // Strip formatting
                    displayName = TextHelper.StripRichText(displayName);
                }
                else
                {
                    // Unknown display name, default to ???
                    displayName = "???";
                }
                
                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeCreated");
                Find.LetterStack.ReceiveLetter(
                    label: "Phinix_trade_tradeReceivedLetter_label".Translate(displayName),
                    text: "Phinix_trade_tradeReceivedLetter_description".Translate(displayName),
                    textLetterDef: letterDef
                );
            };
            trading.OnTradeCreationFailure += (sender, args) =>
            {
                Logger.Trace(string.Format("Failed to create trade with {0}: {1} ({2})", args.OtherPartyUuid, args.FailureMessage, args.FailureReason.ToString()));
                
                Find.WindowStack.Add(new Dialog_Message("Phinix_error_tradeCreationFailedTitle".Translate(), "Phinix_error_tradeCreationFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));
            };
            trading.OnTradeCompleted += (sender, args) =>
            {
                // Try get the other party's display name
                if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                {
                    // Strip formatting
                    displayName = TextHelper.StripRichText(displayName);
                }
                else
                {
                    // Unknown display name, default to ???
                    displayName = "???";
                }
            
                // Convert all the received items into their Verse counterparts and strip out any unknown ones
                //// While it would be less computationally-expensive to strip out unknown items beforehand, we would
                //// have no idea whether we could actually make the item without another check, so we just piggy-back
                //// off of the converter's checks and strip them out afterward.
                Verse.Thing[] verseItems = args.Items
                                                .Select(TradingThingConverter.ConvertThingFromProtoOrUnknown)
                                                .Where(thing => thing.def.defName != "UnknownItem")
                                                .ToArray();
                

                // Launch drop pods to a trade spot on a home tile
                Map map = Find.AnyPlayerHomeMap;
                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(dropSpot, map, verseItems, canRoofPunch: false);
            
                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeAccepted");
                Find.LetterStack.ReceiveLetter("Trade success", string.Format("The trade with {0} was successful", displayName), letterDef, new LookTargets(dropSpot, map));
                
                Logger.Trace(string.Format("Trade with {0} completed successfully", args.OtherPartyUuid));
            };
            trading.OnTradeCancelled += (sender, args) =>
            {
                // Try get the other party's display name
                if (userManager.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                {
                    // Strip formatting
                    displayName = TextHelper.StripRichText(displayName);
                }
                else
                {
                    // Unknown display name, default to ???
                    displayName = "???";
                }

                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeCancelled");
                Find.LetterStack.ReceiveLetter("Trade cancelled", string.Format("The trade with {0} was cancelled", displayName), letterDef);
                
                Logger.Trace(string.Format("Trade with {0} cancelled", args.OtherPartyUuid));
            };
            
            // Forward events so the UI can handle them
            netClient.OnConnecting += (sender, e) => { OnConnecting?.Invoke(sender, e); };
            netClient.OnDisconnect += (sender, e) => { OnDisconnect?.Invoke(sender, e); };
            authenticator.OnAuthenticationSuccess += (sender, e) => { OnAuthenticationSuccess?.Invoke(sender, e); };
            authenticator.OnAuthenticationFailure += (sender, e) => { OnAuthenticationFailure?.Invoke(sender, e); };
            userManager.OnLoginSuccess += (sender, e) => { OnLoginSuccess?.Invoke(sender, e); };
            userManager.OnLoginFailure += (sender, e) => { OnLoginFailure?.Invoke(sender, e); };
            chat.OnChatMessageReceived += (sender, e) => { OnChatMessageReceived?.Invoke(sender, e); };
            trading.OnTradeCreationSuccess += (sender, e) => { OnTradeCreationSuccess?.Invoke(sender, e); };
            trading.OnTradeCreationFailure += (sender, e) => { OnTradeCreationFailure?.Invoke(sender, e); };
            trading.OnTradeCompleted += (sender, e) => { OnTradeCompleted?.Invoke(sender, e); };
            trading.OnTradeCancelled += (sender, e) => { OnTradeCancelled?.Invoke(sender, e); };
            
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
                
                Find.WindowStack.Add(new Dialog_Message("Phinix_error_connectionFailedTitle".Translate(), "Phinix_error_connectionFailedMessage".Translate(ServerAddress, ServerPort)));
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
