using System.Collections;

namespace UserManagement
{
    /// <summary>
    /// An immutable representation of a <see cref="UserManagement.User"/>.
    /// </summary>
    public struct ImmutableUser
    {
        /// <inheritdoc cref="User.Uuid"/>
        public string Uuid { get; }
        /// <inheritdoc cref="User.DisplayName"/>
        public string DisplayName { get; }
        /// <inheritdoc cref="User.LoggedIn"/>
        public bool LoggedIn { get; }
        /// <inheritdoc cref="User.AcceptingTrades"/>
        public bool AcceptingTrades { get; }

        public ImmutableUser(string uuid, string displayName = "???", bool loggedIn = false, bool acceptingTrades = false)
        {
            this.Uuid = uuid;
            this.DisplayName = displayName;
            this.LoggedIn = loggedIn;
            this.AcceptingTrades = acceptingTrades;
        }

        public override bool Equals(object obj)
        {
            return obj is ImmutableUser other && this.Equals(other);
        }

        public bool Equals(ImmutableUser other)
        {
            return Uuid == other.Uuid &&
                   DisplayName == other.DisplayName &&
                   LoggedIn == other.LoggedIn &&
                   AcceptingTrades == other.AcceptingTrades;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Uuid.GetHashCode();
                hashCode = (hashCode * 397) ^ DisplayName.GetHashCode();
                hashCode = (hashCode * 397) ^ LoggedIn.GetHashCode();
                hashCode = (hashCode * 397) ^ AcceptingTrades.GetHashCode();
                return hashCode;
            }
        }
    }
}