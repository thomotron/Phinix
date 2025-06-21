using Authentication;
using Chat;
using Connections;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Trading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;
using Verse.Sound;
using Thing = Verse.Thing;

namespace PhinixClient
{
    public class Client : Mod
    {
        public static Client Instance;
        public static readonly Version Version = typeof(Client).Assembly.GetName().Version;
        public const string PackageId = "Thomotron.Phinix";

        public void Log(LogEventArgs e) => ILoggableHandler(null, e);

        public override string SettingsCategory() => "Phinix";

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
        public int UnreadMessagesExcludingBlocked => chat.GetUnreadMessagesExcluding(Settings.BlockedUsers);
        public event EventHandler<UIChatMessageEventArgs> OnChatMessageReceived;
        public event EventHandler OnChatSync;

        private ClientTrading trading;
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
        public event EventHandler OnChatMessageLimitChanged;

        public Settings Settings { get; }

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
        /// Collection of UUIDs that we have created trades with and are waiting for a confirmation from the server for.
        /// Used to display the trade immediately once it's confirmed.
        /// </summary>
        private HashSet<string> waitingForTradeCreationWith = new HashSet<string>();
        /// <summary>
        /// Lock object protecting <see cref="waitingForTradeCreationWith"/>
        /// </summary>
        private object waitingForTradeCreationWithLock = new object();

        /// <summary>
        /// Collection of trades queued to be opened on the next frame.
        /// Necessary because textures and other assets can only be gotten on the main Unity thread.
        /// </summary>
        private List<ImmutableTrade> tradeWindowQueue = new List<ImmutableTrade>();
        /// <summary>
        /// Lock object protecting <see cref="tradeWindowQueue"/>.
        /// </summary>
        private object tradeWindowQueueLock = new object();

        public Client(ModContentPack content) : base(content)
        {
            Instance = this;

            // Apply Harmony patches
            new HarmonyLib.Harmony(PackageId).PatchAll();

            // Load in Settings
            Settings = GetSettings<Settings>();
            if (!Settings.Migrated) Settings.MigrateFromHugsLib();

            // Set up our module instances
            netClient = new NetClient();
            authenticator = new ClientAuthenticator(netClient, getCredentials);
            userManager = new ClientUserManager(netClient, authenticator);
            chat = new ClientChat(netClient, authenticator, userManager);
            trading = new ClientTrading(netClient, authenticator, userManager);

            // Subscribe to log events
            authenticator.OnLogEntry += ILoggableHandler;
            userManager.OnLogEntry += ILoggableHandler;
            chat.OnLogEntry += ILoggableHandler;
            trading.OnLogEntry += ILoggableHandler;

            #region Module Event Handlers
            // Subscribe to connection events
            netClient.OnDisconnect += (sender, args) =>
            {
                // Clear the waiting list for opening trades
                lock (waitingForTradeCreationWithLock) waitingForTradeCreationWith.Clear();
            };

            // Subscribe to authentication events
            authenticator.OnAuthenticationSuccess += (sender, args) =>
            {
                Verse.Log.Message("Successfully authenticated with server.");
                userManager.SendLogin(
                    displayName: Settings.DisplayName,
                    acceptingTrades: Settings.AcceptingTrades
                );
            };
            authenticator.OnAuthenticationFailure += (sender, args) =>
            {
                Verse.Log.Message(string.Format("Failed to authenticate with server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString()));

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_authFailedTitle".Translate(), text: "Phinix_error_authFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));

                Disconnect();
            };

            // Subscribe to user management events
            userManager.OnLoginSuccess += (sender, args) =>
            {
                Verse.Log.Message(string.Format("Successfully logged in with UUID {0}", userManager.Uuid));
            };
            userManager.OnLoginFailure += (sender, args) =>
            {
                Verse.Log.Message(string.Format("Failed to log in to server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString()));

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_loginFailedTitle".Translate(), text: "Phinix_error_loginFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));

                Disconnect();
            };
            userManager.OnUserDisplayNameChanged += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User with UUID {0} changed their display name from \"{1}\" to \"{2}\"", args.Uuid, args.OldDisplayName, args.NewDisplayName));
            };
            userManager.OnUserLoggedIn += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User {0} logged in", args.Uuid));
            };
            userManager.OnUserLoggedOut += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User {0} logged out", args.Uuid));
            };
            userManager.OnUserCreated += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("New user created: {0} ({1}) - {2}ogged in", args.DisplayName, args.Uuid, args.LoggedIn ? "L" : "Not l"));
            };

            // Subscribe to chat events
            chat.OnChatMessageReceived += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message("Received chat message from UUID " + args.Message.SenderUuid);

                // Check if the message wasn't ours, chat noises are enabled, and if we are in-game before playing a sound
                if (args.Message.SenderUuid != Uuid && Settings.PlayNoiseOnMessageReceived && Current.Game != null && !Settings.BlockedUsers.Contains(args.Message.SenderUuid))
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
                if (Prefs.DevMode) Verse.Log.Message(string.Format("Created trade {0} with {1}", args.TradeId, args.OtherPartyUuid));

                // Don't display anything if the other party is blocked and we want to hide their trades
                if (!Settings.ShowBlockedTrades && Instance.Settings.BlockedUsers.Contains(args.OtherPartyUuid)) return;

                // Check if we are waiting for this trade to be created. If so, show the trade window immediately.
                lock (waitingForTradeCreationWithLock)
                {
                    // Check for and remove the other party's UUID in one go
                    if (waitingForTradeCreationWith.Remove(args.OtherPartyUuid))
                    {
                        if (trading.TryGetTrade(args.TradeId, out ImmutableTrade trade))
                        {
                            // Show the trade window and skip any further processing. No need to generate a letter if
                            // we already have the window up.
                            lock (tradeWindowQueueLock) tradeWindowQueue.Add(trade);
                            return;
                        }
                        else
                        {
                            // Log the failure and revert to the letter instead
                            Verse.Log.Warning(string.Format("Failed to get newly created trade {0} when attempting to open immediately", args.TradeId));
                        }
                    }
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
                if (Prefs.DevMode) Verse.Log.Message(string.Format("Failed to create trade with {0}: {1} ({2})", args.OtherPartyUuid, args.FailureMessage, args.FailureReason.ToString()));

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_tradeCreationFailedTitle".Translate(), text: "Phinix_error_tradeCreationFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString())));

                // Remove the other party from the waiting list
                lock (waitingForTradeCreationWithLock) waitingForTradeCreationWith.Remove(args.OtherPartyUuid);
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
                LookTargets dropSpotLookTarget = dropPods(verseItems);

                // Generate a letter
                LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeAccepted");
                Find.LetterStack.ReceiveLetter("Phinix_trade_tradeCompletedLetter_label".Translate(), "Phinix_trade_tradeCompletedLetter_description".Translate(displayName), letterDef, dropSpotLookTarget);

                if (Prefs.DevMode) Verse.Log.Message(string.Format("Trade with {0} completed successfully", args.OtherPartyUuid));
            };
            trading.OnTradeCancelled += (sender, args) =>
            {
                // Don't display anything if the other party is blocked and we want to hide their trades
                if (!Settings.ShowBlockedTrades && Instance.Settings.BlockedUsers.Contains(args.OtherPartyUuid)) return;

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

                if (Prefs.DevMode) Verse.Log.Message(string.Format("Trade with {0} cancelled", args.OtherPartyUuid));
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

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_tradeUpdateFailedTitle".Translate(), text: "Phinix_error_tradeUpdateFailedMessage".Translate(displayName, args.FailureMessage, args.FailureReason.ToString())));
            };
            trading.OnTradesSynced += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("Synced {0} trade{1} from server", args.TradeIds.Length, args.TradeIds.Length != 1 ? "s" : ""));
            };
            #endregion

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
            Connect(Settings.ServerAddress, Settings.ServerPort);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Phinix_hugslibsettings_serverAddressTitle".Translate());
            Settings.ServerAddress = listing.TextEntry(Settings.ServerAddress);

            listing.Label("Phinix_hugslibsettings_serverPortTitle".Translate());
            string portStr = Settings.ServerPort.ToString();
            portStr = listing.TextEntry(portStr);
            int.TryParse(portStr, out int serverPort);
            Settings.ServerPort = serverPort;

            listing.Label("Phinix_hugslibsettings_displayNameTitle".Translate());
            Settings.DisplayName = listing.TextEntry(Settings.DisplayName);

            bool acceptingTrades = Settings.AcceptingTrades;
            listing.CheckboxLabeled("Phinix_hugslibsettings_acceptingTradesTitle".Translate(), ref acceptingTrades);
            Settings.AcceptingTrades = acceptingTrades;

            bool showNameFormatting = Settings.ShowNameFormatting;
            listing.CheckboxLabeled("Phinix_hugslibsettings_showNameFormatting".Translate(), ref showNameFormatting);
            Settings.ShowNameFormatting = showNameFormatting;

            bool showChatFormatting = Settings.ShowChatFormatting;
            listing.CheckboxLabeled("Phinix_hugslibsettings_showChatFormatting".Translate(), ref showChatFormatting);
            Settings.ShowChatFormatting = showChatFormatting;

            bool playNoiseOnMessageReceived = Settings.PlayNoiseOnMessageReceived;
            listing.CheckboxLabeled("Phinix_hugslibsettings_playNoiseOnMessageReceived".Translate(), ref playNoiseOnMessageReceived);
            Settings.PlayNoiseOnMessageReceived = playNoiseOnMessageReceived;

            bool showUnreadMessageCount = Settings.ShowUnreadMessageCount;
            listing.CheckboxLabeled("Phinix_hugslibsettings_showUnreadMessageCount".Translate(), ref showUnreadMessageCount);
            Settings.ShowUnreadMessageCount = showUnreadMessageCount;

            bool showBlockedUnreadMessageCount = Settings.ShowBlockedUnreadMessageCount;
            listing.CheckboxLabeled("Phinix_hugslibsettings_showBlockedUnreadMessageCount".Translate(), ref showBlockedUnreadMessageCount);
            Settings.ShowBlockedUnreadMessageCount = showBlockedUnreadMessageCount;

            listing.Label("Phinix_hugslibsettings_chatMessageLimit".Translate());
            string limitStr = Settings.ChatMessageLimit.ToString();
            limitStr = listing.TextEntry(limitStr);
            int.TryParse(limitStr, out int chatMessageLimit);
            Settings.ChatMessageLimit = chatMessageLimit;

            bool forceMessageFieldFocus = Settings.ForceMessageFieldFocus;
            listing.CheckboxLabeled("Phinix_hugsLibSettings_forceMessageFieldFocus".Translate(), ref forceMessageFieldFocus);
            Settings.ForceMessageFieldFocus = forceMessageFieldFocus;

            bool allItemsTradable = Settings.AllItemsTradable;
            listing.CheckboxLabeled("Phinix_hugslibsettings_allItemsTradable".Translate(), ref allItemsTradable);
            Settings.AllItemsTradable = allItemsTradable;

            bool showBlockedTrades = Settings.ShowBlockedTrades;
            listing.CheckboxLabeled("Phinix_hugslibsettings_showBlockedTrades".Translate(), ref showBlockedTrades);
            Settings.ShowBlockedTrades = showBlockedTrades;

            listing.End();
        }

        public override void WriteSettings()
        {
            if (!Settings.IsChanged) return;

            Settings.AcceptChanges();
            userManager.UpdateSelf(Settings.DisplayName, Settings.AcceptingTrades);
        }

        /// <summary>
        /// Adds a user's UUID to the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of user to block</param>
        public void BlockUser(string senderUuid)
        {
            if (!Settings.BlockedUsers.Add(senderUuid)) return;

            Settings.AcceptChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, true));
        }

        /// <summary>
        /// Removes a user's UUID from the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of the user to unblock</param>
        public void UnBlockUser(string senderUuid)
        {
            if (!Settings.BlockedUsers.Remove(senderUuid)) return;

            Settings.AcceptChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, false));
        }

        /// <summary>
        /// A hook into the main update loop. Periodically updates state.
        /// </summary>
        /// <seealso cref="Patches.RootPatch.Update"/>
        public void Update()
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

            lock (tradeWindowQueueLock)
            {
                // Check if we have any trade windows to open
                while (tradeWindowQueue.Any())
                {
                    // Dequeue and open the window
                    Find.WindowStack.Add(new TradeWindow(tradeWindowQueue.Pop()));
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
                Verse.Log.Message(string.Format("Could not connect to {0}:{1}", Settings.ServerAddress, Settings.ServerPort));

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_connectionFailedTitle".Translate(), text: "Phinix_error_connectionFailedMessage".Translate(Settings.ServerAddress, Settings.ServerPort)));
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
        /// Creates a trade with the given user.
        /// </summary>
        /// <param name="uuid">Other party's UUID</param>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public void CreateTrade(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                throw new ArgumentException("UUID cannot be null or empty", nameof(uuid));
            }

            // Add the other party to the waiting list so we can open it immediately
            lock (waitingForTradeCreationWithLock)
            {
                waitingForTradeCreationWith.Add(uuid);
            }

            trading.CreateTrade(uuid);
        }

        /// <summary>
        /// Handler for <see cref="ILoggable"/> <c>OnLogEvent</c> events.
        /// Raised by modules as a way to hook into the log.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="args">Event arguments</param>
        private void ILoggableHandler(object sender, LogEventArgs args)
        {
            switch (args.LogLevel)
            {
                case LogLevel.DEBUG:
                    if (Prefs.DevMode) Verse.Log.Message(args.Message);
                    break;
                case LogLevel.WARNING:
                    Verse.Log.Warning(args.Message);
                    break;
                case LogLevel.ERROR:
                case LogLevel.FATAL:
                    Verse.Log.Error(args.Message);
                    break;
                case LogLevel.INFO:
                default:
                    Verse.Log.Message(args.Message);
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
            if (Prefs.DevMode) Verse.Log.Message(string.Format("Authentication needs more credentials for the server \"{0}\" with authentication type \"{1}\"", serverName, authType.ToString()));

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
