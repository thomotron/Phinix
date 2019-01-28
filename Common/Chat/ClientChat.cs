using System;
using System.Collections.Generic;
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
        public event EventHandler<ChatMessageEventArgs> OnChatMessageReceived;

        /// <summary>
        /// The number of messages received since <c>GetMessages()</c> was last called.
        /// </summary>
        public int UnreadMessages
        {
            get
            {
                lock (messageHistoryLock)
                {
                    return messageHistory.Count - messageCountAtLastCheck;
                }
            }
        }
        /// <summary>
        /// The number of messages in history when <c>GetMessages()</c> was last called.
        /// </summary>
        private int messageCountAtLastCheck;

        /// <summary>
        /// <c>NetClient</c> instance to bind the packet handler to.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <c>ClientAuthenticator</c> to get the session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <c>ClientUserManager</c> used for user lookup and display name rendering.
        /// </summary>
        private ClientUserManager userManager;

        /// <summary>
        /// List of chat messages received from the server.
        /// </summary>
        private List<ChatMessage> messageHistory;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>messageHistory</c>.
        /// </summary>
        private object messageHistoryLock = new object();
        
        public ClientChat(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;
            
            this.messageHistory = new List<ChatMessage>();
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

            // Create and pack the chat message packet
            ChatMessagePacket packet = new ChatMessagePacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Message = message,
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
                
            // Send it on its way
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Returns a list of all messages received since connecting to the server.
        /// </summary>
        /// <returns>A list of all messages received since connecting to the server</returns>
        public ChatMessage[] GetMessages()
        {
            lock (messageHistoryLock)
            {
                // Set the read message count
                messageCountAtLastCheck = messageHistory.Count;
                
                // Return the messages in history
                return messageHistory.ToArray();
            }
        }

        /// <summary>
        /// Handles incoming <c>ChatMessagePacket</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming packet</param>
        private void chatMessagePacketHandler(string connectionId, ChatMessagePacket packet)
        {
            lock (messageHistoryLock)
            {
                // Store the message in chat history
                messageHistory.Add(new ChatMessage(packet.Uuid, packet.Message, packet.Timestamp.ToDateTime()));
            }
            
            OnChatMessageReceived?.Invoke(this, new ChatMessageEventArgs(packet.Message, packet.Uuid, packet.Timestamp.ToDateTime()));
        }
    }
}