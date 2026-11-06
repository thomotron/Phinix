using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Trading;
using Utils;

namespace Agora
{
    // Receipts for items the server accepted. See the explanation at the top of Packets/EscrowPackets.proto.
    //
    // Every time a bounty post, bounty fulfilment, contract proposal or contract delivery succeeds, we remember its
    // token here (rememberToken). A client that lost its connection mid-hand-over asks about its tokens after logging
    // back in (CheckTokensPacket), and we say which ones we accepted.
    public partial class ServerAgora
    {
        /// <summary>
        /// How long to keep receipts. A player who's away longer than this gets their held items put back, which
        /// could duplicate them, but 30 days is far longer than anyone sits mid-hand-over.
        /// </summary>
        private static readonly TimeSpan TOKEN_RECEIPT_LIFETIME = TimeSpan.FromDays(30);

        /// <summary>
        /// Every accepted token, by token. Protected by <see cref="bountiesLock"/>, like everything else that holds items.
        /// </summary>
        private Dictionary<string, AcceptedToken> acceptedTokens = new Dictionary<string, AcceptedToken>();

        /// <summary>
        /// Records that we accepted a player's items. Must be called while holding <see cref="bountiesLock"/>, in the
        /// same locked block that actually takes the items, so the receipt and the items can never disagree.
        /// </summary>
        private void rememberToken(string uuid, string token)
        {
            // We hold the player's items now and they'll let go of theirs, so get it on disk soon
            SaveRequests.Request();
            if (string.IsNullOrEmpty(token)) return;
            acceptedTokens[token] = new AcceptedToken { Token = token, Uuid = uuid, Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        }

        /// <summary>
        /// Answers a client asking which of its held tokens we accepted.
        /// </summary>
        private void checkTokensPacketHandler(string connectionId, CheckTokensPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            TokenResultsPacket results = new TokenResultsPacket();
            results.Checked.AddRange(packet.Tokens);
            lock (bountiesLock)
            {
                // Only count receipts that belong to whoever is asking. Tokens are random, so guessing someone else's
                // is hopeless anyway, but there's no reason to answer for other people
                results.Accepted.AddRange(packet.Tokens.Where(token => acceptedTokens.TryGetValue(token, out AcceptedToken receipt) && receipt.Uuid == packet.Uuid));
            }

            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(results).ToByteArray());
        }

        /// <summary>
        /// Handles a client confirming deliveries have arrived in its colony. The first one (sent right after logging in,
        /// even if empty) marks the player as confirming, so their deliveries are kept until confirmed from then on.
        /// Anything their game couldn't use goes back to whoever it came from.
        /// </summary>
        private void ackDeliveriesPacketHandler(string connectionId, AckDeliveriesPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            lock (bountiesLock)
            {
                if (ackingUuids.Add(packet.Uuid)) SaveRequests.Request();

                foreach (DeliveryAck ack in packet.Acks)
                {
                    PendingBountyDelivery pending = pendingDeliveries.FirstOrDefault(p => p.RecipientUuid == packet.Uuid && p.Delivery.DeliveryId == ack.DeliveryId);
                    if (pending == null) continue;  // Already confirmed, or sent before we kept them
                    pendingDeliveries.Remove(pending);
                    SaveRequests.Request();

                    // Send back what their game couldn't build, if there's someone to send it to. Things coming back
                    // are never sent back again, so nothing can bounce forever
                    BountyDeliveryPacket delivery = pending.Delivery;
                    List<ProtoThing> unusable = ack.UnusableItems.Where(i => i >= 0 && i < delivery.Items.Count).Distinct().Select(i => delivery.Items[i]).ToList();
                    if (unusable.Count > 0 && !string.IsNullOrEmpty(delivery.OtherPartyUuid) && delivery.Reason != BountyDeliveryReason.Unusable)
                    {
                        RaiseLogEntry(new LogEventArgs(string.Format("Sending {0} unusable thing(s) back from {1} to {2}", unusable.Count, packet.Uuid.Highlight(HighlightType.UUID), delivery.OtherPartyUuid.Highlight(HighlightType.UUID))));
                        deliver(delivery.OtherPartyUuid, delivery.BountyId, BountyDeliveryReason.Unusable, packet.Uuid, unusable);
                    }
                }
            }
        }

        /// <summary>
        /// Forgets receipts older than <see cref="TOKEN_RECEIPT_LIFETIME"/>. Called when saving. Must be called while
        /// holding <see cref="bountiesLock"/>.
        /// </summary>
        private void forgetOldTokens()
        {
            long cutoff = DateTimeOffset.UtcNow.Subtract(TOKEN_RECEIPT_LIFETIME).ToUnixTimeSeconds();
            foreach (string token in acceptedTokens.Values.Where(receipt => receipt.Time < cutoff).Select(receipt => receipt.Token).ToList())
            {
                acceptedTokens.Remove(token);
            }
        }
    }
}
