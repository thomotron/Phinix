using System;

namespace Trading
{
    public class TradeUpdateEventArgs : EventArgs
    {
        public string TradeId;

        public TradeUpdateEventArgs(string tradeId)
        {
            this.TradeId = tradeId;
        }
    }
}