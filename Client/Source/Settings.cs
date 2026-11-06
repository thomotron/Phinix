using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Verse;

namespace PhinixClient
{
    public class Settings : ModSettings, IChangeTracking
    {
        #region Properties

        private string originalServerAddress;
        private string serverAddress;
        public string ServerAddress
        {
            get => serverAddress;
            set => serverAddress = value;
        }

        private int originalServerPort;
        private int serverPort;
        public int ServerPort
        {
            get => serverPort;
            set => serverPort = value;
        }

        private string originalCompanionAddress;
        private string companionAddress;
        /// <summary>
        /// Address of a companion server, which provides Phime features the main server doesn't have. Empty for none.
        /// </summary>
        public string CompanionAddress
        {
            get => companionAddress;
            set => companionAddress = value;
        }

        private int originalCompanionPort;
        private int companionPort;
        public int CompanionPort
        {
            get => companionPort;
            set => companionPort = value;
        }

        private string originalDisplayName;
        private string displayName;
        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        private string originalBio;
        private string bio;
        /// <summary>
        /// A short line about the player, shown to others when they hover over their name.
        /// </summary>
        public string Bio
        {
            get => bio;
            set => bio = value;
        }

        private bool originalAcceptingTrades;
        private bool acceptingTrades;
        public bool AcceptingTrades
        {
            get => acceptingTrades;
            set => acceptingTrades = value;
        }

        private bool originalShowNameFormatting;
        private bool showNameFormatting;
        public bool ShowNameFormatting
        {
            get => showNameFormatting;
            set => showNameFormatting = value;
        }

        private bool originalShowChatFormatting;
        private bool showChatFormatting;
        public bool ShowChatFormatting
        {
            get => showChatFormatting;
            set => showChatFormatting = value;
        }

        private bool originalPlayNoiseOnMessageReceived;
        private bool playNoiseOnMessageReceived;
        public bool PlayNoiseOnMessageReceived
        {
            get => playNoiseOnMessageReceived;
            set => playNoiseOnMessageReceived = value;
        }

        private bool originalShowUnreadMessageCount;
        private bool showUnreadMessageCount;
        public bool ShowUnreadMessageCount
        {
            get => showUnreadMessageCount;
            set => showUnreadMessageCount = value;
        }

        private bool originalShowBlockedUnreadMessageCount;
        private bool showBlockedUnreadMessageCount;
        public bool ShowBlockedUnreadMessageCount
        {
            get => showBlockedUnreadMessageCount;
            set => showBlockedUnreadMessageCount = value;
        }

        private int originalChatMessageLimit;
        private int chatMessageLimit;
        public int ChatMessageLimit
        {
            get => chatMessageLimit;
            set => chatMessageLimit = value;
        }

        private bool originalForceMessageFieldFocus;
        private bool forceMessageFieldFocus;
        public bool ForceMessageFieldFocus
        {
            get => forceMessageFieldFocus;
            set => forceMessageFieldFocus = value;
        }

        private bool originalAllItemsTradable;
        private bool allItemsTradable;
        public bool AllItemsTradable
        {
            get => allItemsTradable;
            set => allItemsTradable = value;
        }

        private bool originalShowBlockedTrades;
        private bool showBlockedTrades;
        public bool ShowBlockedTrades
        {
            get => showBlockedTrades;
            set => showBlockedTrades = value;
        }

        private bool originalMigrated;
        private bool migrated;
        public bool Migrated
        {
            get => migrated;
            set => migrated = value;
        }

        private HashSet<string> originalBlockedUsers;
        private HashSet<string> blockedUsers;
        public HashSet<string> BlockedUsers => blockedUsers;

        private Dictionary<string, string> originalFriends;
        private Dictionary<string, string> friends;
        /// <summary>
        /// Friended users' UUIDs, each with the display name they had when they were friended (formatting stripped).
        /// </summary>
        /// <remarks>
        /// Keyed by UUID because that never changes, so a friend who renames is still a friend. Use
        /// <see cref="Client.AddFriend"/>, <see cref="Client.RemoveFriend"/> and <see cref="Client.GetFriends"/>
        /// rather than this directly, since the user list reads it from the network thread.
        /// </remarks>
        public Dictionary<string, string> Friends => friends;

        private bool originalShareColonyStats;
        private bool shareColonyStats;
        /// <summary>
        /// Whether our player card shows the colony we're playing: its name, biome, day, colonists, animals and wealth.
        /// </summary>
        public bool ShareColonyStats
        {
            get => shareColonyStats;
            set => shareColonyStats = value;
        }

        private bool originalDropCurrentMap;
        private bool dropCurrentMap;
        public bool DropCurrentMap
        {
            get => dropCurrentMap;
            set => dropCurrentMap = value;
        }

        private GiftsFromStrangers originalGiftsFromStrangers;
        private GiftsFromStrangers giftsFromStrangers;
        /// <summary>
        /// What to do with gifts from people who aren't friends. Friends' gifts always arrive straight away.
        /// </summary>
        public GiftsFromStrangers GiftsFromStrangers
        {
            get => giftsFromStrangers;
            set => giftsFromStrangers = value;
        }

        private bool originalCollapseBlockedUsers;
        private bool collapseBlockedUsers;
        public bool CollapseBlockedUsers
        {
            get => collapseBlockedUsers;
            set => collapseBlockedUsers = value;
        }

        /// <inheritdoc/>
        public bool IsChanged
        {
            get
            {
                return serverAddress != originalServerAddress ||
                       serverPort != originalServerPort ||
                       companionAddress != originalCompanionAddress ||
                       companionPort != originalCompanionPort ||
                       displayName != originalDisplayName ||
                       bio != originalBio ||
                       acceptingTrades != originalAcceptingTrades ||
                       showNameFormatting != originalShowNameFormatting ||
                       showChatFormatting != originalShowChatFormatting ||
                       playNoiseOnMessageReceived != originalPlayNoiseOnMessageReceived ||
                       showUnreadMessageCount != originalShowUnreadMessageCount ||
                       showBlockedUnreadMessageCount != originalShowBlockedUnreadMessageCount ||
                       chatMessageLimit != originalChatMessageLimit ||
                       forceMessageFieldFocus != originalForceMessageFieldFocus ||
                       allItemsTradable != originalAllItemsTradable ||
                       showBlockedTrades != originalShowBlockedTrades ||
                       !blockedUsers.SequenceEqual(originalBlockedUsers) ||
                       friends.Count != originalFriends.Count ||
                       friends.Any(friend => !originalFriends.TryGetValue(friend.Key, out string name) || name != friend.Value) ||
                       migrated != originalMigrated ||
                       dropCurrentMap != originalDropCurrentMap ||
                       shareColonyStats != originalShareColonyStats ||
                       giftsFromStrangers != originalGiftsFromStrangers ||
                       collapseBlockedUsers != originalCollapseBlockedUsers;
            }
        }

        #endregion

        #region Constructors

        public Settings()
        {
            // Always set defaults
            serverAddress = "phinix.chat";
            serverPort = 16200;
            companionAddress = "";
            companionPort = 16200;
            displayName = SteamUtility.SteamPersonaName;
            bio = "";
            acceptingTrades = true;
            showNameFormatting = true;
            showChatFormatting = true;
            playNoiseOnMessageReceived = true;
            showUnreadMessageCount = true;
            showBlockedUnreadMessageCount = false;
            chatMessageLimit = 40;
            forceMessageFieldFocus = true;
            allItemsTradable = false;
            showBlockedTrades = false;
            migrated = false;
            dropCurrentMap = false;
            shareColonyStats = true;
            giftsFromStrangers = GiftsFromStrangers.AskFirst;
            collapseBlockedUsers = true;

            originalBlockedUsers = new HashSet<string>();
            blockedUsers = new HashSet<string>();
            originalFriends = new Dictionary<string, string>();
            friends = new Dictionary<string, string>();

            SetOriginalValues();
        }

        #endregion

        #region Methods

        public override void ExposeData()
        {
            Scribe_Values.Look(ref serverAddress, "serverAddress", "phinix.chat");
            Scribe_Values.Look(ref serverPort, "serverPort", 16200);
            Scribe_Values.Look(ref companionAddress, "companionAddress", "");
            Scribe_Values.Look(ref companionPort, "companionPort", 16200);
            Scribe_Values.Look(ref displayName, "displayName", SteamUtility.SteamPersonaName);
            Scribe_Values.Look(ref bio, "bio", "");
            if (bio is null) bio = "";
            Scribe_Values.Look(ref acceptingTrades, "acceptingTrades", true);
            Scribe_Values.Look(ref showNameFormatting, "showNameFormatting", true);
            Scribe_Values.Look(ref showChatFormatting, "showChatFormatting", true);
            Scribe_Values.Look(ref playNoiseOnMessageReceived, "playNoiseOnMessageReceived", true);
            Scribe_Values.Look(ref showUnreadMessageCount, "showUnreadMessageCount", true);
            Scribe_Values.Look(ref showBlockedUnreadMessageCount, "showBlockedUnreadMessageCount", false);
            Scribe_Values.Look(ref chatMessageLimit, "chatMessageLimit", 40);
            Scribe_Values.Look(ref forceMessageFieldFocus, "forceMessageFieldFocus", true);
            Scribe_Values.Look(ref allItemsTradable, "allItemsTradable", false);
            Scribe_Values.Look(ref showBlockedTrades, "showBlockedTrades", false);
            Scribe_Values.Look(ref migrated, "migrated", false);
            Scribe_Collections.Look(ref blockedUsers, "blockedUsers", LookMode.Value);
            Scribe_Values.Look(ref dropCurrentMap, "dropCurrentMap", false);
            Scribe_Values.Look(ref shareColonyStats, "shareColonyStats", true);
            Scribe_Values.Look(ref giftsFromStrangers, "giftsFromStrangers", GiftsFromStrangers.AskFirst);
            Scribe_Values.Look(ref collapseBlockedUsers, "collapseBlockedUsers", true);
            Scribe_Collections.Look(ref friends, "friends", LookMode.Value, LookMode.Value);

            // Prevent scribe from interpreting a missing value as null
            if (blockedUsers is null) blockedUsers = new HashSet<string>();
            if (friends is null) friends = new Dictionary<string, string>();
        }

        /// <inheritdoc/>
        public void AcceptChanges()
        {
            Write();
            SetOriginalValues();
        }

        /// <summary>
        /// Attempts to load settings saved in the HugsLib format and applies them to the current instance. Changes are saved immediately.
        /// </summary>
        public void MigrateFromHugsLib()
        {
            LegacySettings legacySettings = LegacySettings.FromHugsLibSettings(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml"));
            if (legacySettings != null)
            {
                ServerAddress = legacySettings.ServerAddress ?? ServerAddress;
                ServerPort = legacySettings.ServerPort ?? ServerPort;
                DisplayName = legacySettings.DisplayName ?? DisplayName;
                AcceptingTrades = legacySettings.AcceptingTrades ?? AcceptingTrades;
                ShowNameFormatting = legacySettings.ShowNameFormatting ?? ShowNameFormatting;
                ShowChatFormatting = legacySettings.ShowChatFormatting ?? ShowChatFormatting;
                PlayNoiseOnMessageReceived = legacySettings.PlayNoiseOnMessageReceived ?? PlayNoiseOnMessageReceived;
                ShowUnreadMessageCount = legacySettings.ShowUnreadMessageCount ?? ShowUnreadMessageCount;
                ShowBlockedUnreadMessageCount = legacySettings.ShowBlockedUnreadMessageCount ?? ShowBlockedUnreadMessageCount;
                ChatMessageLimit = legacySettings.ChatMessageLimit ?? ChatMessageLimit;
                ForceMessageFieldFocus = legacySettings.ForceMessageFieldFocus ?? ForceMessageFieldFocus;
                AllItemsTradable = legacySettings.AllItemsTradable ?? AllItemsTradable;
                ShowBlockedTrades = legacySettings.ShowBlockedTrades ?? ShowBlockedTrades;

                BlockedUsers.Clear();
                BlockedUsers.AddRange(legacySettings.BlockedUsers);
            }

            Migrated = true;

            Write();
            AcceptChanges();

            Log.Message("Migrated settings from HugsLib.");
        }

        /// <summary>
        /// Resets the object's state by copying to the original state variables.
        /// </summary>
        private void SetOriginalValues()
        {
            originalServerAddress = serverAddress;
            originalServerPort = serverPort;
            originalCompanionAddress = companionAddress;
            originalCompanionPort = companionPort;
            originalDisplayName = displayName;
            originalBio = bio;
            originalAcceptingTrades = acceptingTrades;
            originalShowNameFormatting = showNameFormatting;
            originalShowChatFormatting = showChatFormatting;
            originalPlayNoiseOnMessageReceived = playNoiseOnMessageReceived;
            originalShowUnreadMessageCount = showUnreadMessageCount;
            originalShowBlockedUnreadMessageCount = showBlockedUnreadMessageCount;
            originalChatMessageLimit = chatMessageLimit;
            originalForceMessageFieldFocus = forceMessageFieldFocus;
            originalAllItemsTradable = allItemsTradable;
            originalShowBlockedTrades = showBlockedTrades;
            originalMigrated = migrated;
            originalDropCurrentMap = dropCurrentMap;
            originalShareColonyStats = shareColonyStats;
            originalGiftsFromStrangers = giftsFromStrangers;
            originalCollapseBlockedUsers = collapseBlockedUsers;

            originalBlockedUsers.Clear();
            originalBlockedUsers.AddRange(blockedUsers);

            originalFriends = new Dictionary<string, string>(friends);
        }

        #endregion
    }

    /// <summary>
    /// What to do with a gift from someone who isn't a friend.
    /// </summary>
    public enum GiftsFromStrangers
    {
        /// <summary>Drop it in like a friend's gift</summary>
        AlwaysAccept,
        /// <summary>Send a letter showing what's in it, with buttons to accept or send it back. The default</summary>
        AskFirst,
        /// <summary>Send it straight back</summary>
        Never
    }
}
