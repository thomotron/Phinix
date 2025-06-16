using PhinixClient.GUI;
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

        private List<string> originalBlockedUsers;
        private List<string> blockedUsers;
        public List<string> BlockedUsers => blockedUsers;

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
                       !blockedUsers.SequenceEqual(originalBlockedUsers);
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

            originalBlockedUsers = new List<string>();
            blockedUsers = new List<string>();

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
            Scribe_Collections.Look(ref blockedUsers, "blockedUsers", LookMode.Value);

            // Prevent scribe from interpreting a missing value as null
            if (blockedUsers is null) blockedUsers = new List<string>();
        }

        /// <inheritdoc/>
        public void AcceptChanges()
        {
            SetOriginalValues();
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

            originalBlockedUsers.Clear();
            originalBlockedUsers.AddRange(blockedUsers);
        }

        #endregion
    }

    public class LegacySettings
    {
        // TODO: Make a replica of the Hugs settings and load them manually from the XML file.
    }
}
