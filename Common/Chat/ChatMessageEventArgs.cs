using System;

namespace Chat
{
    public class ChatMessageEventArgs : EventArgs
    {
        public ChatMessage Message;

        public ChatMessageEventArgs(ChatMessage message)
        {
            this.Message = message;
        }
    }
}