using System;

namespace Chat
{
    /// <summary>
    /// Client variant of <see cref="ChatMessage"/> featuring a status field to indicate whether the message was received by
    /// the server and if it was accepted.
    /// </summary>
    public class ClientChatMessage : ChatMessage
    {
        /// <summary>
        /// The status of the chat message.
        /// </summary>
        public ChatMessageStatus Status;

        /// <inheritdoc />
        /// <summary>
        /// Creates a new <see cref="ClientChatMessage"/> with the given message ID, sender UUID, and message and sets the timestamp to now with a pending status.
        /// </summary>
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        public ClientChatMessage(string messageId, string senderUuid, string message) : base(messageId, senderUuid, message)
        {
            this.Status = ChatMessageStatus.PENDING;
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new <see cref="ClientChatMessage"/> with the given message ID, sender UUID, message, and status and sets the timestamp to now.
        /// </summary>
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        /// <param name="status">Status</param>
        public ClientChatMessage(string messageId, string senderUuid, string message, ChatMessageStatus status) : base(messageId, senderUuid, message)
        {
            this.Status = status;
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new <see cref="ClientChatMessage"/> with the given message ID, sender UUID, message, and timestamp with a pending status.
        /// </summary>
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        /// <param name="timestamp">Message timestamp</param>
        public ClientChatMessage(string messageId, string senderUuid, string message, DateTime timestamp) : base(messageId, senderUuid, message, timestamp)
        {
            this.Status = ChatMessageStatus.PENDING;
        }

        /// <inheritdoc />
        /// <summary>
        /// Creates a new <see cref="ClientChatMessage"/> with the given message ID, sender UUID, message, timestamp, and status.
        /// </summary>
        /// <param name="messageId">Unique message ID</param>
        /// <param name="senderUuid">Sender's UUID</param>
        /// <param name="message">Message</param>
        /// <param name="timestamp">Message timestamp</param>
        /// <param name="status">Status</param>
        public ClientChatMessage(string messageId, string senderUuid, string message, DateTime timestamp, ChatMessageStatus status) : base(messageId, senderUuid, message, timestamp)
        {
            this.Status = status;
        }
    }
}