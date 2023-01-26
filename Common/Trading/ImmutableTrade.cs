using System;
using System.Collections.Generic;
using System.Linq;
using UserManagement;

namespace Trading
{
    /// <summary>
    /// An immutable representation of a <see cref="Trade"/>.
    /// </summary>
    public struct ImmutableTrade
    {
        /// <summary>
        /// This trade's unique ID.
        /// </summary>
        public string TradeId { get; }

        /// <summary>
        /// The other party's user details.
        /// </summary>
        public ImmutableUser OtherParty { get; }

        /// <summary>
        /// The other party's UUID.
        /// </summary>
        public string OtherPartyUuid => OtherParty.Uuid;

        /// <summary>
        /// The other party's display name.
        /// </summary>
        public string OtherPartyDisplayName => OtherParty.DisplayName;

        /// <summary>
        /// Our items currently on offer.
        /// </summary>
        public ProtoThing[] ItemsOnOffer { get; }

        /// <summary>
        /// Our pawns on offer.
        /// </summary>
        public ProtoPawn[] PawnsOnOffer { get; }

        /// <summary>
        /// The other party's items currently on offer.
        /// </summary>
        public ProtoThing[] OtherPartyItemsOnOffer { get; }

        /// <summary>
        /// The other party's pawns currently on offer.
        /// </summary>
        public ProtoPawn[] OtherPartyPawnsOnOffer { get; }

        /// <summary>
        /// Whether we have accepted the trade.
        /// </summary>
        public bool Accepted { get; }

        /// <summary>
        /// Whether the other party has accepted the trade.
        /// </summary>
        public bool OtherPartyAccepted { get; }

        /// <summary>
        /// Creates a blank <see cref="ImmutableTrade"/> with the given trade ID and other party's details.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="otherParty">Other party's user details</param>
        public ImmutableTrade(string tradeId, ImmutableUser otherParty) : this(tradeId, otherParty, Array.Empty<ProtoThing>(), Array.Empty<ProtoPawn>(), Array.Empty<ProtoThing>(), Array.Empty<ProtoPawn>(), false, false) {}

        public ImmutableTrade(string tradeId, ImmutableUser otherParty, IEnumerable<ProtoThing> ourItemsOnOffer, IEnumerable<ProtoPawn> ourPawnsOnOffer, IEnumerable<ProtoThing> otherPartyItemsOnOffer, IEnumerable<ProtoPawn> otherPartyPawnsOnOffer, bool accepted, bool otherPartyAccepted)
        {
            TradeId = tradeId;
            OtherParty = otherParty;
            ItemsOnOffer = ourItemsOnOffer.ToArray();
            PawnsOnOffer = ourPawnsOnOffer.ToArray();
            OtherPartyItemsOnOffer = otherPartyItemsOnOffer.ToArray();
            OtherPartyPawnsOnOffer = otherPartyPawnsOnOffer.ToArray();
            Accepted = accepted;
            OtherPartyAccepted = otherPartyAccepted;
        }

        public bool Equals(ImmutableTrade other)
        {
            return TradeId == other.TradeId &&
                   OtherParty.Equals(other.OtherParty) &&
                   Equals(ItemsOnOffer, other.ItemsOnOffer) &&
                   Equals(PawnsOnOffer, other.PawnsOnOffer) &&
                   Equals(OtherPartyItemsOnOffer, other.OtherPartyItemsOnOffer) &&
                   Equals(OtherPartyPawnsOnOffer, other.OtherPartyPawnsOnOffer) &&
                   Accepted == other.Accepted &&
                   OtherPartyAccepted == other.OtherPartyAccepted;
        }

        public override bool Equals(object obj)
        {
            return obj is ImmutableTrade other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TradeId.GetHashCode();
                hashCode = (hashCode * 397) ^ OtherParty.GetHashCode();
                hashCode = (hashCode * 397) ^ (ItemsOnOffer != null ? ItemsOnOffer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (PawnsOnOffer != null ? PawnsOnOffer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OtherPartyItemsOnOffer != null ? OtherPartyItemsOnOffer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OtherPartyPawnsOnOffer != null ? OtherPartyPawnsOnOffer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Accepted.GetHashCode();
                hashCode = (hashCode * 397) ^ OtherPartyAccepted.GetHashCode();
                return hashCode;
            }
        }
    }
}