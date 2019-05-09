using System;

namespace Trading
{
    public class TradeUpdateEventArgs : EventArgs
    {
        public string TradeId;


        public string Token;

        /// <summary>
        /// Creates a new <c>TradeUpdateEventArgs</c> with the given trade ID and token.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="token">Item update token</param>
        public TradeUpdateEventArgs(string tradeId, string token = "")
        {
            this.TradeId = tradeId;
            this.Token = token;
        }
    }
}