using System;
using System.Collections.Generic;
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
        
        public ChatMessageWidget(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;
            
            this.ReceivedTime = DateTime.UtcNow;
        }

        public ChatMessageWidget(string senderUuid, string message, DateTime receivedTime)
        {
            this.ReceivedTime = receivedTime;
            this.SenderUuid = senderUuid;
            this.Message = message;
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
            // Disabled due to bad text wrapping (as per issue #7)
            // BUG: Text doesn't wrap properly when drawing a button, but it works just fine when drawing a label
//            // Draw a button with the formatted text
//            if (Widgets.ButtonText(container, Client.Instance.ShowChatFormatting ? Format() : TextHelper.StripRichText(Format()), false))
//            {
//                // Draw a context menu with user-specific actions
//                drawContextMenu();
//            }
            
            Widgets.Label(container, Format());
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