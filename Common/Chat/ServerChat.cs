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
    public class ServerChat : Chat
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);
        
        /// <summary>
        /// <c>NetServer</c> instance to bind the packet handler to.
        /// </summary>
        private NetServer netServer;

        /// <summary>
        /// <c>ServerAuthenticator</c> instance used to check session validity.
        /// </summary>
        private ServerAuthenticator authenticator;

        /// <summary>
        /// <c>ServerUserManager</c> instance used to check login state and source UUID validity.
        /// </summary>
        private ServerUserManager userManager;

        /// <summary>
        /// List of chat messages sent to users on connect.
        /// </summary>
        private List<ChatMessage> messageHistory;
        /// <summary>
        /// Lock file to prevent race conditions when accessing <c>messageHistory</c>.
        /// </summary>
        private object messageHistoryLock = new object();
        /// <summary>
        /// Maximum number of chat messages to buffer in history.
        /// </summary>
        private int messageHistoryCapacity;
        
        public ServerChat(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, int messageHistoryCapacity)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            this.messageHistoryCapacity = messageHistoryCapacity;
            
            this.messageHistory = new List<ChatMessage>();
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            userManager.OnLogin += loginHandler;
        }

        private void loginHandler(object sender, ServerLoginEventArgs args)
        {
            lock (messageHistoryLock)
            {
                // Send each message in the chat history to the newly logged-in user
                foreach (ChatMessage chatMessage in messageHistory)
                {
                    sendChatMessage(args.ConnectionId, chatMessage);
                }
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
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerChat).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with the packet
            switch (typeUrl.Type)
            {
                case "ChatMessagePacket":
                    RaiseLogEntry(new LogEventArgs(string.Format("Got a ChatMessagePacket from {0}", connectionId), LogLevel.DEBUG));
                    chatMessagePacketHandler(connectionId, message.Unpack<ChatMessagePacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        private void chatMessagePacketHandler(string connectionId, ChatMessagePacket packet)
        {
            // Ignore packets from non-authenticated sessions
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId)) return;

            // Ignore packets from non-logged in users
            if (!userManager.IsLoggedIn(connectionId, packet.Uuid)) return;
            
            // Clear the session ID for security
            packet.SessionId = "";
            
            // Sanitise the message content
            packet.Message = TextHelper.SanitiseRichText(packet.Message);
            
            // Add the message to the message history
            addMessageToHistory(new ChatMessage(packet.Uuid, packet.Message));
            
            // Set the timestamp
            packet.Timestamp = DateTime.UtcNow.ToTimestamp();
                    
            // Broadcast the chat packet to everyone
            broadcastChatMessage(packet);
        }

        /// <summary>
        /// Broadcasts the given <c>ChatMessagePacket</c> to all currently logged-in users.
        /// </summary>
        /// <param name="packet"><c>ChatMessagePacket</c> to broadcast</param>
        private void broadcastChatMessage(ChatMessagePacket packet)
        {
            // Pack the packet
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it to each logged in user
            foreach (string connectionId in userManager.GetConnections())
            {
                try
                {
                    // Try send the chat message
                    netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
                }
                catch (NotConnectedException)
                {
                    // Report the failure
                    RaiseLogEntry(new LogEventArgs(string.Format("Tried sending a chat message to connection {0}, but it is closed", connectionId), LogLevel.DEBUG));
                }
            }
        }

        /// <summary>
        /// Sends the given <c>ChatMessage</c> to the user.
        /// </summary>
        /// <param name="connectionId">Destination connection ID</param>
        /// <param name="chatMessage"><c>ChatMessage</c> to send</param>
        private void sendChatMessage(string connectionId, ChatMessage chatMessage)
        {
            // Create and pack our chat message packet
            ChatMessagePacket packet = new ChatMessagePacket
            {
                Uuid = chatMessage.SenderUuid,
                Message = chatMessage.Message,
                Timestamp = chatMessage.ReceivedTime.ToTimestamp()
            };
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            
            // Send it on its way
            netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Adds the given <c>ChatMessage</c> to the message history.
        /// </summary>
        /// <param name="chatMessage"><c>ChatMessage</c> to store</param>
        private void addMessageToHistory(ChatMessage chatMessage)
        {
            lock (messageHistoryLock)
            {
                // Add the message to history
                messageHistory.Add(chatMessage);

                // Check if we've exceeded the history capacity
                if (messageHistory.Count > messageHistoryCapacity)
                {
                    // Remove the oldest message
                    messageHistory.RemoveAt(0);
                }
            }
        }
    }
}
