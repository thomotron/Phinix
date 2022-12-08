using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Verse.Sound;
using Thing = Verse.Thing;

namespace PhinixClient
{
    public class Client : ModBase
    {
        public static Client Instance;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Client)).GetName().Version;
        public void Log(LogEventArgs e) => ILoggableHandler(null, e);

        public override string ModIdentifier => "Phinix";

        #region Modules
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
        public bool TryGetUser(string uuid, out ImmutableUser user) => userManager.TryGetUser(uuid, out user);
        public string[] GetUserUuids(bool loggedIn = false) => userManager.GetUuids(loggedIn);
        public ImmutableUser[] GetUsers(bool loggedIn = false) => userManager.GetUsers(loggedIn);
        public event EventHandler<LoginEventArgs> OnLoginSuccess;
        public event EventHandler<LoginEventArgs> OnLoginFailure;
        public event EventHandler<UserDisplayNameChangedEventArgs> OnUserDisplayNameChanged;
        public event EventHandler<UserLoginStateChangedEventArgs> OnUserLoggedIn;
        public event EventHandler<UserLoginStateChangedEventArgs> OnUserLoggedOut;
        public event EventHandler<UserCreatedEventArgs> OnUserCreated;
        public event EventHandler OnUserSync;

        public bool Online => Connected && Authenticated && LoggedIn;

        private ClientChat chat;
        public void SendMessage(string message) => chat.Send(message);
        public void MarkAsRead() => chat.MarkAsRead();
        public UIChatMessage[] GetUnreadChatMessages(bool markAsRead = true) => GetChatMessages(markAsRead, true);
        public int UnreadMessages => chat.UnreadMessages;
        public int UnreadMessagesExcludingBlocked => chat.GetUnreadMessagesExcluding(BlockedUsers);
        public event EventHandler<UIChatMessageEventArgs> OnChatMessageReceived;
        public event EventHandler OnChatSync;

        private ClientTrading trading;
        public void CreateTrade(string uuid) => trading.CreateTrade(uuid);
        public void CancelTrade(string tradeId) => trading.CancelTrade(tradeId);
        public string[] GetTradeIds() => trading.GetTradeIds();
        public string[] GetTradeIdsExceptWith(IEnumerable<string> otherPartyUuids) => trading.GetTradeIdsExceptWith(otherPartyUuids);
        public ImmutableTrade[] GetTrades() => trading.GetTrades();
        public ImmutableTrade[] GetTradesExceptWith(IEnumerable<string> otherPartyUuids) => trading.GetTradesExceptWith(otherPartyUuids);
        public bool TryGetOtherPartyUuid(string tradeId, out string otherPartyUuid) => trading.TryGetOtherPartyUuid(tradeId, out otherPartyUuid);
        public bool TryGetOtherPartyAccepted(string tradeId, out bool otherPartyAccepted) => trading.TryGetOtherPartyAccepted(tradeId, out otherPartyAccepted);
        public bool TryGetPartyAccepted(string tradeId, string partyUuid, out bool accepted) => trading.TryGetPartyAccepted(tradeId, partyUuid, out accepted);
        public bool TryGetItemsOnOffer(string tradeId, string uuid, out IEnumerable<Trading.ProtoThing> items) => trading.TryGetItemsOnOffer(tradeId, uuid, out items);
        public void UpdateTradeItems(string tradeId, IEnumerable<ProtoThing> items, string token = "") => trading.UpdateItems(tradeId, items, token);
        public void UpdateTradeStatus(string tradeId, bool? accepted = null, bool? cancelled = null) => trading.UpdateStatus(tradeId, accepted, cancelled);
        public LookTargets DropPods(IEnumerable<Thing> verseThings) => dropPods(verseThings);
        public event EventHandler<UICreateTradeEventArgs> OnTradeCreationSuccess;
        public event EventHandler<UICreateTradeEventArgs> OnTradeCreationFailure;
        public event EventHandler<UICompleteTradeEventArgs> OnTradeCompleted;
        public event EventHandler<UICompleteTradeEventArgs> OnTradeCancelled;
        public event EventHandler<UITradeUpdateEventArgs> OnTradeUpdateSuccess;
        public event EventHandler<UITradeUpdateEventArgs> OnTradeUpdateFailure;
        public event EventHandler<UITradesSyncedEventArgs> OnTradesSynced;
        #endregion

        public event EventHandler<BlockedUsersChangedEventArgs> OnBlockedUsersChanged;

        #region Setting Handles
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

        private SettingHandle<bool> showNameFormatting;
        public bool ShowNameFormatting
        {
            get => showNameFormatting.Value;
            set
            {
                showNameFormatting.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> showChatFormatting;
        public bool ShowChatFormatting
        {
            get => showChatFormatting.Value;
            set
            {
                showChatFormatting.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> playNoiseOnMessageReceived;
        public bool PlayNoiseOnMessageReceived
        {
            get => playNoiseOnMessageReceived.Value;
            set
            {
                playNoiseOnMessageReceived.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> showUnreadMessageCount;
        public bool ShowUnreadMessageCount
        {
            get => showUnreadMessageCount.Value;
            set
            {
                showUnreadMessageCount.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> showBlockedUnreadMessageCount;
        public bool ShowBlockedUnreadMessageCount
        {
            get => showBlockedUnreadMessageCount.Value;
            set
            {
                showBlockedUnreadMessageCount.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<int> chatMessageLimit;
        public int ChatMessageLimit
        {
            get => chatMessageLimit.Value;
            set
            {
                chatMessageLimit.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> allItemsTradable;
        public bool AllItemsTradable
        {
            get => allItemsTradable.Value;
            set
            {
                allItemsTradable.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<bool> showBlockedTrades;
        public bool ShowBlockedTrades
        {
            get => showBlockedTrades.Value;
            set
            {
                showBlockedTrades.Value = value;
                HugsLibController.SettingsManager.SaveChanges();
            }
        }

        private SettingHandle<ListSetting<string>> blockedUsers;
        public List<string> BlockedUsers => blockedUsers.Value.List;
        #endregion

        /// <summary>
        /// Queue of sounds to play on the next frame.
        /// Necessary because sounds are only played on the main Unity thread.
        /// </summary>
        private List<SoundDef> soundQueue = new List<SoundDef>();
        /// <summary>
        /// Lock object to prevent race conditions when accessing soundQueue.
        /// </summary>
        private object soundQueueLock = new object();

        /// <summary>
        /// Items on offer that have been reserved for one or more trades.
        /// </summary>
        public TradeReservedItems ReservedThings
        {
            get
            {
                TradeReservedItems reservedItems = Current.Game.GetComponent<TradeReservedItems>();
                if (reservedItems != null)
                {
                    return reservedItems;
                }
                else
                {
                    // Create a new reserved items instance and add it to the game
                    reservedItems = new TradeReservedItems();
                    Current.Game.components.Add(reservedItems);
                    return reservedItems;
                }
            }
        }

        /// <summary>
        /// Things that have been sent to the server to be put on offer. Organised by concatenated trade ID and update
        /// token.
        /// </summary>
        public readonly Dictionary<string, List<Thing>> PendingTradeThings = new Dictionary<string, List<Thing>>();

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
            #region Settings
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
                title: "Phinix_hugslibsettings_acceptingTradesTitle".Translate(),
                description: null,
                defaultValue: true
            );
            showNameFormatting = Settings.GetHandle(
                settingName: "showNameFormatting",
                title: "Phinix_hugslibsettings_showNameFormatting".Translate(),
                description: null,
                defaultValue: true
            );
            showChatFormatting = Settings.GetHandle(
                settingName: "showChatFormatting",
                title: "Phinix_hugslibsettings_showChatFormatting".Translate(),
                description: null,
                defaultValue: true
            );
            playNoiseOnMessageReceived = Settings.GetHandle(
                settingName: "playNoiseOnMessageReceived",
                title: "Phinix_hugslibsettings_playNoiseOnMessageReceived".Translate(),
                description: null,
                defaultValue: true
            );
            showUnreadMessageCount = Settings.GetHandle(
                settingName: "showUnreadMessageCount",
                title: "Phinix_hugslibsettings_showUnreadMessageCount".Translate(),
                description: null,
                defaultValue: true
            );
            showBlockedUnreadMessageCount = Settings.GetHandle(
                settingName: "showUnreadMessageCount",
                title: "Phinix_hugslibsettings_showBlockedUnreadMessageCount".Translate(),
                description: "Phinix_hugslibsettings_showBlockedUnreadMessageCount_description".Translate(),
                defaultValue: true
            );
            chatMessageLimit = Settings.GetHandle(
                settingName: "chatMessageLimit",
                title: "Phinix_hugslibsettings_chatMessageLimit".Translate(),
                description: null,
                defaultValue: 40
            );
            allItemsTradable = Settings.GetHandle(
                settingName: "allItemsTradable",
                title: "Phinix_hugslibsettings_allItemsTradable".Translate(),
                description: null,
                defaultValue: false
            );
            showBlockedTrades = Settings.GetHandle(
                settingName: "showBlockedTrades",
                title: "Phinix_hugslibsettings_showBlockedTrades".Translate(),
                description: null,
                defaultValue: false
            );
            blockedUsers = Settings.GetHandle<ListSetting<string>>(
                settingName: "blockedUsers",
                title: "Phinix_hugslibsettings_blockedUsers".Translate(),
                description: null
            );
            blockedUsers.NeverVisible = true;
            // Always initialise a new value otherwise it will use the reference of the default value, resulting in the
            // default list being updated and the save mechanism never being able to differentiate any changes.
            if (blockedUsers.Value == null) blockedUsers.Value = new ListSetting<string>();
            #endregion

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

            #region Module Event Handlers
            // Subscribe to net client events
            netClient.OnDisconnect += (sender, args) =>
            {
                // Clear trade update cache
                PendingTradeThings.Clear();
            };

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
            userManager.OnUserDisplayNameChanged += (sender, args) =>
            {
                Logger.Trace(string.Format("User with UUID {0} changed their display name from \"{1}\" to \"{2}\"", args.Uuid, args.OldDisplayName, args.NewDisplayName));
            };
            userManager.OnUserLoggedIn += (sender, args) =>
            {
                Logger.Trace(string.Format("User {0} logged in", args.Uuid));
            };
            userManager.OnUserLoggedOut += (sender, args) =>
            {
                Logger.Trace(string.Format("User {0} logged out", args.Uuid));
            };
            userManager.OnUserCreated += (sender, args) =>
            {
                Logger.Trace(string.Format("New user created: {0} ({1}) - {2}ogged in", args.DisplayName, args.Uuid, args.LoggedIn ? "L" : "Not l"));
            };

            // Subscribe to chat events
            chat.OnChatMessageReceived += (sender, args) =>
            {
                Logger.Trace("Received chat message from UUID " + args.Message.SenderUuid);

                // Check if the message wasn't ours, chat noises are enabled, and if we are in-game before playing a sound
                if (args.Message.SenderUuid != Uuid && PlayNoiseOnMessageReceived && Current.Game != null && !BlockedUsers.Contains(args.Message.SenderUuid))
                {
                    lock (soundQueueLock)
                    {
                        // Add a little tick noise to the sound queue
                        // (queue is necessary because sounds only play on the main Unity thread)
                        soundQueue.Add(SoundDefOf.Tick_Tiny);
                    }
                }
            };

            // Subscribe to trading events
            trading.OnTradeCreationSuccess += (sender, args) =>
            {
                Logger.Trace(string.Format("Created trade {0} with {1}", args.TradeId, args.OtherPartyUuid));

                // Don't display anything if the other party is blocked and we want to hide their trades
                if (!ShowBlockedTrades && Instance.BlockedUsers.Contains(args.OtherPartyUuid)) return;

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
                // Remove any entries for this trade from the trade update cache
                PendingTradeThings.RemoveAll(pair => pair.Key.StartsWith(args.TradeId));

                // Return any reserved items
                if (ReservedThings.ContainsKey(args.TradeId))
                {
                    foreach (Thing thing in ReservedThings[args.TradeId])
                    {
                        // TODO: Check if this will delete existing items in that spot? Need to make it not do that
                        IntVec3 spawnPos;
                        if (thing.Position.IsValid)
                        {
                            spawnPos = thing.Position;
                        }
                        else
                        {
                            spawnPos = DropCellFinder.TradeDropSpot(Find.CurrentMap);
                            Log(new LogEventArgs($"Position for {thing.LabelCap} was invalid ({thing.Position}), using {spawnPos} instead.", LogLevel.DEBUG));
                        }

                        GenSpawn.Spawn(thing, spawnPos, Find.CurrentMap, WipeMode.VanishOrMoveAside);
                    }
                    ReservedThings.Remove(args.TradeId);
                }

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
                LookTargets dropSpotLookTarget = dropPods(verseItems);

                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeAccepted");
                Find.LetterStack.ReceiveLetter("Phinix_trade_tradeCompletedLetter_label".Translate(), "Phinix_trade_tradeCompletedLetter_description".Translate(displayName), letterDef, dropSpotLookTarget);

                Logger.Trace(string.Format("Trade with {0} completed successfully", args.OtherPartyUuid));
            };
            trading.OnTradeCancelled += (sender, args) =>
            {
                // Remove any entries for this trade from the trade update cache
                PendingTradeThings.RemoveAll(pair => pair.Key.StartsWith(args.TradeId));

                // Return any reserved items
                if (ReservedThings.ContainsKey(args.TradeId))
                {
                    foreach (Thing thing in ReservedThings[args.TradeId])
                    {
                        // TODO: Check if this will delete existing items in that spot? Need to make it not do that
                        GenSpawn.Spawn(thing, thing.Position, Find.CurrentMap, WipeMode.VanishOrMoveAside);
                    }
                    ReservedThings.Remove(args.TradeId);
                }

                // Don't display anything if the other party is blocked and we want to hide their trades
                if (!ShowBlockedTrades && Instance.BlockedUsers.Contains(args.OtherPartyUuid)) return;

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

                // Convert all the received items into their Verse counterparts and strip out any unknown ones
                //// While it would be less computationally-expensive to strip out unknown items beforehand, we would
                //// have no idea whether we could actually make the item without another check, so we just piggy-back
                //// off of the converter's checks and strip them out afterward.
                Verse.Thing[] verseItems = args.Items
                                                .Select(TradingThingConverter.ConvertThingFromProtoOrUnknown)
                                                .Where(thing => thing.def.defName != "UnknownItem")
                                                .ToArray();

                // Launch drop pods to a trade spot on a home tile
                LookTargets dropSpotLookTarget = dropPods(verseItems);

                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeCancelled");
                Find.LetterStack.ReceiveLetter("Phinix_trade_tradeCancelled_label".Translate(), "Phinix_trade_tradeCancelled_description".Translate(displayName), letterDef, dropSpotLookTarget);

                Logger.Trace(string.Format("Trade with {0} cancelled", args.OtherPartyUuid));
            };
            trading.OnTradeUpdateSuccess += (sender, args) =>
            {
                // Ignore trades without cached items
                if (!PendingTradeThings.ContainsKey(args.TradeId + args.Token)) return;

                // Despawn the cached items and stash them in the reserved items list
                foreach (Thing thing in PendingTradeThings[args.TradeId + args.Token])
                {
                    if (thing.Spawned) thing.DeSpawn();
                    ReservedThings.Add(args.TradeId, thing);
                }

                PendingTradeThings.Remove(args.TradeId + args.Token);
            };
            trading.OnTradeUpdateFailure += (sender, args) =>
            {
                // Try get the other party's display name
                if (trading.TryGetOtherPartyUuid(args.TradeId, out string otherPartyUuid) &&
                    userManager.TryGetDisplayName(otherPartyUuid, out string displayName))
                {
                    // Strip formatting
                    displayName = TextHelper.StripRichText(displayName);
                }
                else
                {
                    // Unknown display name, default to ???
                    displayName = "???";
                }

                Find.WindowStack.Add(new Dialog_Message("Phinix_error_tradeUpdateFailedTitle".Translate(), "Phinix_error_tradeUpdateFailedMessage".Translate(displayName, args.FailureMessage, args.FailureReason.ToString())));
            };
            trading.OnTradesSynced += (sender, args) =>
            {
                Logger.Trace(string.Format("Synced {0} trade{1} from server", args.TradeIds.Length, args.TradeIds.Length != 1 ? "s" : ""));
            };
            #endregion

            // Subscribe to setting handle value change events
            acceptingTradesHandle.ValueChanged += (handle) => { userManager.UpdateSelf(acceptingTrades: acceptingTradesHandle.Value); };

            // Forward events so the UI can handle them
            netClient.OnConnecting += (sender, e) => { OnConnecting?.Invoke(sender, e); };
            netClient.OnDisconnect += (sender, e) => { OnDisconnect?.Invoke(sender, e); };
            authenticator.OnAuthenticationSuccess += (sender, e) => { OnAuthenticationSuccess?.Invoke(sender, e); };
            authenticator.OnAuthenticationFailure += (sender, e) => { OnAuthenticationFailure?.Invoke(sender, e); };
            userManager.OnLoginSuccess += (sender, e) => { OnLoginSuccess?.Invoke(sender, e); };
            userManager.OnLoginFailure += (sender, e) => { OnLoginFailure?.Invoke(sender, e); };
            userManager.OnUserDisplayNameChanged += (sender, e) => { OnUserDisplayNameChanged?.Invoke(sender, e); };
            userManager.OnUserLoggedIn += (sender, e) => { OnUserLoggedIn?.Invoke(sender, e); };
            userManager.OnUserLoggedOut += (sender, e) => { OnUserLoggedOut?.Invoke(sender, e); };
            userManager.OnUserCreated += (sender, e) => { OnUserCreated?.Invoke(sender, e); };
            userManager.OnUserSync += (sender, e) => { OnUserSync?.Invoke(sender, e); };
            chat.OnChatMessageReceived += (sender, e) => { OnChatMessageReceived?.Invoke(sender, new UIChatMessageEventArgs(new UIChatMessage(userManager, e.Message))); };
            chat.OnChatSync += (sender, e) => { OnChatSync?.Invoke(sender, e); };
            trading.OnTradeCreationSuccess += (sender, e) => { OnTradeCreationSuccess?.Invoke(sender, UICreateTradeEventArgs.FromCreateTradeEventArgs(e, userManager)); };
            trading.OnTradeCreationFailure += (sender, e) => { OnTradeCreationFailure?.Invoke(sender, UICreateTradeEventArgs.FromCreateTradeEventArgs(e, userManager)); };
            trading.OnTradeCompleted += (sender, e) => { OnTradeCompleted?.Invoke(sender, UICompleteTradeEventArgs.FromCompleteTradeEventArgs(e, userManager)); };
            trading.OnTradeCancelled += (sender, e) => { OnTradeCancelled?.Invoke(sender, UICompleteTradeEventArgs.FromCompleteTradeEventArgs(e, userManager)); };
            trading.OnTradeUpdateSuccess += (sender, e) => { OnTradeUpdateSuccess?.Invoke(sender, UITradeUpdateEventArgs.FromTradeUpdateEventArgs(e, trading)); };
            trading.OnTradeUpdateFailure += (sender, e) => { OnTradeUpdateFailure?.Invoke(sender, UITradeUpdateEventArgs.FromTradeUpdateEventArgs(e, trading)); };
            trading.OnTradesSynced += (sender, e) => { OnTradesSynced?.Invoke(sender, UITradesSyncedEventArgs.FromTradesSyncedEventArgs(e, trading, userManager)); };

            // Connect to the server set in the config
            Connect(ServerAddress, ServerPort);
        }

        /// <summary>
        /// Adds a user's UUID to the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of user to block</param>
        public void BlockUser(string senderUuid)
        {
            BlockedUsers.AddDistinct(senderUuid);
            blockedUsers.HasUnsavedChanges = true;
            HugsLibController.SettingsManager.SaveChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, true));
        }

        /// <summary>
        /// Removes a user's UUID from the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of the user to unblock</param>
        public void UnBlockUser(string senderUuid)
        {
            BlockedUsers.Remove(senderUuid);
            blockedUsers.HasUnsavedChanges = true;
            HugsLibController.SettingsManager.SaveChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, false));
        }

        /// <inheritdoc />
        public override void Update()
        {
            lock (soundQueueLock)
            {
                // Check if we have sounds to play
                while (soundQueue.Any())
                {
                    // Dequeue and play a sound
                    SoundDef sound = soundQueue.Pop();
                    sound.PlayOneShotOnCamera();
                }
            }
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
        /// Gets the current chat message buffer, optionally marking them as read.
        /// </summary>
        /// <param name="markAsRead">Whether to mark the messages as read</param>
        /// <param name="unreadOnly">Whether to only get unread messages</param>
        /// <returns>List of chat messages</returns>
        public UIChatMessage[] GetChatMessages(bool markAsRead = true, bool unreadOnly = false)
        {
            ClientChatMessage[] chatMessages = unreadOnly ? chat.GetUnreadMessages(markAsRead) : chat.GetMessages(markAsRead);

            return chatMessages.Select(m => new UIChatMessage(userManager, m)).ToArray();
        }

        /// <summary>
        /// Tries to get the chat message with the given ID.
        /// </summary>
        /// <param name="messageId">ID of the chat message to retrieve</param>
        /// <param name="message">Chat message output</param>
        /// <returns>Whether the chat message was retrieved successfully</returns>
        public bool TryGetMessage(string messageId, out UIChatMessage message)
        {
            message = null;

            // Try pull out the message
            if (!chat.TryGetMessage(messageId, out ClientChatMessage clientChatMessage)) return false;

            // Wrap it with the sender's user details
            message = new UIChatMessage(userManager, clientChatMessage);

            return true;
        }

        /// <summary>
        /// Adds the given items to <see cref="PendingTradeThings"/> and sends them to the server.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="items">Items to append to offer</param>
        /// <param name="token">Unique request token</param>
        public void AddTradeItems(string tradeId, IEnumerable<Thing> items, string token)
        {
            // Iterate over the items and both cache them for later, and convert them to be sent out
            List<Thing> itemList = new List<Thing>();
            List<ProtoThing> protoList = new List<ProtoThing>();
            foreach (Thing item in items)
            {
                itemList.Add(item);
                protoList.Add(item.ConvertToProto());
            }

            // Cache the items against the trade ID and token
            PendingTradeThings[tradeId + token] = itemList;

            // Send them out
            trading.AddItems(tradeId, protoList, token);
        }

        /// <summary>
        /// Clears the items on offer for the given trade and returns any that were despawned as part of an earlier
        /// update.
        /// </summary>
        /// <param name="tradeId">ID of the trade to reset</param>
        public void ResetTradeItems(string tradeId)
        {
            // Respawn everything pending and clear the list
            foreach (Thing thing in PendingTradeThings.Where(pair => pair.Key.StartsWith(tradeId)).SelectMany(p => p.Value))
            {
                // TODO: Figure out which map everything came from
                GenSpawn.Spawn(thing, thing.Position, thing.Map);
            }
            PendingTradeThings.RemoveAll(pair => pair.Key.StartsWith(tradeId));

            // Fetch our items on offer according to the server
            if (!TryGetItemsOnOffer(tradeId, Uuid, out IEnumerable<ProtoThing> thingsOnOffer))
            {
                Instance.Log(new LogEventArgs("Failed to get our offer when resetting trade! Cannot spawn back items!", LogLevel.ERROR));
            }

            // Respawn everything from the local item cache
            if (ReservedThings.ContainsKey(tradeId))
            {
                List<Thing> thingsToDrop = new List<Thing>();
                foreach (Thing serverThing in thingsOnOffer.ConvertToVerseOrUnknown().Where(thing => thing.def.defName != "UnknownItem"))
                {
                    // Match everything in the local item cache
                    IEnumerable<Thing> matchingReservedThings = ReservedThings[tradeId].Where(t => TradingThingConverter.CompareThings(t, serverThing));
                    foreach (Thing thing in matchingReservedThings)
                    {
                        // Subtract the stack size of the match from serverThing
                        serverThing.stackCount = Math.Max(0, serverThing.stackCount - thing.stackCount);
                    }

                    // Queue the remainder of serverThing into drop pods if the stack has anything left
                    if (serverThing.stackCount > 0) thingsToDrop.Add(serverThing);
                }

                // Respawn the contents of the cache
                foreach (Thing thing in ReservedThings[tradeId])
                {
                    // TODO: Figure out which map everything came from
                    GenSpawn.Spawn(thing, thing.Position, Find.CurrentMap, thing.Rotation);
                }

                // Drop pods
                if (thingsToDrop.Any()) Instance.DropPods(thingsToDrop);
            }
            else
            {
                // Convert and drop our items in pods
                DropPods(thingsOnOffer.ConvertToVerse());
            }

            // Clear the items from the server
            UpdateTradeItems(tradeId, Array.Empty<ProtoThing>());
        }

        /// <summary>
        /// Handler for <see cref="ILoggable"/> <c>OnLogEvent</c> events.
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
        /// Handles credential requests from the <see cref="ClientAuthenticator"/> module.
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

        /// <summary>
        /// Launches the given <see cref="Thing"/>s in drop pods to a trade spot at the home colony.
        /// </summary>
        /// <param name="things">Collection of <see cref="Thing"/>s to drop</param>
        /// <returns>LookTarget for the drop location</returns>
        private LookTargets dropPods(IEnumerable<Thing> things)
        {
            // Launch drop pods to a trade spot on a home tile
            Map map = Find.AnyPlayerHomeMap;
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, things, canRoofPunch: false);

            return new LookTargets(dropSpot, map);
        }
    }
}
