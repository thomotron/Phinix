using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        private static readonly Regex UrlRegex = new Regex(@"https?:\/\/\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            Client.Instance.OnBlockedUsersChanged += (s, e) => ReplaceWithBuffer();
            Client.Instance.OnChatSync += (s, e) => ReplaceWithBuffer();
            Client.Instance.OnDisconnect += (s, e) => Clear();
            Client.Instance.OnChatMessageLimitChanged += (s, e) => ReplaceWithBuffer();

            // Populate the message list before first draw
            ReplaceWithBuffer();
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
            bool heightChanged = !(oldHeight - innerContainer.height).Equals(0f);

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
            else if ((heightChanged && stickyScroll) || scrollToBottom)
            {
                // Scroll to bottom
                chatScroll.y = innerContainer.height - inRect.height;
                scrollToBottom = false;
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

                // Append the buffered messages to the list, filtering out any blocked users and trimming it to the limit
                messages.AddRange(
                    Client.Instance.GetChatMessages()
                        .Where(m => !Client.Instance.Settings.BlockedUsers.Contains(m.SenderUuid))
                        .Skip(Math.Max(0, messages.Count() - Client.Instance.Settings.ChatMessageLimit))
                );
                messagesChanged = true;
            }
        }

        private void ChatMessageReceivedEventHandler(object sender, UIChatMessageEventArgs args)
        {
            if (Client.Instance.Settings.BlockedUsers.Contains(args.Message.User.Uuid)) return;

            lock (messagesLock)
            {
                // Append the new message to the list
                messages.Add(args.Message);
                messagesChanged = true;

                // Trim the list down to the buffer limit if needed
                messages.RemoveRange(0, Math.Max(0, messages.Count - Client.Instance.Settings.ChatMessageLimit));
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
                    chatMessage.Status == ChatMessageStatus.CONFIRMED ? Client.ShownName(chatMessage.User.DisplayName) : TextHelper.StripRichText(chatMessage.User.DisplayName),
                    ScreenshotSharing.ShowLinks(Emoji.Placeholders(Emoji.Mark(Client.Instance.Settings.ShowChatFormatting && chatMessage.Status == ChatMessageStatus.CONFIRMED ? chatMessage.Message : TextHelper.StripRichText(chatMessage.Message), new List<string>()), hidden: false), colour: false)
                );

                // Calculate the message sizing
                Rect messageRect = new Rect(
                    x: inRect.x,
                    y: currentY,
                    width: inRect.width - SCROLLBAR_WIDTH,
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

            string displayName = Client.ShownName(chatMessage.User.DisplayName);
            Vector2 displayNameSize = Text.CurFontStyle.CalcSize(new GUIContent(displayName));
            Rect displayNameRect = new Rect(
                x: inRect.x + timestampRect.width,
                y: inRect.y,
                width: displayNameSize.x,
                height: displayNameSize.y
            );

            string message = chatMessage.Message;
            if (!Client.Instance.Settings.ShowChatFormatting) message = TextHelper.StripRichText(message);

            // :shortcodes: become emojis
            List<string> emojis = new List<string>();
            message = Emoji.Mark(message, emojis);

            // A shared screenshot's tag shows as a link (clicking the message opens it, see below)
            message = ScreenshotSharing.ShowLinks(message, colour: true);

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
                Widgets.DrawHighlight(inRect);
            }

            // Draw the message
            Emoji.Label(inRect, formattedText, emojis);

            // Hovering over their name shows their bio, if they've written one
            if (chatMessage.User.Uuid != null && Client.Instance.TryGetBio(chatMessage.User.Uuid, out string bio)) TooltipHandler.TipRegion(displayNameRect, bio);

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
                // Left-clicking a message with a screenshot opens it; right-click still gives the menu
                if (Event.current.button == 0 && ScreenshotSharing.TryFindId(chatMessage.Message, out string screenshotId))
                {
                    ScreenshotSharing.Open(screenshotId);
                }
                else
                {
                    drawMessageContextMenu(chatMessage);
                }
            }
        }

        private void drawNameContextMenu(ImmutableUser user)
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();

            // Only add the trade option if this is not our message
            if (user.Uuid != Client.Instance.Uuid)
            {
                // Their card: bio, last seen, mods
                if (Client.Instance.FeatureEnabled(Features.PROFILES)) items.Add(new FloatMenuOption("Phinix_chat_contextMenu_viewProfile".Translate(), () => Find.WindowStack.Add(new PlayerCardWindow(user.Uuid))));

                // Trade with...
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(user.DisplayName)), () => Client.Instance.CreateTrade(user.Uuid)));

                // Add/Remove friend
                if (!Client.Instance.FriendsEnabled)
                {
                    // Switched off on this server
                }
                else if (Client.Instance.IsFriend(user.Uuid))
                {
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_removeFriend".Translate(), () => Client.Instance.RemoveFriend(user.Uuid)));
                }
                else
                {
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_addFriend".Translate(), () => Client.Instance.AddFriend(user.Uuid)));
                }

                // Block/Unblock user
                if (Client.Instance.Settings.BlockedUsers.Contains(user.Uuid))
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

            // Dev mode only: their name with its formatting tags, for looking into names that draw wrong
            if (Prefs.DevMode) items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyRawName".Translate(), () => GUIUtility.systemCopyBuffer = user.DisplayName));

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }

        private void drawMessageContextMenu(ClientChatMessage chatMessage)
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();

            if (ScreenshotSharing.TryFindId(chatMessage.Message, out string screenshotId))
            {
                items.Add(new FloatMenuOption("Phinix_screenshot_view".Translate(), () => ScreenshotSharing.Open(screenshotId)));
            }

            foreach (string url in parseUrls(chatMessage.Message.StripTags()))
            {
                string ellipsisedUrl = url.Length > 100 ? $"{url.Substring(0, 100)}..." : url;
                items.Add(new FloatMenuOption(string.Format("Phinix_chat_contextMenu_openInBrowser".Translate(), ellipsisedUrl), () => Application.OpenURL(url)));
            }

            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyToClipboard".Translate(), () => GUIUtility.systemCopyBuffer = chatMessage.Message));

            // Someone else's message the server has confirmed (so it has its real ID) can be reported
            if (chatMessage.SenderUuid != Client.Instance.Uuid && chatMessage.Status == ChatMessageStatus.CONFIRMED)
            {
                items.Add(new FloatMenuOption("Phinix_report_menuOption".Translate(), () => Find.WindowStack.Add(new ReportMessageWindow(MessageReports.ForChatMessage(chatMessage)))));
            }

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// Attempts to parse one or more URLs from a message.
        /// </summary>
        /// <param name="message">Message to parse.</param>
        /// <returns>A collection of URLs contained in the message.</returns>
        private IEnumerable<string> parseUrls(string message)
        {
            if (message == null || message.Length == 0) yield break;

            MatchCollection matches = UrlRegex.Matches(message);
            foreach (Match match in matches)
            {
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri matchUri))
                {
                    yield return matchUri.ToString();
                }
            }
        }
    }
}