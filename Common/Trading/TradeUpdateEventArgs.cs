using System;

namespace Trading
{
    public class TradeUpdateEventArgs : EventArgs
    {
        public string TradeId;

        public bool Success;

        public string Token;

        public TradeFailureReason FailureReason;

        public string FailureMessage;

        /// <summary>
        /// Creates a successful <see cref="TradeUpdateEventArgs"/> with the given trade ID and token.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="token">Item update token</param>
        public TradeUpdateEventArgs(string tradeId, string token = "")
        {
            this.Success = true;
            this.TradeId = tradeId;
            this.Token = token;
        }

        /// <summary>
        /// Creates a failed <see cref="TradeUpdateEventArgs"/> with the given trade ID, token, reason, and message.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="failureReason">Failure reason</param>
        /// <param name="failureMessage">Failure message</param>
        /// <param name="token">Item update token</param>
        public TradeUpdateEventArgs(string tradeId, TradeFailureReason failureReason, string failureMessage, string token = "")
        {
            this.Success = false;
            this.TradeId = tradeId;
            this.Token = token;
            this.FailureReason = failureReason;
            this.FailureMessage = failureMessage;
        }
    }
}