using System;
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
        
        public ServerChat(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
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
            if (!ProtobufPacketHelper.ValidatePacket("Chat", MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

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
                netServer.Send(connectionId, MODULE_NAME, packedPacket.ToByteArray());
            }
        }
    }
}