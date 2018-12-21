using System;
using System.Collections.Generic;
using System.Linq;

namespace Trading
{
    public class Trade
    {
        /// <summary>
        /// This trade's unique ID.
        /// </summary>
        public readonly string TradeId;

        /// <summary>
        /// Collection of each participating party's UUID.
        /// </summary>
        public readonly string[] PartyUuids;

        /// <summary>
        /// Items currently on offer organised by the owner's UUID.
        /// </summary>
        public readonly Dictionary<string, Thing[]> ItemsOnOffer;

        /// <summary>
        /// List containing UUIDs of each party that has accepted the trade.
        /// </summary>
        public readonly List<string> AcceptedParties;

        public Trade(IEnumerable<string> partyUuids)
        {
            this.PartyUuids = partyUuids.ToArray();
            
            this.TradeId = Guid.NewGuid().ToString();
            this.ItemsOnOffer = new Dictionary<string, Thing[]>();
            this.AcceptedParties = new List<string>();
        }

        /// <summary>
        /// Sets the items on offer for the given party and resets the accepted status of all parties.
        /// </summary>
        /// <param name="partyUuid">Party's UUID</param>
        /// <param name="items">Items to set as on offer</param>
        /// <exception cref="ArgumentException">Party UUID is not present in this trade</exception>
        public void SetItemsOnOffer(string partyUuid, IEnumerable<Thing> items)
        {
            // Check if the party UUID is present in this trade
            if (!PartyUuids.Contains(partyUuid)) throw new ArgumentException("Party UUID is not present in this trade.", nameof(partyUuid));

            // Set the party's items on offer
            if (!ItemsOnOffer.ContainsKey(partyUuid))
            {
                ItemsOnOffer.Add(partyUuid, items.ToArray());
            }
            else
            {
                ItemsOnOffer[partyUuid] = items.ToArray();
            }
            
            // Reset all parties' accepted states
            AcceptedParties.Clear();
        }
    }
}