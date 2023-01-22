using System;
using Trading;
using UserManagement;

namespace PhinixClient
{
    public class UICreateTradeEventArgs : CreateTradeEventArgs
    {
        /// <summary>
        /// Trade details.
        /// </summary>
        public readonly ImmutableTrade Trade;

        /// <inheritdoc cref="CreateTradeEventArgs.TradeId"/>
        public new string TradeId => Trade.TradeId;

        /// <inheritdoc cref="CreateTradeEventArgs.OtherPartyUuid"/>
        public new string OtherPartyUuid => Trade.OtherPartyUuid;
        
        public UICreateTradeEventArgs(ImmutableTrade trade) : base(trade.TradeId, trade.OtherPartyUuid)
        {
            this.Trade = trade;
        }

        public UICreateTradeEventArgs(TradeFailureReason failureReason, string failureMessage) : base(failureReason, failureMessage) {}
        
        /// <summary>
        /// Converts a base <see cref="CreateTradeEventArgs"/> into a <see cref="UICreateTradeEventArgs"/> using the
        /// given <see cref="ClientUserManager"/> for user lookup.
        /// </summary>
        /// <param name="args">Base event args</param>
        /// <param name="userManager"><see cref="ClientUserManager"/> instance for user lookup</param>
        /// <returns>Converted event args</returns>
        public static UICreateTradeEventArgs FromCreateTradeEventArgs(CreateTradeEventArgs args, ClientUserManager userManager)
        {
            if (args.Success)
            {
                // Try to pull out the other party from userManager, creating a barebones one if that fails
                if (!userManager.TryGetUser(args.OtherPartyUuid, out ImmutableUser otherParty))
                {
                    otherParty = new ImmutableUser(args.OtherPartyUuid);
                }
                
                return new UICreateTradeEventArgs(new ImmutableTrade(args.TradeId, otherParty, Array.Empty<ProtoThing>(), Array.Empty<ProtoThing>(), false, false));
            }
            else
            {
                return new UICreateTradeEventArgs(args.FailureReason, args.FailureMessage);
            }
        }
    }
}