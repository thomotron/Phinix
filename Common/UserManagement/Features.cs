namespace UserManagement
{
    /// <summary>
    /// Names of the features a server can switch off in server.conf, as sent to the mod at login
    /// (<see cref="LoginResponsePacket.DisabledFeatures"/>). The mod hides whatever is switched off.
    /// </summary>
    /// <remarks>
    /// Contracts, private messages and screenshots have their own switches from before, which their modules send.
    /// </remarks>
    public static class Features
    {
        /// <summary>Players' markets (the Market tab)</summary>
        public const string MARKET = "Market";

        /// <summary>The bounty board (the Bounties tab)</summary>
        public const string BOUNTIES = "Bounties";

        /// <summary>Sending gifts</summary>
        public const string GIFTS = "Gifts";

        /// <summary>Trading, gifting and selling colonists and animals</summary>
        public const string PAWN_TRADING = "PawnTrading";

        /// <summary>Bios, player cards and "last seen" times</summary>
        public const string PROFILES = "Profiles";

        /// <summary>The friends list: adding friends, and the Friends section of the player list</summary>
        public const string FRIENDS = "Friends";

        /// <summary>Emojis in chat and private messages, and the emoji button</summary>
        public const string EMOJIS = "Emojis";
    }
}
