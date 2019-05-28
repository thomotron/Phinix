using System;

namespace Trading
{
    public class CreateTradeEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the trade creation was successful.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Trade ID
        /// </summary>
        public string TradeId;

        /// <summary>
        /// Other party's UUID.
        /// </summary>
        public string OtherPartyUuid;

        /// <summary>
        /// Reason the trade failed to create.
        /// </summary>
        public TradeFailureReason FailureReason;

        /// <summary>
        /// Message from the server.
        /// </summary>
        public string FailureMessage;

        /// <summary>
        /// Creates a successful <see cref="CreateTradeEventArgs"/> with the given trade ID and UUID.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherPartyUuid">Other party's UUID</param>
        public CreateTradeEventArgs(string tradeId, string otherPartyUuid)
        {
            this.Success = true;
            this.TradeId = tradeId;
            this.OtherPartyUuid = otherPartyUuid;
        }

        /// <summary>
        /// Creates a failed <see cref="CreateTradeEventArgs"/> with the given failure reason and message.
        /// </summary>
        /// <param name="failureReason">Reason the trade failed to create</param>
        /// <param name="failureMessage">Message from the server</param>
        public CreateTradeEventArgs(TradeFailureReason failureReason, string failureMessage)
        {
            this.Success = false;
            this.FailureReason = failureReason;
            this.FailureMessage = failureMessage;
        }
    }
}