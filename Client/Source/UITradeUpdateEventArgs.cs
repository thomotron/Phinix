using Trading;
using UserManagement;

namespace PhinixClient
{
    public class UITradeUpdateEventArgs : TradeUpdateEventArgs
    {
        /// <summary>
        /// Trade details.
        /// </summary>
        public readonly ImmutableTrade Trade;

        /// <inheritdoc cref="TradeUpdateEventArgs.TradeId"/>
        public new string TradeId => Trade.TradeId;
        
        public UITradeUpdateEventArgs(ImmutableTrade trade, string token = "") : base(trade.TradeId, token)
        {
            this.Trade = trade;
        }

        public UITradeUpdateEventArgs(ImmutableTrade trade, TradeFailureReason failureReason, string failureMessage, string token = "") : base(trade.TradeId, failureReason, failureMessage, token)
        {
            this.Trade = trade;
        }

        /// <summary>
        /// Converts a base <see cref="TradeUpdateEventArgs"/> into a <see cref="UITradeUpdateEventArgs"/> using the
        /// given <see cref="ClientTrading"/> for trade details lookup.
        /// </summary>
        /// <param name="args">Base event args</param>
        /// <param name="trading"><see cref="ClientTrading"/> instance for trade details lookup</param>
        /// <returns>Converted event args</returns>
        public static UITradeUpdateEventArgs FromTradeUpdateEventArgs(TradeUpdateEventArgs args, ClientTrading trading)
        {
            // Try pull out the trade, creating a barebones one if that fails
            if (!trading.TryGetTrade(args.TradeId, out ImmutableTrade trade))
            {
                trade = new ImmutableTrade(args.TradeId, new ImmutableUser());
            }

            if (args.Success)
            {
                return new UITradeUpdateEventArgs(trade, args.Token);
            }
            else
            {
                return new UITradeUpdateEventArgs(trade, args.FailureReason, args.FailureMessage, args.Token);
            }
        }
    }
}