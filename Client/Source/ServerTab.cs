using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Chat;
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
            Rect chatAreaRect = container.TopPartPixels(container.height - (CHAT_TEXTBOX_HEIGHT + DEFAULT_SPACING));
            if (Instance.Online)
            {
                DrawMessages(chatAreaRect);
            }
            else
            {
                DrawPlaceholder(chatAreaRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }
            
            // Message entry field
            TextFieldWidget messageField = new TextFieldWidget(message, newMessage => message = newMessage);
            
            // Send button
            ButtonWidget button = new ButtonWidget(
                label: "Phinix_chat_sendButton".Translate(),
                clickAction: () =>
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
            );
            Container buttonWrapper = new Container(
                child: button,
                width: CHAT_SEND_BUTTON_WIDTH,
                height: CHAT_SEND_BUTTON_HEIGHT
            );
            
            // Fit the text field and button within a flex container
            HorizontalFlexContainer messageEntryFlexContainer = new HorizontalFlexContainer(new Displayable[]{messageField, buttonWrapper});
            
            // Draw the flex container
            messageEntryFlexContainer.Draw(container.BottomPartPixels(CHAT_TEXTBOX_HEIGHT));
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
                VerticalFlexContainer chatFlexContainer = new VerticalFlexContainer(messages.Cast<Displayable>(), 0f);
                
                // Set up the scrollable container
                Rect innerContainer = new Rect(
                    x: container.xMin,
                    y: container.yMin,
                    width: container.width - SCROLLBAR_WIDTH,
                    height: chatFlexContainer.CalcHeight(container.width - SCROLLBAR_WIDTH)
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
            // Create a flex container to hold the users
            VerticalFlexContainer userListFlexContainer = new VerticalFlexContainer();
            
            // Add each logged in user to the flex container
            foreach (string uuid in Instance.GetUserUuids(true))
            {
                // Try to get the display name of the user
                if (!Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";
                
                userListFlexContainer.Add(new TextWidget(displayName));
            }
            
            // Wrap the flex container in a scroll container
            ScrollContainer scrollContainer = new ScrollContainer(userListFlexContainer, userListScroll, newScrollPos => userListScroll = newScrollPos);
            
            // Draw the scroll container
            scrollContainer.Draw(container);
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
