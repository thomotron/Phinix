using System;
using Google.Protobuf.WellKnownTypes;

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
        /// Creates a new <see cref="ChatMessage"/> with the given sender UUID and message and sets the timestamp to now.
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
        /// Creates a new <see cref="ChatMessage"/> with the given sender UUID, message, and timestamp.
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

        /// <summary>
        /// Convert to a <see cref="ChatMessageStore"/>.
        /// </summary>
        /// <returns>Converted <see cref="ChatMessageStore"/></returns>
        public ChatMessageStore ToChatMessageStore()
        {
            return new ChatMessageStore
            {
                MessageId = MessageId,
                Timestamp = Timestamp.ToTimestamp(),
                SenderUuid = SenderUuid,
                Message = Message
            };
        }

        /// <summary>
        /// Recreates a <see cref="ChatMessage"/> from a <see cref="ChatMessageStore"/>.
        /// </summary>
        /// <param name="store"><see cref="ChatMessageStore"/> to create from</param>
        /// <returns>Recreated <see cref="ChatMessage"/></returns>
        public static ChatMessage FromChatMessageStore(ChatMessageStore store)
        {
            return new ChatMessage(
                messageId: store.MessageId,
                senderUuid: store.SenderUuid,
                message: store.Message,
                timestamp: store.Timestamp.ToDateTime()
            );
        }
    }
}