using System;
using System.Collections;
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

        /// <summary>
        /// Creates a new <c>Trade</c> between the given parties.
        /// </summary>
        /// <param name="partyUuids">UUIDs of each party</param>
        public Trade(IEnumerable<string> partyUuids)
        {
            this.PartyUuids = partyUuids.ToArray();
            
            this.TradeId = Guid.NewGuid().ToString();
            this.ItemsOnOffer = new Dictionary<string, Thing[]>();
            this.AcceptedParties = new List<string>();
        }

        /// <summary>
        /// Creates a new <c>Trade</c> between the given parties with the given trade ID.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="partyUuids">UUIDs of each party</param>
        public Trade(string tradeId, IEnumerable<string> partyUuids)
        {
            this.TradeId = tradeId;
            this.PartyUuids = partyUuids.ToArray();
            
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

        /// <summary>
        /// Attempts to get the other party's UUID from this trade.
        /// Returns whether the other party's UUID was retrieved successfully.
        /// </summary>
        /// <param name="partyUuid">This party's UUID</param>
        /// <param name="otherPartyUuid">Other party's UUID output</param>
        /// <returns>Whether the other party's UUID was retrieved successfully</returns>
        /// <exception cref="ArgumentException">Party UUID is not present in this trade</exception>
        public bool TryGetOtherParty(string partyUuid, out string otherPartyUuid)
        {
            // Check if the party UUID is present in this trade
            if (!PartyUuids.Contains(partyUuid)) throw new ArgumentException("Party UUID is not present in this trade.", nameof(partyUuid));
            
            // Set other party's UUID to something arbitrary
            otherPartyUuid = null;

            // Try to get the other party's UUID
            try
            {
                otherPartyUuid = PartyUuids.Single(uuid => uuid != partyUuid);
            }
            catch (InvalidOperationException)
            {
                // Failed to get a single UUID that isn't this party's
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to get whether this party has accepted this trade.
        /// Returns whether the accepted state was retrieved successfully.
        /// </summary>
        /// <param name="partyUuid">Party's UUID</param>
        /// <param name="accepted">Whether the party has accepted</param>
        /// <returns>Whether the accepted state was retrieved successfully</returns>
        /// <exception cref="ArgumentException">Party UUID is not present in this trade</exception>
        public bool TryGetAccepted(string partyUuid, out bool accepted)
        {
            // Check if the party UUID is present in this trade
            if (!PartyUuids.Contains(partyUuid)) throw new ArgumentException("Party UUID is not present in this trade.", nameof(partyUuid));

            // Get whether the party has accepted
            accepted = AcceptedParties.Contains(partyUuid);

            return true;
        }
    }
}