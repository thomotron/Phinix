using System;

namespace Chat
{
    public class ChatMessageEvent : EventArgs
    {
        public string Message;

        public string OriginUuid;

        public ChatMessageEvent(string message, string originUuid)
        {
            this.Message = message;
            this.OriginUuid = originUuid;
        }
    }
}