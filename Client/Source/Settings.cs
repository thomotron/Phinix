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

        private string originalDisplayName;
        private string displayName;
        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
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

        /// <inheritdoc/>
        public bool IsChanged
        {
            get
            {
                return serverAddress != originalServerAddress ||
                       serverPort != originalServerPort ||
                       displayName != originalDisplayName ||
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
                       migrated != originalMigrated;
            }
        }

        #endregion

        #region Constructors

        public Settings()
        {
            // Always set defaults
            serverAddress = "phinix.chat";
            serverPort = 16200;
            displayName = SteamUtility.SteamPersonaName;
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

            originalBlockedUsers = new HashSet<string>();
            blockedUsers = new HashSet<string>();

            SetOriginalValues();
        }

        #endregion

        #region Methods

        public override void ExposeData()
        {
            Scribe_Values.Look(ref serverAddress, "serverAddress", "phinix.chat");
            Scribe_Values.Look(ref serverPort, "serverPort", 16200);
            Scribe_Values.Look(ref displayName, "displayName", SteamUtility.SteamPersonaName);
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

            // Prevent scribe from interpreting a missing value as null
            if (blockedUsers is null) blockedUsers = new HashSet<string>();
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
            originalDisplayName = displayName;
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

            originalBlockedUsers.Clear();
            originalBlockedUsers.AddRange(blockedUsers);
        }

        #endregion
    }
}
