using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chat;
using Trading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class ChatMessageList
    {
        private const float SCROLLBAR_WIDTH = 16f;

        private readonly Color pendingMessageColour = new Color(1f, 1f, 1f, 0.8f);
        private readonly Color deniedMessageColour = new Color(0.94f, 0.28f, 0.28f);
        private readonly Color backgroundHighlightColour = new Color(1f, 1f, 1f, 0.1f);

        /// <summary>
        /// List of filtered chat messages.
        /// </summary>
        private readonly List<UIChatMessage> filteredMessages = new List<UIChatMessage>();
        /// <summary>
        /// List of new chat messages for the UI thread to replace <see cref="filteredMessages"/> with.
        /// </summary>
        private readonly List<UIChatMessage> messages = new List<UIChatMessage>();
        /// <summary>
        /// Whether <see cref="messages"/> has been modified and <see cref="filteredMessages"/> should be repopulated by
        /// the UI thread.
        /// </summary>
        private bool messagesChanged = false;
        /// <summary>
        /// Lock object protecting <see cref="messages"/>.
        /// </summary>
        private readonly object messagesLock = new object();

        /// <summary>
        /// Cache for chat message positions and sizes to reduce load on the UI thread.
        /// </summary>
        private readonly Dictionary<string, Rect> messageRectCache = new Dictionary<string, Rect>();

        // A collection of state variables for the sticky scroll logic
        private Vector2 chatScroll = new Vector2(0, 0);
        private float oldHeight = 0f;
        private bool scrollToBottom = false;
        private bool stickyScroll = true;

        /// <summary>
        /// Whether to clear messages the next time <see cref="Draw"/> is called.
        /// </summary>
        private bool clearMessages = false;

        /// <summary>
        /// Creates a new <see cref="ChatMessageList" /> and populates it with all received chat messages.
        /// </summary>
        public ChatMessageList()
        {
            // Subscribe to events
            // TODO: Unsubscribe from the event when being destroyed (not that it will be until Phinix shuts down)
            Client.Instance.OnChatMessageReceived += ChatMessageReceivedEventHandler;
            Client.Instance.OnUserDisplayNameChanged += UserChangedEventHandler;
            Client.Instance.OnBlockedUsersChanged += BlockedUsersChangedEventHandler;
            Client.Instance.OnChatSync += (s, e) => ReplaceWithBuffer();
            Client.Instance.OnDisconnect += (s, e) => Clear();
        }

        public void Draw(Rect inRect) {
            // Clear the message list if requested
            if (clearMessages)
            {
                filteredMessages.Clear();
                clearMessages = false;

                // Rebuild the message rect cache
                recalculateMessageRects(inRect);
            }

            // Repopulate the messages list if necessary
            if (messagesChanged)
            {
                if (Monitor.TryEnter(messagesLock))
                {
                    // Clear and replace the filtered messages list
                    filteredMessages.Clear();
                    filteredMessages.AddRange(messages);
                    messagesChanged = false;

                    // Mark the messages as read
                    Client.Instance.MarkAsRead();

                    Monitor.Exit(messagesLock);

                    // Rebuild the message rect cache
                    recalculateMessageRects(inRect);
                }
            }

            // Set up the scrollable container
            Rect innerContainer = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width - SCROLLBAR_WIDTH,
                height: messageRectCache.Values.Sum(r => r.height)
            );

            // Get a copy of the old scroll position
            Vector2 oldChatScroll = new Vector2(chatScroll.x, chatScroll.y);

            // Start scrolling
            Widgets.BeginScrollView(inRect, ref chatScroll, innerContainer);

            // Draw the message
            foreach (UIChatMessage chatMessage in filteredMessages)
            {
                drawChatMessage(messageRectCache[chatMessage.MessageId], chatMessage);
            }

            // Stop scrolling
            Widgets.EndScrollView();

            // Enter the logic to get sticky scrolling to work
            #region Sticky scroll logic

            // Credit to Aze for figuring out how to get the bottom scroll pos
            bool scrolledToBottom = chatScroll.y.Equals(innerContainer.height - inRect.height);
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
                    chatScroll.y = innerContainer.height - inRect.height;
                    scrollToBottom = false;
                }
            }

            // Update old height for the next pass
            oldHeight = innerContainer.height;

            #endregion
        }

        /// <summary>
        /// Scrolls to the bottom of the list.
        /// </summary>
        public void ScrollToBottom()
        {
            scrollToBottom = true;
        }

        /// <summary>
        /// Clears the chat.
        /// </summary>
        public void Clear()
        {
            lock (messagesLock)
            {
                // Clear new message list and flag the main list to be cleared next frame
                messages.Clear();
                clearMessages = true;
            }
        }

        /// <summary>
        /// Clears the list and populates it with the messages in the chat buffer.
        /// </summary>
        /// <seealso cref="Client.GetChatMessages"/>
        public void ReplaceWithBuffer()
        {
            lock (messagesLock)
            {
                Clear();

                // Append the buffered messages to the list
                messages.AddRange(Client.Instance.GetChatMessages());
                messagesChanged = true;
            }
        }

        private void ChatMessageReceivedEventHandler(object sender, UIChatMessageEventArgs args)
        {
            lock (messagesLock)
            {
                // Append the new message to the list
                messages.Add(args.Message);
                messagesChanged = true;
            }
        }

        private void UserChangedEventHandler(object sender, UserDisplayNameChangedEventArgs args)
        {
            lock (messagesLock)
            {
                // Update the user's display name in each of their messages
                foreach (UIChatMessage chatMessage in messages.Where(m => m.User.Uuid == args.Uuid))
                {
                    chatMessage.User = new ImmutableUser(chatMessage.User.Uuid, args.NewDisplayName, chatMessage.User.LoggedIn, chatMessage.User.AcceptingTrades);
                }

                messagesChanged = true;
            }
        }

        private void BlockedUsersChangedEventHandler(object sender, BlockedUsersChangedEventArgs args)
        {
            lock (messagesLock)
            {
                if (args.IsBlocked)
                {
                    // Remove all their messages from the list
                    messages.RemoveAll(m => m.User.Uuid == args.Uuid);
                }
                else
                {
                    // Pull in the chat buffer to repopulate messages from the now-unblocked user
                    // TODO: Make this less expensive
                    ReplaceWithBuffer();
                }

                messagesChanged = true;
            }
        }

        private void recalculateMessageRects(Rect inRect)
        {
            // Clear the existing cache
            messageRectCache.Clear();

            float currentY = inRect.yMin;
            foreach (UIChatMessage chatMessage in filteredMessages)
            {
                // Build a formatted representation of the message
                string formattedMessage = string.Format(
                    "[{0:HH:mm}] {1}: {2}",
                    chatMessage.Timestamp,
                    Client.Instance.ShowNameFormatting && chatMessage.Status == ChatMessageStatus.CONFIRMED ? chatMessage.User.DisplayName : TextHelper.StripRichText(chatMessage.User.DisplayName),
                    Client.Instance.ShowChatFormatting && chatMessage.Status == ChatMessageStatus.CONFIRMED ? chatMessage.Message : TextHelper.StripRichText(chatMessage.Message)
                );

                // Calculate the message sizing
                Rect messageRect = new Rect(
                    x: inRect.x,
                    y: currentY,
                    width: inRect.width,
                    height: Text.CalcHeight(formattedMessage, inRect.width)
                );

                // Cache the result
                try
                {
                    messageRectCache.Add(chatMessage.MessageId, messageRect);
                }
                catch (ArgumentException)
                {
                    // Client will fail to draw subsequent messages with this ID, but may recover after one or more updates to the message list
                    // A broken UI is more usable than none at all, simply log it and keep going
                    Client.Instance.Log(new LogEventArgs(string.Format("Found existing chat message with key {0} when recalculating messageRectCache. Chat may fail to draw messages with this ID until it's updated again!", chatMessage.MessageId), LogLevel.ERROR));
                }

                currentY += messageRect.height;
            }
        }

        private void drawChatMessage(Rect inRect, UIChatMessage chatMessage)
        {
            // Get the formatted chat message
            string timestamp = string.Format("[{0:HH:mm}] ", chatMessage.Timestamp.ToLocalTime());
            Vector2 timestampSize = Text.CurFontStyle.CalcSize(new GUIContent(timestamp));
            Rect timestampRect = new Rect(
                x: inRect.x,
                y: inRect.y,
                width: timestampSize.x,
                height: timestampSize.y
            );

            string displayName = Client.Instance.ShowNameFormatting ? chatMessage.User.DisplayName : TextHelper.StripRichText(chatMessage.User.DisplayName);
            Vector2 displayNameSize = Text.CurFontStyle.CalcSize(new GUIContent(displayName));
            Rect displayNameRect = new Rect(
                x: inRect.x + timestampRect.width,
                y: inRect.y,
                width: displayNameSize.x,
                height: displayNameSize.y
            );

            string message = chatMessage.Message;
            if (!Client.Instance.ShowChatFormatting) message = TextHelper.StripRichText(message);

            // Put all the pieces together
            string formattedText = string.Format("{0}{1}: {2}", timestamp, displayName, message);

            // Change the colour of the message to reflect the sent status
            switch (chatMessage.Status)
            {
                case ChatMessageStatus.PENDING:
                    formattedText = TextHelper.StripRichText(formattedText).Colorize(pendingMessageColour);
                    break;
                case ChatMessageStatus.DENIED:
                    formattedText = TextHelper.StripRichText(formattedText).Colorize(deniedMessageColour);
                    break;
                default:
                    break;
            }

            if (Mouse.IsOver(inRect))
            {
                // Draw a highlighted background
                Widgets.DrawRectFast(inRect, backgroundHighlightColour);
            }

            // Draw the message
            Widgets.Label(inRect, formattedText);

            // Handle any button clicks
            if (Widgets.ButtonInvisible(timestampRect, false))
            {
                // We don't care about the timestamp, but we don't want to trigger the message button, so this stays
            }
            else if (Widgets.ButtonInvisible(displayNameRect, true))
            {
                drawNameContextMenu(chatMessage.User);
            }
            else if (Widgets.ButtonInvisible(inRect, false))
            {
                drawMessageContextMenu(chatMessage);
            }
        }

        private void drawNameContextMenu(ImmutableUser user)
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();

            // Only add the trade option if this is not our message
            if (user.Uuid != Client.Instance.Uuid)
            {
                // Trade with...
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(user.DisplayName)), () => Client.Instance.CreateTrade(user.Uuid)));

                // Block/Unblock user
                if (Client.Instance.BlockedUsers.Contains(user.Uuid))
                {
                    // Unblock
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_unblockUser".Translate(), () => Client.Instance.UnBlockUser(user.Uuid)));
                }
                else
                {
                    // Block
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_blockUser".Translate(), () => Client.Instance.BlockUser(user.Uuid)));
                }
            }

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }

        private void drawMessageContextMenu(ClientChatMessage chatMessage)
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyToClipboard".Translate(), () => { GUIUtility.systemCopyBuffer = chatMessage.Message; }));

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}