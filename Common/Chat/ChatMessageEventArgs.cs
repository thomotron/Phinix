using System;

namespace Chat
{
    public class ChatMessageEventArgs : EventArgs
    {
        public string Message;

        public string OriginUuid;

        public ChatMessageEventArgs(string message, string originUuid)
        {
            this.Message = message;
            this.OriginUuid = originUuid;
        }
    }
}