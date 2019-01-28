using System;

namespace Chat
{
    public class ChatMessage
    {
        /// <summary>
        /// Time the message was sent.
        /// </summary>
        public DateTime Timestamp;
        
        /// <summary>
        /// UUID of the sender.
        /// </summary>
        public string SenderUuid;
        
        /// <summary>
        /// The message itself.
        /// </summary>
        public string Message;
        
        /// <summary>
        /// Creates a new <c>ChatMessage</c> with the given sender UUID and message and sets the timestamp to now.
        /// </summary>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        public ChatMessage(string senderUuid, string message)
        {
            this.SenderUuid = senderUuid;
            this.Message = message;
            
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new <c>ChatMessage</c> with the given sender UUID, message, and timestamp.
        /// </summary>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        /// <param name="timestamp">Timestamp</param>
        public ChatMessage(string senderUuid, string message, DateTime timestamp)
        {
            this.Timestamp = timestamp;
            this.SenderUuid = senderUuid;
            this.Message = message;
        }
    }
}