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
        /// The formatted message.
        /// </summary>
        private string formattedMessage;

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

        public ChatMessageWidget(ClientChatMessage message)
        {
            this.ReceivedTime = message.Timestamp;
            this.SenderUuid = message.SenderUuid;
            this.Message = message.Message;
            this.Status = message.Status;
        }

        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Disabled due to bad text wrapping (as per issue #7)
            // BUG: Text doesn't wrap properly when drawing a button, but it works just fine when drawing a label
//            // Draw a button with the formatted text
//            if (Widgets.ButtonText(container, Client.Instance.ShowChatFormatting ? Format() : TextHelper.StripRichText(Format()), false))
//            {
//                // Draw a context menu with user-specific actions
//                drawContextMenu();
//            }

            // Format the message if we haven't already done so
            if (formattedMessage == null) formattedMessage = format();

            // Change the colour of the message to reflect the sent status
            switch (Status)
            {
                case ChatMessageStatus.PENDING:
                    formattedMessage = string.Format("<color=#ffffff80>{0}</color>", TextHelper.StripRichText(formattedMessage));
                    break;
                case ChatMessageStatus.DENIED:
                    formattedMessage = string.Format("<color=#f04747>{0}</color>", TextHelper.StripRichText(formattedMessage));
                    break;
                default:
                    break;
            }

            Widgets.Label(container, formattedMessage);
        }

        /// <inheritdoc />
        public override void Update()
        {
            // Reformat the message with the latest data
            formattedMessage = format();
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            // Return the calculated the height of the formatted text
            return Text.CalcHeight(formattedMessage, width);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return FLUID;
        }

        private string format()
        {
            Client.Instance.Log(new LogEventArgs("A chat message just got formatted"));

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

        private void drawContextMenu()
        {
            // Do nothing if this is our UUID
            if (SenderUuid == Client.Instance.Uuid) return;

            // Try to get the display name of this message's sender
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Trade with " + TextHelper.StripRichText(displayName), () => Client.Instance.CreateTrade(SenderUuid)));

            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}