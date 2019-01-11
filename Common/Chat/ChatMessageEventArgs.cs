using System;

namespace Chat
{
    public class ChatMessageEventArgs : EventArgs
    {
        public string Message;

        public string OriginUuid;

        public DateTime Timestamp;

        public ChatMessageEventArgs(string message, string originUuid, DateTime timestamp)
        {
            this.Message = message;
            this.OriginUuid = originUuid;
            this.Timestamp = timestamp;
        }
    }
}