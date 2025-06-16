using System.Collections.Generic;
using PhinixClient.GUI;
using RimWorld;
using UnityEngine;
using Verse;
using static PhinixClient.Client;

namespace PhinixClient
{
    public class ServerTab : MainTabWindow
    {
        private const float DEFAULT_SPACING = 10f;
        private const float COLUMN_SPACING = 20f;
        private const float SCROLLBAR_WIDTH = 16f;

        private const float CHAT_TEXTBOX_HEIGHT = 30f;

        private const float CHAT_SEND_BUTTON_HEIGHT = 30f;
        private const float CHAT_SEND_BUTTON_WIDTH = 80f;

        private const float SETTINGS_BUTTON_HEIGHT = 30f;
        private const float SETTINGS_BUTTON_WIDTH = 210f;

        private const float USER_SEARCH_HEIGHT = 30f;
        private const float USER_SEARCH_WIDTH = 210f;

        private const float RIGHT_COLUMN_WIDTH = 210f;

        // TODO: Add some kind of option to resize chat tab. Maybe a draggable corner?
        public override Vector2 InitialSize => new Vector2(1000f, 680f);

        private static string message = "";
        private static string userSearch = "";

        private readonly List<TabRecord> tabList = new List<TabRecord>();
        private int activeTab = 0;

        private readonly ChatMessageList chatMessageList = new ChatMessageList();
        private readonly UserList userList = new UserList();
        private readonly TradeList tradeList = new TradeList();

        private bool chatMessageFieldFocused = false;

        public ServerTab()
        {
            // Populate the tab list
            tabList.Add(new TabRecord("Phinix_tabs_chat".Translate(), () => activeTab = 0, () => activeTab == 0));
            tabList.Add(new TabRecord("Phinix_tabs_trades".Translate(), () => activeTab = 1, () => activeTab == 1));
        }

        ///<inheritdoc/>
        /// <summary>
        /// Overrides the default accept key behaviour and instead sends a message.
        /// </summary>
        public override void OnAcceptKeyPressed()
        {
            sendChatMessage();
        }

        /// <summary>
        /// Extends the default window drawing behaviour by drawing the Phinix chat interface.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public override void DoWindowContents(Rect inRect)
        {
            Rect usableRect = inRect.BottomPartPixels(inRect.height - TabDrawer.TabHeight);
            Rect rightColumnRect = usableRect.RightPartPixels(RIGHT_COLUMN_WIDTH);
            Rect chatRect = usableRect.LeftPartPixels(usableRect.width - (rightColumnRect.width + DEFAULT_SPACING));

            // Tabs
            TabDrawer.DrawTabs(usableRect, tabList, 1, 200f);

            switch (activeTab)
            {
                case 0: // Chat tab
                    GenerateChat(chatRect); // Chat
                    GenerateRightColumn(rightColumnRect); // Right column
                    break;
                case 1: // Trades tab
                    GenerateTrades(usableRect); // Trades
                    break;
                default:
                    Widgets.DrawMenuSection(usableRect); // Placeholder
                    break;
            }
        }

        /// <summary>
        /// Draws a column containing a settings button, user search box, and a user list.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateRightColumn(Rect inRect)
        {
            Rect settingsButtonRect = inRect.TopPartPixels(SETTINGS_BUTTON_HEIGHT);
            Rect userSearchRect = new Rect(inRect.x, settingsButtonRect.yMax + DEFAULT_SPACING, inRect.width, USER_SEARCH_HEIGHT);
            Rect userListRect = new Rect(inRect.x, userSearchRect.yMax + DEFAULT_SPACING, inRect.width, inRect.yMax - (userSearchRect.yMax + DEFAULT_SPACING));

            // Settings button
            if (Widgets.ButtonText(settingsButtonRect, "Phinix_chat_settingsButton".Translate()))
            {
                Find.WindowStack.Add(new SettingsWindow());
            }

            // User search box
            string userSearchOld = userSearch;
            userSearch = Widgets.TextField(userSearchRect, userSearch);
            if (!userSearch.Equals(userSearchOld))
            {
                userList.Filter(userSearch);
            }

            // User list
            if (Instance.Online)
            {
                userList.Draw(userListRect);
            }
            else
            {
                Widgets.DrawMenuSection(userListRect);
            }
        }

        /// <summary>
        /// Generates the chat window consisting of a message list, message entry box, and a send button.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateChat(Rect inRect)
        {
            Rect sendButtonRect = inRect.BottomPartPixels(CHAT_TEXTBOX_HEIGHT).RightPartPixels(CHAT_SEND_BUTTON_WIDTH);
            Rect messageBoxRect = inRect.BottomPartPixels(CHAT_TEXTBOX_HEIGHT).LeftPartPixels(inRect.width - (CHAT_SEND_BUTTON_WIDTH + DEFAULT_SPACING));
            Rect chatRect = inRect.TopPartPixels(inRect.height - (messageBoxRect.height + DEFAULT_SPACING));

            // Chat message area
            if (Instance.Online)
            {
                chatMessageList.Draw(chatRect);
            }
            else
            {
                Widgets.DrawMenuSection(chatRect);
                Widgets.NoneLabelCenteredVertically(chatRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }

            // Message entry field
            UnityEngine.GUI.SetNextControlName("Phinix_chatMessageField");
            message = Widgets.TextField(messageBoxRect, message);

            if (Client.Instance.Settings.ForceMessageFieldFocus)
            {
                // Aggressively hold the focus on the chat field until clicked out of
                if (Input.GetMouseButtonDown(0))
                {
                    chatMessageFieldFocused = Mouse.IsOver(messageBoxRect);
                }
                if (chatMessageFieldFocused) UnityEngine.GUI.FocusControl("Phinix_chatMessageField");
            }

            // Send button
            if (Widgets.ButtonText(sendButtonRect, "Phinix_chat_sendButton".Translate()))
            {
                sendChatMessage();
            }
        }

        /// <summary>
        /// Generates the active trade list.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateTrades(Rect inRect)
        {
            if (Instance.Online)
            {
                tradeList.Draw(inRect);
            }
            else
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }
        }

        /// <summary>
        /// Sends <see cref="message"/> to the server.
        /// </summary>
        private void sendChatMessage()
        {
            if (!string.IsNullOrEmpty(message) && Instance.Online)
            {
                // TODO: Make chat message 'sent' callback to remove message, preventing removal of lengthy messages for nothing and causing frustration
                Instance.SendMessage(message);
                chatMessageList.ScrollToBottom();

                message = "";
            }
        }
    }
}
