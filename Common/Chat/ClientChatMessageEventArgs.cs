using System;

namespace Chat
{
    public class ClientChatMessageEventArgs : EventArgs
    {
        public ClientChatMessage Message;

        public ClientChatMessageEventArgs(ClientChatMessage message)
        {
            this.Message = message;
        }
    }
}