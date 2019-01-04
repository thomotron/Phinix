using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class ChatMessage : Displayable
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
        public override void Draw(Rect container)
        {
            // Draw a label with the formatted text
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
    }
}