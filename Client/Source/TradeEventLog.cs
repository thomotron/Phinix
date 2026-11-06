using System;
using System.Collections.Generic;

namespace PhinixClient
{
    /// <summary>
    /// Remembers what happened to our trades with each player (opened, accepted, completed, cancelled) so the Inbox can
    /// show it in that player's conversation, like "Rook accepted the trade".
    /// </summary>
    /// <remarks>
    /// Kept in memory only, for this play session. The server doesn't store these, so they're gone after a restart
    /// while the messages around them stay. Trade events arrive on the network thread, so everything is under a lock.
    /// </remarks>
    public static class TradeEventLog
    {
        public enum Kind
        {
            Opened,
            TheyAccepted,
            YouAccepted,
            Completed,
            Cancelled
        }

        public struct Entry
        {
            /// <summary>
            /// When it happened, in local time.
            /// </summary>
            public DateTime Time;
            public Kind Kind;
        }

        /// <summary>
        /// Most entries kept per player, so a long session of trading can't grow this forever.
        /// </summary>
        private const int MAX_PER_PLAYER = 50;

        private static readonly object entriesLock = new object();

        /// <summary>
        /// Entries by the other player's UUID, oldest first.
        /// </summary>
        private static readonly Dictionary<string, List<Entry>> entries = new Dictionary<string, List<Entry>>();

        /// <summary>
        /// Trades where we last saw the other player's (or our) acceptance, so we only log the moment it turns on.
        /// </summary>
        private static readonly HashSet<string> theyAccepted = new HashSet<string>();
        private static readonly HashSet<string> youAccepted = new HashSet<string>();

        /// <summary>
        /// Starts listening to the client's trade events. Called once, from the <see cref="Client"/> constructor.
        /// </summary>
        public static void Subscribe(Client client)
        {
            client.OnTradeCreationSuccess += (sender, args) => add(args.OtherPartyUuid, Kind.Opened);
            client.OnTradeCompleted += (sender, args) => finish(args.TradeId, args.OtherPartyUuid, Kind.Completed);
            client.OnTradeCancelled += (sender, args) => finish(args.TradeId, args.OtherPartyUuid, Kind.Cancelled);
            client.OnTradeUpdateSuccess += (sender, args) =>
            {
                string tradeId = args.Trade.TradeId;
                string otherUuid = args.Trade.OtherPartyUuid;
                lock (entriesLock)
                {
                    // Changing the items on offer takes both acceptances back, so an accept can happen more than once
                    if (track(theyAccepted, tradeId, args.Trade.OtherPartyAccepted)) add(otherUuid, Kind.TheyAccepted);
                    if (track(youAccepted, tradeId, args.Trade.Accepted)) add(otherUuid, Kind.YouAccepted);
                }
            };
        }

        /// <summary>
        /// Everything logged with a player, oldest first.
        /// </summary>
        public static Entry[] For(string otherUuid)
        {
            lock (entriesLock)
            {
                return otherUuid != null && entries.TryGetValue(otherUuid, out List<Entry> list) ? list.ToArray() : new Entry[0];
            }
        }

        /// <summary>
        /// Records whether a trade is accepted now. Returns true when it just turned on.
        /// </summary>
        private static bool track(HashSet<string> accepted, string tradeId, bool nowAccepted)
        {
            if (nowAccepted) return accepted.Add(tradeId);

            accepted.Remove(tradeId);
            return false;
        }

        private static void finish(string tradeId, string otherUuid, Kind kind)
        {
            lock (entriesLock)
            {
                theyAccepted.Remove(tradeId);
                youAccepted.Remove(tradeId);
                add(otherUuid, kind);
            }
        }

        private static void add(string otherUuid, Kind kind)
        {
            if (string.IsNullOrEmpty(otherUuid)) return;

            lock (entriesLock)
            {
                if (!entries.TryGetValue(otherUuid, out List<Entry> list))
                {
                    list = new List<Entry>();
                    entries[otherUuid] = list;
                }

                list.Add(new Entry { Time = DateTime.Now, Kind = kind });
                if (list.Count > MAX_PER_PLAYER) list.RemoveRange(0, list.Count - MAX_PER_PLAYER);
            }
        }
    }
}
