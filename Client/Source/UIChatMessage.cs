using System.Collections.Generic;
using Chat;
using UserManagement;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Pairs a chat message to the user that sent it.
    /// </summary>
    public class UIChatMessage : ClientChatMessage
    {
        /// <summary>
        /// User that sent <see cref="ChatMessage"/>
        /// </summary>
        public ImmutableUser User;

        public UIChatMessage(ClientChatMessage chatMessage, ImmutableUser user) : base(chatMessage.MessageId, chatMessage.SenderUuid, chatMessage.Message, chatMessage.Timestamp, chatMessage.Status)
        {
            this.User = user;
        }

        /// <summary>
        /// Creates a new <see cref="UIChatMessage"/> from the given <see cref="ClientChatMessage"/> using <paramref name="userManager"/> to look up the sender's user details.
        /// If the user cannot be found, a default one is used.
        /// </summary>
        /// <param name="userManager">User manager instance to use for user lookup</param>
        /// <param name="chatMessage">Base chat message</param>
        /// <returns>New <see cref="UIChatMessage"/> instance</returns>
        public UIChatMessage(UserManager userManager, ClientChatMessage chatMessage) : base(chatMessage.MessageId, chatMessage.SenderUuid, chatMessage.Message, chatMessage.Timestamp, chatMessage.Status)
        {
            // Try get a copy of the user's details, otherwise use the defaults
            if (!userManager.TryGetUser(chatMessage.SenderUuid, out ImmutableUser user))
            {
                user = new ImmutableUser(chatMessage.SenderUuid);
            }

            this.User = user;
        }
    }
}