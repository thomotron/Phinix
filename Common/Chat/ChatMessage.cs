using System;

namespace Chat
{
    public class ChatMessage
    {
        /// <summary>
        /// Unique ID of this message.
        /// </summary>
        public string MessageId;
        
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
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        public ChatMessage(string messageId, string senderUuid, string message)
        {
            this.MessageId = messageId;
            this.SenderUuid = senderUuid;
            this.Message = message;
            
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a new <c>ChatMessage</c> with the given sender UUID, message, and timestamp.
        /// </summary>
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        /// <param name="timestamp">Timestamp</param>
        public ChatMessage(string messageId, string senderUuid, string message, DateTime timestamp)
        {
            this.MessageId = messageId;
            this.Timestamp = timestamp;
            this.SenderUuid = senderUuid;
            this.Message = message;
        }
    }
}