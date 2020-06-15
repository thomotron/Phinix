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

        private static string message = "";
        private static string userSearch = "";

        private static Vector2 activeTradesScroll = new Vector2(0, 0);

        private static ChatMessageList chatMessageList;
        private UserList userList;
        private TextFieldWidget messageBox;

        private static TabsContainer contents;

        public ServerTab()
        {
            // Generate the chat and user list
            chatMessageList = new ChatMessageList();
            userList = new UserList(() => userSearch);
            messageBox = new TextFieldWidget(
                initialText: message,
                onChange: newMessage => message = newMessage
            );

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
                        initialText: userSearch,
                        onChange: (newText) =>
                        {
                            userSearch = newText;
                            userList.Update();
                        }
                    ),
                    height: USER_SEARCH_HEIGHT
                )
            );

            // User list
            column.Add(
                new ConditionalContainer(
                    childIfTrue: userList,
                    childIfFalse: new PlaceholderWidget(),
                    condition: () => Instance.Online
                )
            );

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
            messageEntryFlexContainer.Add(messageBox);

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
        /// Generates a <see cref="VerticalScrollContainer"/> containing a series of available trades.
        /// </summary>
        /// <returns></returns>
        private Displayable GenerateTradeRows()
        {
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

            // Wrap the column in a scroll container
            VerticalScrollContainer scrolledColumn = new VerticalScrollContainer(column);

            // Make sure we have active trades before attempting to draw them
            ConditionalContainer activeTradesConditional = new ConditionalContainer(
                childIfTrue: scrolledColumn,
                childIfFalse: new PlaceholderWidget("Phinix_trade_noActiveTradesPlaceholder".Translate()),
                condition: () => Instance.GetTrades().Any()
            );

            // Make sure we are online above all else
            ConditionalContainer onlineConditional = new ConditionalContainer(
                childIfTrue: activeTradesConditional,
                childIfFalse: new PlaceholderWidget("Phinix_chat_pleaseLogInPlaceholder".Translate()),
                condition: () => Instance.Online
            );

            // Return the generated panel
            return onlineConditional;
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
                messageBox.Text = "";
                chatMessageList.ScrollToBottom();
            }
        }
    }
}
