using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ChatMessage : IDrawable
    {
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
        
        public ChatMessage(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;
            
            this.ReceivedTime = DateTime.UtcNow;
        }

        public ChatMessage(string senderUuid, string message, DateTime receivedTime)
        {
            this.ReceivedTime = receivedTime;
            this.SenderUuid = senderUuid;
            this.Message = message;
        }

        public string Format()
        {
            // Try to get the display name of the sender
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";
            
            // Return the formatted message
            return string.Format("[{0:HH:mm}] {1}: {2}", ReceivedTime.ToLocalTime(), displayName, Message);
        }

        /// <inheritdoc />
        public void Draw(Rect container)
        {
            // Draw a button with the formatted text
            if (Widgets.ButtonText(container, Format(), false))
            {
                // Draw a context menu with user-specific actions
                drawContextMenu();
            }
        }

        /// <inheritdoc />
        public float GetHeight(float width)
        {
            // Return the calculated the height of the formatted text
            return Text.CalcHeight(Format(), width);
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException"><c>ChatMessage</c>s are always drawn at full width</exception>
        public float GetWidth(float height)
        {
            throw new NotSupportedException("ChatMessages are always drawn at full width.");
        }

        private void drawContextMenu()
        {
            // Do nothing if this is our UUID
            if (SenderUuid == Client.Instance.Uuid) return;
            
            // Try to get the display name of this message's sender
            if (!Client.Instance.TryGetDisplayName(SenderUuid, out string displayName)) displayName = "???";
            
            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Trade with " + displayName, () => Client.Instance.CreateTrade(SenderUuid)));
            
            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}