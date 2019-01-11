using System;

namespace Chat
{
    public class ChatMessage
    {
        /// <summary>
        /// Time the message was received.
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
        /// Creates a new <c>ChatMessage</c> with the given sender UUID and message and sets the time received to now.
        /// </summary>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        public ChatMessage(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;
            
            this.ReceivedTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new <c>ChatMessage</c> with the given sender UUID, message, and time received.
        /// </summary>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        public ChatMessage(string senderUuid, string message, DateTime receivedTime)
        {
            this.ReceivedTime = receivedTime;
            this.SenderUuid = senderUuid;
            this.Message = message;
        }
    }
}