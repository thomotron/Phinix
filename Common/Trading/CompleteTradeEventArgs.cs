using System;
using System.Collections.Generic;

namespace Trading
{
    public class CompleteTradeEventArgs : EventArgs
    {
        /// <summary>
        /// Trade ID.
        /// </summary>
        public string TradeId;

        /// <summary>
        /// Whether the trade finished successfully.
        /// </summary>
        public bool Success;

        /// <summary>
        /// UUID of the other party.
        /// </summary>
        public string OtherPartyUuid;

        /// <summary>
        /// Items received.
        /// </summary>
        public IEnumerable<ProtoThing> Items;

        /// <summary>
        /// Pawns received.
        /// </summary>
        public IEnumerable<ProtoPawn> Pawns;

        public CompleteTradeEventArgs(string tradeId, bool success, string otherPartyUuid, IEnumerable<ProtoThing> items, IEnumerable<ProtoPawn> pawns)
        {
            this.TradeId = tradeId;
            this.Success = success;
            this.OtherPartyUuid = otherPartyUuid;
            this.Items = items;
            this.Pawns = pawns;
        }
    }
}