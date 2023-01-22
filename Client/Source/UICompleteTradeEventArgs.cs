using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using Trading;
using UserManagement;

namespace PhinixClient
{
    public class UICompleteTradeEventArgs : CompleteTradeEventArgs
    {
        /// <summary>
        /// Trade details.
        /// </summary>
        public readonly ImmutableTrade Trade;
        
        /// <inheritdoc cref="CompleteTradeEventArgs.TradeId"/>
        public new string TradeId => Trade.TradeId;
        
        /// <inheritdoc cref="CompleteTradeEventArgs.OtherPartyUuid"/>
        public new string OtherPartyUuid => Trade.OtherPartyUuid;

        /// <inheritdoc cref="CompleteTradeEventArgs.Items"/>
        public new IEnumerable<ProtoThing> Items => Trade.OtherPartyItemsOnOffer;
        
        public UICompleteTradeEventArgs(ImmutableTrade trade, bool success) : base(trade.TradeId, success, trade.OtherPartyUuid, trade.OtherPartyItemsOnOffer)
        {
            this.Trade = trade;
        }

        /// <summary>
        /// Converts a base <see cref="CompleteTradeEventArgs"/> into a <see cref="UICompleteTradeEventArgs"/> using the
        /// given <see cref="ClientUserManager"/> for user lookup.
        /// </summary>
        /// <param name="args">Base event args</param>
        /// <param name="userManager"><see cref="ClientUserManager"/> instance for user lookup</param>
        /// <returns>Converted event args</returns>
        public static UICompleteTradeEventArgs FromCompleteTradeEventArgs(CompleteTradeEventArgs args, ClientUserManager userManager)
        {
            // Try to pull out the other party from userManager, creating a barebones one if that fails
            if (!userManager.TryGetUser(args.OtherPartyUuid, out ImmutableUser otherParty))
            {
                otherParty = new ImmutableUser(args.OtherPartyUuid);
            }
            
            ProtoThing[] ourItems = !args.Success ? args.Items.ToArray() : Array.Empty<ProtoThing>();
            ProtoThing[] theirItems = args.Success ? args.Items.ToArray() : Array.Empty<ProtoThing>();

            return new UICompleteTradeEventArgs(new ImmutableTrade(args.TradeId, otherParty, ourItems, theirItems, args.Success, args.Success), args.Success);
        }
    }
}