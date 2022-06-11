using PhinixClient.GUI;
using PhinixClient.GUI.Compound_Widgets;
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

        private static Vector2 activeTradesScroll = new Vector2(0, 0);

        private UserList userList = new UserList();

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
            Rect rightColumnRect = inRect.RightPartPixels(RIGHT_COLUMN_WIDTH);
            Rect chatRect = inRect.LeftPartPixels(inRect.width - (rightColumnRect.width + DEFAULT_SPACING));

            // Chat
            GenerateChat(chatRect);

            // Right column
            GenerateRightColumn(rightColumnRect);
        }

        /// <summary>
        /// Draws a column containing a settings button, user search box, and a user list.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateRightColumn(Rect inRect)
        {
            Rect settingsButtonRect = inRect.TopPartPixels(SETTINGS_BUTTON_HEIGHT);
            Rect userSearchRect = new Rect(inRect.x, settingsButtonRect.yMax + DEFAULT_SPACING, inRect.width, USER_SEARCH_HEIGHT);
            Rect userListRect = inRect.BottomPartPixels(inRect.height - (userSearchRect.yMax + DEFAULT_SPACING));

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
                // TODO: Online panel
                Widgets.DrawMenuSection(chatRect);
                Widgets.NoneLabelCenteredVertically(chatRect, "Online! :)");
            }
            else
            {
                Widgets.DrawMenuSection(chatRect);
                Widgets.NoneLabelCenteredVertically(chatRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }

            // Message entry field
            message = Widgets.TextField(messageBoxRect, message);

            // Send button
            if (Widgets.ButtonText(sendButtonRect, "Phinix_chat_sendButton".Translate()))
            {
                sendChatMessage();
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

                message = "";
            }
        }
    }
}
