namespace PhinixClient
{
    /// <summary>
    /// Event args for when a user is blocked or unblocked.
    /// </summary>
    public class BlockedUsersChangedEventArgs
    {
        /// <summary>
        /// UUID of the user.
        /// </summary>
        public readonly string Uuid;

        /// <summary>
        /// The user's new blocked state.
        /// </summary>
        public readonly bool IsBlocked;

        /// <summary>
        /// Creates a new <see cref="BlockedUsersChangedEventArgs"/> with the given user's UUID and blocked state.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="isBlocked">User's new blocked state</param>
        public BlockedUsersChangedEventArgs(string uuid, bool isBlocked)
        {
            this.Uuid = uuid;
            this.IsBlocked = isBlocked;
        }
    }
}