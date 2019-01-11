using System.Collections.Generic;
using System.Linq;
using Chat;
using PhinixClient.GUI;
using RimWorld;
using UnityEngine;
using Utils;
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

        private static int currentTabIndex;

        private static Vector2 chatScroll = new Vector2(0, 0);
        private static float oldHeight = 0f;
        private static bool scrollToBottom = false;
        private static bool stickyScroll = true;

        private static Vector2 userListScroll = new Vector2(0, 0);

        private static string message = "";
        private static string userSearch = "";
        
        private static Vector2 activeTradesScroll = new Vector2(0, 0);

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
            
            // Create a tab container to hold the chat and trade list
            TabsContainer tabContainer = new TabsContainer(newTabIndex => currentTabIndex = newTabIndex, currentTabIndex);
            
            // Create a flex container to hold the chat tab content
            HorizontalFlexContainer chatRow = new HorizontalFlexContainer(DEFAULT_SPACING);

            // Chat container
            chatRow.Add(
                GenerateChat()
            );

            // Right column (settings and user list) container
            chatRow.Add(
                new Container(
                    GenerateRightColumn(),
                    width: RIGHT_COLUMN_CONTAINER_WIDTH
                )
            );
            
            // Add the chat row as a tab
            tabContainer.AddTab("Phinix_tabs_chat".Translate(), chatRow);
            
            // Add the active trades tab
            tabContainer.AddTab("Phinix_tabs_trades".Translate(), GenerateTradeRows());
            
            // Draw the tabs
            tabContainer.Draw(inRect);
        }

        /// <summary>
        /// Generates a <c>VerticalFlexContainer</c> containing a settings button, user search box, and a user list.
        /// </summary>
        private VerticalFlexContainer GenerateRightColumn()
        {
            // Create a flex container to hold the column elements
            VerticalFlexContainer column = new VerticalFlexContainer();

            // Settings button
            column.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_chat_settingsButton".Translate(),
                        clickAction: () => Find.WindowStack.Add(new SettingsWindow())
                    ),
                    height: SETTINGS_BUTTON_HEIGHT
                )
            );

            // User search box
            column.Add(
                new Container(
                    new TextFieldWidget(
                        text: userSearch,
                        onChange: (newText) => userSearch = newText
                    ),
                    height: USER_SEARCH_HEIGHT
                )
            );

            // User list
            if (Instance.Online)
            {
                column.Add(GenerateUserList());
            }
            else
            {
                column.Add(new PlaceholderWidget());
            }

            // Return the generated column
            return column;
        }

        /// <summary>
        /// Generates a <c>VerticalFlexContainer</c> the chat window consisting of a message list, message entry box, and a send button.
        /// </summary>
        private VerticalFlexContainer GenerateChat()
        {
            // Create a flex container to hold the column elements
            VerticalFlexContainer column = new VerticalFlexContainer(DEFAULT_SPACING);
            
            // Chat message area
            if (Instance.Online)
            {
                column.Add(
                    GenerateMessages()
                );
            }
            else
            {
                column.Add(
                    new PlaceholderWidget(
                        text: "Phinix_chat_pleaseLogInPlaceholder".Translate()
                    )
                );
            }

            // Message entry field
            TextFieldWidget messageField = new TextFieldWidget(
                text: message,
                onChange: newMessage => message = newMessage
            );

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

            // Add the flex container to the column
            column.Add(
                new Container(
                    messageEntryFlexContainer,
                    height: CHAT_TEXTBOX_HEIGHT
                )
            );
            
            // Return the generated column
            return column;
        }

        /// <summary>
        /// Draws each chat message within a scrollable container.
        /// </summary>
        private AdapterWidget GenerateMessages()
        {
            return new AdapterWidget(
                drawCallback: container =>
                {
                    // Get all chat messages and convert them to widgets
                    ChatMessage[] messages = Instance.GetChatMessages();
                    ChatMessageWidget[] messageWidgets = messages.Select(message => new ChatMessageWidget(message.SenderUuid, message.Message, message.ReceivedTime)).ToArray();
                    
                    // Create a new flex container from our message list
                    VerticalFlexContainer chatFlexContainer = new VerticalFlexContainer(messageWidgets, 0f);

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
            );
        }

        /// <summary>
        /// Adds each logged in user to a scrollable container.
        /// </summary>
        /// <returns>A <c>ScrollContainer</c> containing the user list</returns>
        private ScrollContainer GenerateUserList()
        {
            // Create a flex container to hold the users
            VerticalFlexContainer userListFlexContainer = new VerticalFlexContainer();

            // Add each logged in user to the flex container
            foreach (string uuid in Instance.GetUserUuids(true))
            {
                // Try to get the display name of the user
                if (!Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";

                // Skip the user if they don't contain the search text
                if (!string.IsNullOrEmpty(userSearch) && !displayName.ToLower().Contains(userSearch.ToLower())) continue;

                userListFlexContainer.Add(
                    new ButtonWidget(
                        label: displayName,
                        clickAction: () => DrawUserContextMenu(uuid, displayName),
                        drawBackground: false
                    )
                );
            }

            // Wrap the flex container in a scroll container
            ScrollContainer scrollContainer = new ScrollContainer(userListFlexContainer, userListScroll, newScrollPos => userListScroll = newScrollPos);

            // Return the scroll container
            return scrollContainer;
        }

        /// <summary>
        /// Draws a grey placeholder box over the container with the given text.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        /// <param name="text">Text to display</param>
        private void DrawPlaceholder(Rect container, string text = "")
        {
            PlaceholderWidget placeholder = new PlaceholderWidget(text);

            placeholder.Draw(container);
        }
        
        /// <summary>
        /// Draws a context menu with user-specific actions.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="displayName">User's display name</param>
        private void DrawUserContextMenu(string uuid, string displayName)
        {
            // Do nothing if this is our UUID
            if (uuid == Instance.Uuid) return;
            
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(displayName)), () => Instance.CreateTrade(uuid)));
            
            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// Generates a <c>ScrollContainer</c> containing a series of available trades.
        /// </summary>
        /// <returns></returns>
        private Displayable GenerateTradeRows()
        {
            // Make sure we are online and have active trades before attempting to draw them
            if (!Instance.Online)
            {
                return new PlaceholderWidget("Phinix_chat_pleaseLogInPlaceholder".Translate());
            }
            else if (Instance.GetTrades().Count() == 0)
            {
                return new PlaceholderWidget("Phinix_trade_noActiveTradesPlaceholder".Translate());
            }
            
            // Create a column to store everything in
            VerticalFlexContainer column = new VerticalFlexContainer(DEFAULT_SPACING);

            // Get TradeRows for each trade and add them to the column
            string[] tradeIds = Instance.GetTrades();
            for (int i = 0; i < tradeIds.Length; i++)
            {
                column.Add(
                    new TradeRow(
                        tradeId: tradeIds[i],
                        drawAlternateBackground: i % 2 != 0
                     )
                );
            }

            // Return the generated column wrapped in a scroll container
            return new ScrollContainer(column, activeTradesScroll, newScrollPos => activeTradesScroll = newScrollPos);
        }
    }
}
