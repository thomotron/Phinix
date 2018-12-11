using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ChatMessageFlexContainer
    {
        /// <summary>
        /// List of <c>ChatMessage</c>s within the container.
        /// </summary>
        private List<ChatMessage> messages;

        /// <summary>
        /// Creates a new <c>ChatMessageFlexContainer</c>.
        /// </summary>
        public ChatMessageFlexContainer()
        {
            this.messages = new List<ChatMessage>();
        }

        /// <summary>
        /// Creates a new <c>ChatMessageFlexContainer</c> from an existing list of <c>ChatMessage</c>s.
        /// </summary>
        /// <param name="messages">List of <c>ChatMessages</c></param>
        public ChatMessageFlexContainer(List<ChatMessage> messages)
        {
            this.messages = messages;
        }

        /// <summary>
        /// Adds a <c>ChatMessage</c> to the container.
        /// </summary>
        /// <param name="message"><c>ChatMessage</c> to add</param>
        public void Add(ChatMessage message)
        {
            messages.Add(message);
        }

        /// <summary>
        /// Draws each message within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        public void Draw(Rect container)
        {
            float offset = 0f;
            foreach (ChatMessage message in messages)
            {
                // Format the message
                string formattedMessage = message.Format();

                // Calculate the formatted message height
                float formattedMessageHeight = Text.CalcHeight(formattedMessage, container.width);
                
                // Draw the message
                Rect chatMessageRect = new Rect(
                    x: container.xMin,
                    y: container.yMin + offset,
                    width: container.width,
                    height: formattedMessageHeight
                );
                Widgets.Label(chatMessageRect, formattedMessage);
                
                // Add the message height to the offset
                offset += formattedMessageHeight;
            }
        }

        /// <summary>
        /// Calculates the height of the container with the given width.
        /// </summary>
        /// <returns>Height of the container</returns>
        public float GetHeight(float width)
        {
            float height = 0f;
            foreach (ChatMessage message in messages)
            {
                // Calculate the height and add it to the sum
                height += Text.CalcHeight(message.Format(), width);
            }

            return height;
        }
    }
}