using System;
using System.Collections.Generic;
using System.Linq;

namespace Trading
{
    public class TradesSyncedEventArgs : EventArgs
    {
        /// <summary>
        /// Collection of trade IDs that were synced from the server.
        /// </summary>
        public readonly string[] TradeIds;

        /// <summary>
        /// Creates a new <see cref="TradesSyncedEventArgs"/> for the given trade IDs.
        /// </summary>
        /// <param name="tradeIds">Trade IDs synced from the server</param>
        public TradesSyncedEventArgs(IEnumerable<string> tradeIds)
        {
            this.TradeIds = tradeIds.ToArray();
        }
    }
}