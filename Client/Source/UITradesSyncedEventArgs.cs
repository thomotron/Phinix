using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Trading;
using UserManagement;

namespace PhinixClient
{
    public class UITradesSyncedEventArgs : TradesSyncedEventArgs
    {
        /// <summary>
        /// Collection of synchronised trades and their details.
        /// </summary>
        public readonly ImmutableTrade[] Trades;
        
        public UITradesSyncedEventArgs(IEnumerable<ImmutableTrade> trades) : base(trades.Select(t => t.TradeId))
        {
            this.Trades = trades.ToArray();
        }

        /// <summary>
        /// Converts a base <see cref="TradesSyncedEventArgs"/> into a <see cref="UITradesSyncedEventArgs"/> using the
        /// given <see cref="ClientTrading"/> for trade details lookup, and <see cref="ClientUserManager"/> for user
        /// lookup.
        /// </summary>
        /// <param name="args">Base event args</param>
        /// <param name="trading"><see cref="ClientTrading"/> instance for trade details lookup</param>
        /// <param name="userManager"><see cref="ClientUserManager"/> instance for user lookup</param>
        /// <returns>Converted event args</returns>
        public static UITradesSyncedEventArgs FromTradesSyncedEventArgs(TradesSyncedEventArgs args, ClientTrading trading, ClientUserManager userManager)
        {
            // Convert each of the received trades
            List<ImmutableTrade> convertedTrades = new List<ImmutableTrade>();
            foreach (string tradeId in args.TradeIds)
            {
                if (trading.TryGetTrade(tradeId, out ImmutableTrade trade))
                {
                    convertedTrades.Add(trade);
                }
            }

            return new UITradesSyncedEventArgs(convertedTrades);
        }
    }
}