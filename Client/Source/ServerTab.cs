using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Chat;
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

        private const float USER_LIST_WIDTH = 210f;

        private const float USER_HEIGHT = 30f;

        private const float RIGHT_COLUMN_CONTAINER_WIDTH = 210f;

        // TODO: Add some kind of option to resize chat tab. Maybe a draggable corner?
        public override Vector2 InitialSize => new Vector2(1000f, 680f);

        private static Vector2 chatScroll = new Vector2(0, 0);
        private static float oldHeight = 0f;
        private static bool scrollToBottom = false;
        private static bool stickyScroll = true;
        
        private static List<ChatMessage> messages = new List<ChatMessage>();
        private static readonly object messagesLock = new object();
        
        private static Vector2 userListScroll = new Vector2(0, 0);

        private static string message = "";
        private static string userSearch = "";

        public ServerTab()
        {
            // Subscribe to new chat messages
            Instance.OnChatMessageReceived += messageHandler;
        }
        
        ///<inheritdoc/>
        /// <summary>
        /// Overrides the default accept key behaviour and instead sends a message.
        /// </summary>
        public override void OnAcceptKeyPressed()
        {
            // Send the message
            if (!string.IsNullOrEmpty(message))
            {
                Instance.SendMessage(message);

                message = "";
            }
        }

        /// <summary>
        /// Extends the default window drawing behaviour by drawing the Phinix chat interface.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public override void DoWindowContents(Rect inRect)
        {
            base.DoWindowContents(inRect);

            // Chat container
            Rect chatContainer = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width - (RIGHT_COLUMN_CONTAINER_WIDTH + COLUMN_SPACING),
                height: inRect.height
            );
            DrawChat(chatContainer);

            // Right column (settings and user list) container
            Rect rightColumnContainer = new Rect(
                x: inRect.xMax - RIGHT_COLUMN_CONTAINER_WIDTH,
                y: inRect.yMin,
                width: RIGHT_COLUMN_CONTAINER_WIDTH,
                height: inRect.height
            );
            DrawRightColumn(rightColumnContainer);
        }
        
        /// <summary>
        /// Handles chat message events raised by the client and adds them to the message list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void messageHandler(object sender, ChatMessageEventArgs args)
        {
            // Add the message
            lock (messagesLock) messages.Add(new ChatMessage(args.OriginUuid, args.Message));

            // Was this our message?
            if (args.OriginUuid == Instance.Uuid)
            {
                // Scroll to the bottom on next update
                scrollToBottom = true;
            }
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
                Find.WindowStack.Add(new SettingsWindow());
            }

            // User search box
            Rect searchBoxRect = new Rect(
                x: container.xMin,
                y: container.yMin + settingsButtonRect.yMax + DEFAULT_SPACING,
                width: USER_SEARCH_WIDTH,
                height: USER_SEARCH_HEIGHT
            );
            userSearch = Widgets.TextField(searchBoxRect, userSearch);

            // User list
            Rect userListRect = new Rect(
                x: container.xMin,
                y: container.yMin + searchBoxRect.yMax + DEFAULT_SPACING,
                width: USER_LIST_WIDTH,
                height: container.height - (searchBoxRect.yMax + DEFAULT_SPACING)
            );
            if (Instance.Online)
            {
                DrawUserList(userListRect);
            }
            else
            {
                DrawPlaceholder(userListRect);
            }
        }

        /// <summary>
        /// Draws the chat window - consisting of a message list, message entry box, and a send button - within the given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawChat(Rect container)
        {
            // Chat message area
            Rect chatAreaRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width,
                height: container.height - (CHAT_TEXTBOX_HEIGHT + DEFAULT_SPACING)
            );
            if (Instance.Online)
            {
                DrawMessages(chatAreaRect);
            }
            else
            {
                DrawPlaceholder(chatAreaRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }

            // Message entry box
            Rect messageEntryRect = new Rect(
                x: container.xMin,
                y: container.yMax - CHAT_TEXTBOX_HEIGHT,
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
            if (Widgets.ButtonText(sendButtonRect, "Phinix_chat_sendButton".Translate()))
            {
                // Send the message
                if (!string.IsNullOrEmpty(message) && Instance.Online)
                {
                    // TODO: Make chat message 'sent' callback to remove message, preventing removal of lengthy messages for nothing and causing frustration
                    Instance.SendMessage(message);

                    message = "";
                    scrollToBottom = true;
                }
            }
        }

        /// <summary>
        /// Draws each chat message within a scrollable container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawMessages(Rect container)
        {
            lock (messagesLock)
            {
                // Create a new flex container from our message list
                VerticalFlexContainer chatFlexContainer = new VerticalFlexContainer(container.width - SCROLLBAR_WIDTH, messages.Cast<IDrawable>());
                
                // Set up the scrollable container
                Rect innerContainer = new Rect(
                    x: container.xMin,
                    y: container.yMin,
                    width: container.width - SCROLLBAR_WIDTH,
                    height: chatFlexContainer.GetHeight(container.width - SCROLLBAR_WIDTH)
                );
                
                // Get a copy of the old scroll position
                Vector2 oldChatScroll = new Vector2(chatScroll.x, chatScroll.y);
                
                // Start scrolling
                Widgets.BeginScrollView(container, ref chatScroll, innerContainer);
            
                // Draw the flex container
                chatFlexContainer.Draw(innerContainer);
                
                // Stop scrolling
                Widgets.EndScrollView();

                // Enter the logic to get sticky scrolling to work
                #region Sticky scroll logic

                // Credit to Aze for figuring out how to get the bottom scroll pos
                bool scrolledToBottom = chatScroll.y.Equals(innerContainer.height - container.height);
                bool scrollChanged = !chatScroll.y.Equals(oldChatScroll.y);
                float heightDifference = oldHeight - innerContainer.height;

                if (scrollChanged)
                {
                    if (scrolledToBottom)
                    {
                        // Enable sticky scroll
                        stickyScroll = true;
                    }
                    else
                    {
                        // Not at bottom, disable sticky scroll
                        stickyScroll = false;
                    }
                }
                else if (!heightDifference.Equals(0f))
                {
                    if (stickyScroll || scrollToBottom)
                    {
                        // Scroll to bottom
                        chatScroll.y = innerContainer.height - container.height;
                        scrollToBottom = false;
                    }
                }
                
                // Update old height for the next pass
                oldHeight = innerContainer.height;
                
                #endregion
            }
        }
        
        /// <summary>
        /// Draws each logged in user within a scrollable container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawUserList(Rect container)
        {
            // Get a list of logged in user UUIDs
            string[] uuids = Instance.GetUserUuids(true);
            
            // Set up scrollable container
            Rect innerContainer = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width - SCROLLBAR_WIDTH,
                height: USER_HEIGHT * uuids.Length 
            );
            
            // Start scrolling
            Widgets.BeginScrollView(container, ref userListScroll, innerContainer);
            
            // Add each user to the scrollable container
            int userCount = 0;
            foreach (string uuid in uuids)
            {
                // Try to get the display name of the user
                if (!Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";

                // Skip the user if they don't contain the search text
                if (!string.IsNullOrEmpty(userSearch) && !displayName.ToLower().Contains(userSearch.ToLower())) continue;
                
                // Draw the user
                Rect userRect = new Rect(
                    x: innerContainer.xMin,
                    y: innerContainer.yMin + (USER_HEIGHT * userCount),
                    width: innerContainer.width,
                    height: USER_HEIGHT
                );
                Widgets.Label(userRect, displayName);
                
                // Increment the user count
                userCount++;
            }

            // Stop scrolling
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Draws a grey placeholder box over the container with the given text.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        /// <param name="text">Text to display</param>
        private void DrawPlaceholder(Rect container, string text = "")
        {
            // Background
            Widgets.DrawMenuSection(container);
            
            // Text
            Widgets.NoneLabelCenteredVertically(container, text);
        }
    }
}
