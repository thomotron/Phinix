namespace Chat
{
    /// <summary>
    /// Reflects the status of a <c>ChatMessage</c>.
    /// (i.e. whether it has been accepted or denied by the server.)
    /// </summary>
    public enum ChatMessageStatus
    {
        PENDING,
        CONFIRMED,
        DENIED
    }
}