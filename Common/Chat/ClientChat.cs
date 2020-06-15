using System;
using System.Collections.Generic;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Chat
{
    public class ClientChat : Chat
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Raised when a chat message is received.
        /// </summary>
        public event EventHandler<ClientChatMessageEventArgs> OnChatMessageReceived;

        /// <summary>
        /// The number of messages received since <c>GetMessages()</c> was last called.
        /// </summary>
        public int UnreadMessages
        {
            get
            {
                lock (messageHistoryLock) { return messageHistory.Count - messageCountAtLastCheck; }
            }
        }
        /// <summary>
        /// The number of messages in history when <c>GetMessages()</c> was last called.
        /// </summary>
        private int messageCountAtLastCheck;

        /// <summary>
        /// <see cref="NetClient"/> instance to bind the packet handler to.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <see cref="ClientAuthenticator"/> to get the session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <see cref="ClientUserManager"/> used for user lookup and display name rendering.
        /// </summary>
        private ClientUserManager userManager;

        /// <summary>
        /// List of chat messages received from the server.
        /// </summary>
        private List<ClientChatMessage> messageHistory;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <see cref="messageHistory"/>.
        /// </summary>
        private object messageHistoryLock = new object();

        public ClientChat(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;

            this.messageHistory = new List<ClientChatMessage>();
            this.messageCountAtLastCheck = 0;

            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netClient.OnDisconnect += disconnectHandler;
        }

        private void disconnectHandler(object sender, EventArgs e)
        {
            lock (messageHistoryLock)
            {
                // Clear message history
                messageHistory.Clear();

                // Reset the last message count
                messageCountAtLastCheck = 0;
            }
        }

        /// <summary>
        /// Handles incoming packets.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Discard packet if it fails validation
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientChat).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with the packet
            switch (typeUrl.Type)
            {
                case "ChatMessagePacket":
                    RaiseLogEntry(new LogEventArgs("Got a ChatMessagePacket", LogLevel.DEBUG));
                    chatMessagePacketHandler(connectionId, message.Unpack<ChatMessagePacket>());
                    break;
                case "ChatMessageResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a ChatMessageResponsePacket", LogLevel.DEBUG));
                    chatMessageResponsePacketHandler(connectionId, message.Unpack<ChatMessageResponsePacket>());
					break;
                case "ChatHistoryPacket":
                    RaiseLogEntry(new LogEventArgs("Got a ChatHistoryPacket", LogLevel.DEBUG));
                    chatHistoryPacketHandler(connectionId, message.Unpack<ChatHistoryPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Sends a message to the chat.
        /// </summary>
        /// <param name="message">Message</param>
        /// <exception cref="ArgumentException">Message cannot be null or empty</exception>
        public void Send(string message)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException("Message cannot be null or empty", nameof(message));

            // Check if we aren't authenticated
            if (!authenticator.Authenticated)
            {
                RaiseLogEntry(new LogEventArgs("Cannot send chat message: Not authenticated"));

                return;
            }

            // Check if we aren't logged in
            if (!userManager.LoggedIn)
            {
                RaiseLogEntry(new LogEventArgs("Cannot send chat message: Not logged in"));

                return;
            }

            // Create a random message ID
            string messageId = Guid.NewGuid().ToString();

            // Create and store a chat message locally
            ClientChatMessage localMessage = new ClientChatMessage(messageId, userManager.Uuid, message);
            lock (messageHistoryLock)
            {
                messageHistory.Add(localMessage);
            }

            // Create and pack the chat message packet
            ChatMessagePacket packet = new ChatMessagePacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                MessageId = messageId,
                Message = message
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);

            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Returns a list of all messages received since connecting to the server.
        /// </summary>
        /// <param name="markAsRead">Whether to mark all messages as read</param>
        /// <returns>A list of all messages received since connecting to the server</returns>
        public ClientChatMessage[] GetMessages(bool markAsRead = true)
        {
            lock (messageHistoryLock)
            {
                // Mark all messages as read, if enabled
                if (markAsRead) MarkAsRead();

                // Return the messages in history
                return messageHistory.ToArray();
            }
        }

        /// <summary>
        /// Returns a list of the current unread messages.
        /// </summary>
        /// <param name="markAsRead">Whether to mark all messages as read</param>
        /// <returns>A list of all current unread messages</returns>
        public ClientChatMessage[] GetUnreadMessages(bool markAsRead = true)
        {
            lock (messageHistoryLock)
            {
                // Mark all messages as read, if enabled
                if (markAsRead) MarkAsRead();

                // Return a subset containing just the unread messages
                return messageHistory.GetRange(messageHistory.Count - UnreadMessages, UnreadMessages).ToArray();
            }
        }

        /// <summary>
        /// Attempts to retrieve the corresponding <see cref="ClientChatMessage"/> for the given message ID.
        /// Returns true if the attempt was successful, otherwise false.
        /// </summary>
        /// <param name="messageId">Message ID</param>
        /// <param name="message">Message output</param>
        /// <returns>Whether the message with the given ID was retrieved successfully</returns>
        public bool TryGetMessage(string messageId, out ClientChatMessage message)
        {
            message = null;

            lock (messageHistoryLock)
            {
                try
                {
                    // Get the message corresponding with the given ID from history
                    message = messageHistory.Single(m => m.MessageId == messageId);
                }
                catch (InvalidOperationException)
                {
                    // A single message with the given ID doesn't exist, return a failure
                    return false;
                }
            }

            // Got what we came for, return a success
            return true;
        }

        /// <summary>
        /// Marks all chat messages as read.
        /// </summary>
        public void MarkAsRead()
        {
            lock (messageHistoryLock)
            {
                // Set the read message count
                messageCountAtLastCheck = messageHistory.Count;
            }
        }

        /// <summary>
        /// Handles incoming <see cref="ChatMessagePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void chatMessagePacketHandler(string connectionId, ChatMessagePacket packet)
        {
            ClientChatMessage message = new ClientChatMessage(packet.MessageId, packet.Uuid, packet.Message, packet.Timestamp.ToDateTime(), ChatMessageStatus.CONFIRMED);

            lock (messageHistoryLock)
            {
                // Store the message in chat history
                messageHistory.Add(message);
            }

            OnChatMessageReceived?.Invoke(this, new ClientChatMessageEventArgs(message));
        }

        /// <summary>
        /// Handles incoming <see cref="ChatHistoryPacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void chatHistoryPacketHandler(string connectionId, ChatHistoryPacket packet)
        {
            lock (messageHistoryLock)
            {
                // Store each message in chat history
                foreach (ChatMessagePacket messagePacket in packet.ChatMessages)
                {
                    messageHistory.Add(new ClientChatMessage(messagePacket.MessageId, messagePacket.Uuid, messagePacket.Message, messagePacket.Timestamp.ToDateTime(), ChatMessageStatus.CONFIRMED));
                }
            }
        }

		/// <summary>
        /// Handles incoming <see cref="ChatMessageResponsePacket"/>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void chatMessageResponsePacketHandler(string connectionId, ChatMessageResponsePacket packet)
        {
            ClientChatMessage message;
            lock (messageHistoryLock)
            {
                try
                {
                    // Try get a message with a corresponding original message ID
                    message = messageHistory.Single(m => m.MessageId == packet.OriginalMessageId);
                }
                catch (InvalidOperationException)
                {
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a ChatMessageResponsePacket with an unknown original message ID ({0})", packet.OriginalMessageId), LogLevel.WARNING));

                    // Stop here
                    return;
                }

                // Update the message ID
                message.MessageId = packet.NewMessageId;

                if (packet.Success)
                {
                    // Update the message content and confirm it
                    message.Message = packet.Message;
                    message.Status = ChatMessageStatus.CONFIRMED;
                }
                else
                {
                    // Deny the message but don't overwrite the content
                    message.Status = ChatMessageStatus.DENIED;
                }
            }

            OnChatMessageReceived?.Invoke(this, new ClientChatMessageEventArgs(message));
        }
    }
}
