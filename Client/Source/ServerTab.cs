using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ServerTab : MainTabWindow
    {
        private const float WINDOW_PADDING = 0f;
        private const float DEFAULT_SPACING = 10f;
        
        private const float CHAT_WIDTH = 730f;

        private const float CHAT_MESSAGE_HEIGHT = 30f;

        private const float CHAT_TEXTBOX_HEIGHT = 30f;

        private const float CHAT_SEND_BUTTON_HEIGHT = 30f;
        private const float CHAT_SEND_BUTTON_WIDTH = 80f;

        private const float SETTINGS_BUTTON_HEIGHT = 30f;
        private const float SETTINGS_BUTTON_WIDTH = 210f;

        private const float USER_SEARCH_HEIGHT = 30f;
        private const float USER_SEARCH_WIDTH = 210f;

        private const float USER_LIST_WIDTH = 210f;

        private const float USER_HEIGHT = 30f;
        private const float USER_WIDTH = USER_LIST_WIDTH;

        private const float RIGHT_COLUMN_CONTAINER_WIDTH = 210f;

        public override Vector2 InitialSize => new Vector2(1000f, 650f);

        private static string message = "";
        private static string userSearch = "";

        public override void DoWindowContents(Rect inRect)
        {
            base.DoWindowContents(inRect);

            // Chat container
            Rect chatContainer = new Rect(
                x: WINDOW_PADDING,
                y: WINDOW_PADDING,
                width: inRect.width - (RIGHT_COLUMN_CONTAINER_WIDTH + DEFAULT_SPACING),
                height: inRect.height - WINDOW_PADDING * 2
            );
            DrawChat(chatContainer);

            // Right column (settings and user list) container
            Rect rightColumnContainer = new Rect(
                x: inRect.xMax - (RIGHT_COLUMN_CONTAINER_WIDTH + WINDOW_PADDING),
                y: inRect.yMin + WINDOW_PADDING,
                width: RIGHT_COLUMN_CONTAINER_WIDTH,
                height: inRect.height - WINDOW_PADDING * 2
            );
            DrawRightColumn(rightColumnContainer);
        }

        /// <summary>
        /// Draws the right column - consisting of a settings button, user search box, and a user list - within the given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawRightColumn(Rect container)
        {
            // Settings button
            Rect settingsButtonRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: SETTINGS_BUTTON_WIDTH,
                height: SETTINGS_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(settingsButtonRect, "Phinix_chat_settingsButton".Translate()))
            {
                // TODO: Build settings dialog
                Log.Message("Settings button was clicked!");
            }

            // User search box
            Rect searchBoxRect = new Rect(
                x: container.xMin,
                y: container.yMin + SETTINGS_BUTTON_HEIGHT + DEFAULT_SPACING,
                width: USER_SEARCH_WIDTH,
                height: USER_SEARCH_HEIGHT
            );
            userSearch = Widgets.TextField(searchBoxRect, userSearch);

            // User list
            Rect userListRect = new Rect(
                x: container.xMin,
                y: container.yMin + SETTINGS_BUTTON_HEIGHT + USER_SEARCH_HEIGHT + DEFAULT_SPACING * 2,
                width: USER_LIST_WIDTH,
                height: container.height - (SETTINGS_BUTTON_HEIGHT + USER_SEARCH_HEIGHT + DEFAULT_SPACING * 2)
            );
            Widgets.DrawMenuSection(userListRect);
        }

        /// <summary>
        /// Draws the chat window - consisting of a message list, message entry box, and a send button - within the given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawChat(Rect container)
        {
            // Chat message area
            // TODO: Replace chat area with actual messages (i.e. DrawMessages() or something)
            Rect chatAreaRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: CHAT_WIDTH,
                height: container.height - (CHAT_TEXTBOX_HEIGHT + DEFAULT_SPACING)
            );
            Widgets.DrawMenuSection(chatAreaRect);

            // Message entry box
            Rect messageEntryRect = new Rect(
                x: container.xMin,
                y: container.yMax - (CHAT_TEXTBOX_HEIGHT + DEFAULT_SPACING),
                width: container.width - (CHAT_SEND_BUTTON_WIDTH + DEFAULT_SPACING),
                height: CHAT_TEXTBOX_HEIGHT
            );
            message = Widgets.TextField(messageEntryRect, message);

            // Send button
            Rect sendButtonRect = new Rect(
                x: container.xMax - CHAT_SEND_BUTTON_WIDTH,
                y: container.yMax - CHAT_SEND_BUTTON_HEIGHT,
                width: CHAT_SEND_BUTTON_WIDTH,
                height: CHAT_SEND_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(sendButtonRect, "Phinix_chat_sendButton".Translate())) // This is a bit weird, but since this will be called every frame it 'just works'
            {
                Log.Message("Send button was clicked!\n" +
                            $"The text field contained \'{message}\'");

                // TODO: Make chat message 'sent' callback to remove message, preventing removal of lengthy messages for nothing and causing frustration
                message = "";
            }
        }
    }
}
