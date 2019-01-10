using System.Collections.Generic;
using System.Linq;

namespace Trading
{
    /// <summary>
    /// A wrapper around a <c>Trade</c> that keeps track of who else needs to be notified that the trade was completed.
    /// </summary>
    public class CompletedTrade
    {
        /// <summary>
        /// The completed trade.
        /// </summary>
        public Trade Trade;
        
        /// <summary>
        /// List of party UUIDs that are awaiting notification of the completed trade.
        /// </summary>
        public List<string> PendingNotification;

        /// <summary>
        /// Whether the trade was cancelled.
        /// </summary>
        public bool Cancelled;

        public CompletedTrade(Trade trade, IEnumerable<string> pendingNotification, bool cancelled)
        {
            this.Trade = trade;
            this.PendingNotification = pendingNotification.ToList();
            this.Cancelled = cancelled;
        }
    }
}