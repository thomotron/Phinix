using System;
using System.Collections.Generic;
using Chat;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class ChatMessageWidget : Displayable
    {
        public override bool IsFluidHeight => false;

        private readonly Color pendingMessageColour = new Color(1f, 1f, 1f, 0.8f);
        private readonly Color deniedMessageColour = new Color(0.94f, 0.28f, 0.28f);
        private readonly Color backgroundHighlightColour = new Color(1f, 1f, 1f, 0.1f);

        /// <summary>
        /// ID of the corresponding chat message.
        /// </summary>
        public string MessageId;

        /// <summary>
        /// Time message was received.
        /// </summary>
        public DateTime ReceivedTime;

        /// <summary>
        /// UUID of the sender.
        /// </summary>
        public string SenderUuid;

        /// <summary>
        /// The message itself.
        /// </summary>
        public string Message;

        /// <summary>
        /// The status of the chat message.
        /// </summary>
        public ChatMessageStatus Status;

        /// <summary>
        /// A cached copy of the sender's display name.
        /// Refreshed every time <see cref="Update"/> is called.
        /// </summary>
        private string cachedDisplayName;
        /// <summary>
        /// A cashed copy of the sender's blocked state.
        /// Refreshed every time <see cref="Update"/> is called.
        /// </summary>
        private bool cachedBlockedState;

        public ChatMessageWidget(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;

            this.ReceivedTime = DateTime.UtcNow;
            this.Status = ChatMessageStatus.PENDING;

            // Pre-cache the user's display name and blocked status
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out cachedDisplayName)) cachedDisplayName = "???";
            cachedBlockedState = Client.Instance.BlockedUsers.Contains(SenderUuid);
        }

        public ChatMessageWidget(ClientChatMessage message)
            : this(message.MessageId, message.SenderUuid, message.Message, message.Timestamp, message.Status)
        {
        }

        public ChatMessageWidget(string senderUuid, string message, DateTime receivedTime, ChatMessageStatus status)
             : this(null, senderUuid, message, receivedTime, status)
        {
        }

        public ChatMessageWidget(string messageId, string senderUuid, string message, DateTime receivedTime, ChatMessageStatus status)
        {
            this.MessageId = messageId;
            this.ReceivedTime = receivedTime;
            this.SenderUuid = senderUuid;
            this.Message = message;
            this.Status = status;

            // Pre-cache the user's display name and blocked status
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out cachedDisplayName)) cachedDisplayName = "???";
            cachedBlockedState = Client.Instance.BlockedUsers.Contains(SenderUuid);
        }

        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Don't draw anything for blocked users
            if (cachedBlockedState) return;

            // Get the formatted chat message
            string timestamp = string.Format("[{0:HH:mm}] ", ReceivedTime.ToLocalTime());
            Rect timestampRect = new Rect(
                x: container.x,
                y: container.y,
                width: Text.CurFontStyle.CalcSize(new GUIContent(timestamp)).x,
                height: Text.CurFontStyle.CalcSize(new GUIContent(timestamp)).y
            );

            string displayName = Client.Instance.ShowNameFormatting ? cachedDisplayName : TextHelper.StripRichText(cachedDisplayName);
            Rect displayNameRect = new Rect(
                x: container.x + timestampRect.width,
                y: container.y,
                width: Text.CurFontStyle.CalcSize(new GUIContent(displayName)).x,
                height: Text.CurFontStyle.CalcSize(new GUIContent(displayName)).y
            );

            string message = Message;
            if (!Client.Instance.ShowChatFormatting) message = TextHelper.StripRichText(message);
            Rect messageRect = container;

            // Put all the pieces together
            string formattedText = timestamp + displayName + ": " + message;

            // Change the colour of the message to reflect the sent status
            switch (Status)
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

            if (Mouse.IsOver(messageRect))
            {
                // Draw a highlighted background
                Widgets.DrawRectFast(container, backgroundHighlightColour);
            }

            // Draw the message
            Widgets.Label(container, formattedText);

            // Handle any button clicks
            if (Widgets.ButtonInvisible(timestampRect, false))
            {
                // We don't care about the timestamp, but we don't want to trigger the message button, so this stays
            }
            else if (Widgets.ButtonInvisible(displayNameRect, true))
            {
                drawNameContextMenu();
            }
            else if (Widgets.ButtonInvisible(messageRect, false))
            {
                drawMessageContextMenu();
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">Chat message with the given message ID does not exist</exception>
        public override void Update()
        {
            // Update the message status if we've been given a message ID
            if (MessageId != null)
            {
                if (!Client.Instance.TryGetMessage(MessageId, out UIChatMessage message))
                {
                    throw new ArgumentException("Chat message with the given message ID does not exist.");
                }

                Status = message.Status;
            }

            // Update the sender's display name and blocked states
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out cachedDisplayName)) cachedDisplayName = "???";
            cachedBlockedState = Client.Instance.BlockedUsers.Contains(SenderUuid);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            if (cachedBlockedState) return 0;

            // Build a formatted representation of the message
            string formattedMessage = string.Format(
                "[{0:HH:mm}] {1}: {2}",
                ReceivedTime,
                Client.Instance.ShowNameFormatting && Status == ChatMessageStatus.CONFIRMED ? cachedDisplayName : TextHelper.StripRichText(cachedDisplayName),
                Client.Instance.ShowChatFormatting && Status == ChatMessageStatus.CONFIRMED ? Message : TextHelper.StripRichText(Message)
            );

            // Return the calculated the height of the formatted text
            return Text.CalcHeight(formattedMessage, width);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return FLUID;
        }

        private void drawNameContextMenu()
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();

            // Only add the trade option if this is not our message
            if (SenderUuid != Client.Instance.Uuid)
            {
                // Trade with...
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(cachedDisplayName)), () => Client.Instance.CreateTrade(SenderUuid)));

                // Block/Unblock user
                if (cachedBlockedState)
                {
                    // Unblock
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_unblockUser".Translate(), () => Client.Instance.UnBlockUser(SenderUuid)));
                }
                else
                {
                    // Block
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_blockUser".Translate(), () => Client.Instance.BlockUser(SenderUuid)));
                }
            }

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }

        private void drawMessageContextMenu()
        {
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyToClipboard".Translate(), () => { GUIUtility.systemCopyBuffer = Message; }));

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}
