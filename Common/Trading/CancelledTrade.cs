using System.Collections.Generic;
using System.Linq;

namespace Trading
{
    /// <summary>
    /// A wrapper around a <c>Trade</c> that keeps track of who else needs to be notified that the trade was cancelled.
    /// </summary>
    public class CancelledTrade
    {
        /// <summary>
        /// The trade that was cancelled.
        /// </summary>
        public Trade Trade;
        
        /// <summary>
        /// List of party UUIDs that are awaiting notification of the cancelled trade.
        /// </summary>
        public List<string> PendingNotification;

        public CancelledTrade(Trade trade, IEnumerable<string> pendingNotification)
        {
            this.Trade = trade;
            this.PendingNotification = pendingNotification.ToList();
        }
    }
}