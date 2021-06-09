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
        /// Time message was received.
        /// Set when the constructor is run.
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

        public ChatMessageWidget(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;

            this.ReceivedTime = DateTime.UtcNow;
            this.Status = ChatMessageStatus.PENDING;
        }

        public ChatMessageWidget(string senderUuid, string message, DateTime receivedTime, ChatMessageStatus status)
        {
            this.ReceivedTime = receivedTime;
            this.SenderUuid = senderUuid;
            this.Message = message;
            this.Status = status;
        }

        public string Format()
        {
            // Get a local copy of the message
            string message = Message;

            // Try to get the display name of the sender
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";

            // Strip name formatting if the user wishes not to see it
            if (!Client.Instance.ShowNameFormatting) displayName = TextHelper.StripRichText(displayName);

            // Strip message formatting if the user wishes not to see it
            if (!Client.Instance.ShowChatFormatting) message = TextHelper.StripRichText(message);

            // Return the formatted message
            return string.Format("[{0:HH:mm}] {1}: {2}", ReceivedTime.ToLocalTime(), displayName, message);
        }

        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Get the formatted chat message
            string timestamp = string.Format("[{0:HH:mm}] ", ReceivedTime.ToLocalTime());
            Rect timestampRect = new Rect(
                x: container.x,
                y: container.y,
                width: Text.CurFontStyle.CalcSize(new GUIContent(timestamp)).x,
                height: Text.CurFontStyle.CalcSize(new GUIContent(timestamp)).y
            );

            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";
            if (!Client.Instance.ShowNameFormatting) displayName = TextHelper.StripRichText(displayName);
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
        public override float CalcHeight(float width)
        {
            // Return the calculated the height of the formatted text
            return Text.CalcHeight(Format(), width);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return FLUID;
        }

        private void drawNameContextMenu()
        {
            // Try to get the display name of this message's sender
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();

            // Only add the trade option if this is not our message
            if (SenderUuid != Client.Instance.Uuid)
            {
                // Trade with...
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.CreateTrade(SenderUuid)));

                // Block/Unblock user
                if (!Client.Instance.BlockedUsers.Contains(SenderUuid))
                {
                    // Block
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_blockUser".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.BlockUser(SenderUuid)));
                }
                else
                {
                    // Unblock
                    items.Add(new FloatMenuOption("Phinix_chat_contextMenu_unblockUser".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.UnBlockUser(SenderUuid)));
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
