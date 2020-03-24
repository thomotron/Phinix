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

        private static Vector2 userListScroll = new Vector2(0, 0);

        private static string message = "";
        private static string userSearch = "";

        private static Vector2 activeTradesScroll = new Vector2(0, 0);

        private static ChatMessageList chatMessageList = new ChatMessageList();

        private static TabsContainer contents;

        public ServerTab()
        {
            // Create a tab container to hold the chat and trade list
            contents = new TabsContainer();

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
            contents.AddTab("Phinix_tabs_chat".Translate(), chatRow);

            // Add the active trades tab
            contents.AddTab("Phinix_tabs_trades".Translate(), GenerateTradeRows());
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
            base.DoWindowContents(inRect);

            // Draw the tabs
            contents.Draw(inRect);
        }

        /// <summary>
        /// Generates a <see cref="VerticalFlexContainer"/> containing a settings button, user search box, and a user list.
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
        /// Generates a <see cref="VerticalFlexContainer"/> the chat window consisting of a message list, message entry box, and a send button.
        /// </summary>
        private VerticalFlexContainer GenerateChat()
        {
            // Create a flex container to hold the column elements
            VerticalFlexContainer column = new VerticalFlexContainer(DEFAULT_SPACING);

            // Chat message area
            column.Add(
                new ConditionalContainer(
                    childIfTrue: chatMessageList,
                    childIfFalse: new PlaceholderWidget(
                        text: "Phinix_chat_pleaseLogInPlaceholder".Translate()
                    ),
                    condition: () => Instance.Online
                )
            );

            // Create a flex container to hold the text field and button
            HorizontalFlexContainer messageEntryFlexContainer = new HorizontalFlexContainer();

            // Message entry field
            messageEntryFlexContainer.Add(
                new TextFieldWidget(
                    text: message,
                    onChange: newMessage => message = newMessage
                )
            );

            // Send button
            messageEntryFlexContainer.Add(
                new WidthContainer(
                    new ButtonWidget(
                        label: "Phinix_chat_sendButton".Translate(),
                        clickAction: sendChatMessage
                    ),
                    width: CHAT_SEND_BUTTON_WIDTH
                )
            );

            // Add the flex container to the column
            column.Add(
                new HeightContainer(
                    messageEntryFlexContainer,
                    height: CHAT_TEXTBOX_HEIGHT
                )
            );

            // Return the generated column
            return column;
        }

        /// <summary>
        /// Adds each logged in user to a scrollable container.
        /// </summary>
        /// <returns>A <see cref="VerticalScrollContainer"/> containing the user list</returns>
        private VerticalScrollContainer GenerateUserList()
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

                // Strip name formatting if the user wishes not to see it
                if (!Instance.ShowNameFormatting) displayName = TextHelper.StripRichText(displayName);

                userListFlexContainer.Add(
                    new ButtonWidget(
                        label: displayName,
                        clickAction: () => DrawUserContextMenu(uuid, displayName),
                        drawBackground: false
                    )
                );
            }

            // Wrap the flex container in a scroll container
            VerticalScrollContainer verticalScrollContainer = new VerticalScrollContainer(userListFlexContainer, userListScroll, newScrollPos => userListScroll = newScrollPos);

            // Return the scroll container
            return verticalScrollContainer;
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
        /// Generates a <see cref="VerticalScrollContainer"/> containing a series of available trades.
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
            return new VerticalScrollContainer(column, activeTradesScroll, newScrollPos => activeTradesScroll = newScrollPos);
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
                chatMessageList.ScrollToBottom();
            }
        }
    }
}
