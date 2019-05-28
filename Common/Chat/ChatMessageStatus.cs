namespace Chat
{
    /// <summary>
    /// Reflects the status of a <see cref="ChatMessage"/>.
    /// (i.e. whether it has been accepted or denied by the server.)
    /// </summary>
    public enum ChatMessageStatus
    {
        PENDING,
        CONFIRMED,
        DENIED
    }
}