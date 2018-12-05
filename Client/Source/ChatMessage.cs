using System;

namespace PhinixClient
{
    public class ChatMessage
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
    }
}