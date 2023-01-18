using Chat;

namespace PhinixClient
{
    public class UIChatMessageEventArgs : ClientChatMessageEventArgs
    {
        public new UIChatMessage Message;
        
        public UIChatMessageEventArgs(UIChatMessage message) : base(message)
        {
            this.Message = message;
        }
    }
}