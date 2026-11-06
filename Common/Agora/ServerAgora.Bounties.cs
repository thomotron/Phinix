using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Trading;
using Utils;

namespace Agora
{
    // The bounty half of ServerAgora. See the note on "partial" at the top of ServerAgora.cs.
    //
    // How a bounty moves through the server:
    //
    //   1. POST     The poster's client takes the reward off their map and sends a PostBountyPacket. We check it, give
    //               it an ID and expiry time, and keep it in `bounties`. From now on WE are holding the reward items.
    //
    //   2. FULFIL   Another player's client takes the wanted items off their map and sends a FulfilBountyPacket. We
    //               check the items really match what the bounty asks for. If they do, we remove the bounty and make
    //               two deliveries: the wanted items to the poster, and the reward to the fulfiller.
    //
    //   3. CANCEL   The poster asks for the bounty back. We remove it and deliver the reward back to them.
    //
    //   4. EXPIRE   A timer checks every minute for bounties past their expiry time, and does the same as cancel.
    //
    // A "delivery" goes straight to the player if they're online. If they're offline, it waits in
    // `pendingDeliveries` and is sent the next time they log in. Either way nothing is lost.
    //
    // Because the server is holding real items, `bounties` and `pendingDeliveries` are saved to disk (see Save and
    // Load at the bottom). Player markets are saved with them, for the same reason.
    public partial class ServerAgora
    {
        /// <summary>
        /// Every bounty on the board, looked up by bounty ID.
        /// </summary>
        private Dictionary<string, Bounty> bounties = new Dictionary<string, Bounty>();

        /// <summary>
        /// Deliveries waiting for their recipient to log in.
        /// </summary>
        private List<PendingBountyDelivery> pendingDeliveries = new List<PendingBountyDelivery>();

        /// <summary>
        /// Players whose client confirms each delivery once it's in their colony (<see cref="AckDeliveriesPacket"/>).
        /// Their deliveries stay in <see cref="pendingDeliveries"/> until confirmed and are sent again on their next
        /// login if they never were, so a delivery lost on the way (dropped connection, crash) still arrives. Older
        /// clients never confirm, so theirs are let go once sent, as before. Protected by <see cref="bountiesLock"/>.
        /// </summary>
        private HashSet<string> ackingUuids = new HashSet<string>();

        /// <summary>
        /// One lock protects both <see cref="bounties"/> and <see cref="pendingDeliveries"/>.
        /// </summary>
        /// <remarks>
        /// One lock for both (rather than one each) because fulfilling a bounty changes both at once: it removes the
        /// bounty and may add deliveries. With a single lock, nobody can ever see a half-finished fulfilment, such as
        /// the bounty gone but the delivery not saved yet.
        /// </remarks>
        private readonly object bountiesLock = new object();

        /// <summary>
        /// Limits how often each user can post a bounty, the same way <see cref="shopRateLimiter"/> does for markets.
        /// </summary>
        private RateLimiter bountyRateLimiter;

        /// <summary>
        /// Most bounties one user can have up at once.
        /// </summary>
        private int maxBountiesPerUser;

        /// <summary>
        /// How long a bounty stays up before it expires.
        /// </summary>
        private TimeSpan bountyLifetime;

        /// <summary>
        /// Runs <see cref="expireBounties"/> every minute.
        /// </summary>
        /// <remarks>
        /// We keep a reference to it in a field because .NET throws away (garbage collects) timers nothing refers to,
        /// which would quietly stop expiry from ever happening.
        /// </remarks>
        private Timer expiryTimer;

        /// <summary>
        /// Most items a bounty can want, and separately, most items it can offer as a reward.
        /// </summary>
        public const int MAX_ITEMS_PER_BOUNTY = 10;

        /// <summary>
        /// Sets up the bounty half. Called from the constructor in ServerAgora.cs.
        /// </summary>
        private void initBounties(int maxBountiesPerUser, int bountyExpiryDays, int postRateLimitPosts, int postRateLimitSeconds)
        {
            this.maxBountiesPerUser = maxBountiesPerUser;
            this.bountyLifetime = TimeSpan.FromDays(bountyExpiryDays);
            this.bountyRateLimiter = new RateLimiter(postRateLimitPosts, TimeSpan.FromSeconds(postRateLimitSeconds));

            // First check after one minute, then every minute after that
            expiryTimer = new Timer(_ => expireBounties(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        #region Packet handlers

        /// <summary>
        /// Handles a player putting up a new bounty.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="PostBountyPacket"/></param>
        private void postBountyPacketHandler(string connectionId, PostBountyPacket packet)
        {
            // The same "who are you?" checks as every other packet
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                // Note that when a post fails the client puts the reward back on its map, so refusing is always safe
                sendPostBountyResponse(connectionId, false, packet.Token, "", "Session is not valid");
                return;
            }

            if (packet.Bounty == null)
            {
                sendPostBountyResponse(connectionId, false, packet.Token, "", "No bounty was sent");
                return;
            }

            if (packet.Bounty.WantedItems.Count == 0 || packet.Bounty.WantedItems.Count > MAX_ITEMS_PER_BOUNTY ||
                packet.Bounty.RewardItems.Count == 0 || packet.Bounty.RewardItems.Count > MAX_ITEMS_PER_BOUNTY)
            {
                sendPostBountyResponse(connectionId, false, packet.Token, "", string.Format("A bounty needs between 1 and {0} wanted items and between 1 and {0} reward items", MAX_ITEMS_PER_BOUNTY));
                return;
            }

            // A wanted stack of 0 (or less) would make the bounty free to fulfil
            if (packet.Bounty.WantedItems.Any(item => item.StackCount < 1))
            {
                sendPostBountyResponse(connectionId, false, packet.Token, "", "Every wanted item needs an amount of at least 1");
                return;
            }

            if (!bountyRateLimiter.TryAct(packet.Uuid))
            {
                sendPostBountyResponse(connectionId, false, packet.Token, "", "You are posting too fast!");
                return;
            }

            // Copy the bounty and overwrite everything the server decides, just like a listing (never trust the client)
            Bounty bounty = packet.Bounty.Clone();
            bounty.BountyId = Guid.NewGuid().ToString();
            bounty.PosterUuid = packet.Uuid;
            bounty.PostedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bounty.ExpiryTime = DateTimeOffset.UtcNow.Add(bountyLifetime).ToUnixTimeSeconds();
            bounty.Note = TextHelper.SanitiseRichText(bounty.Note);
            if (bounty.Note.Length > MAX_NOTE_LENGTH) bounty.Note = bounty.Note.Substring(0, MAX_NOTE_LENGTH);

            lock (bountiesLock)
            {
                // The limit check and the add happen inside the same lock, so two posts arriving at the same moment
                // can't both squeeze in under the limit
                int alreadyPosted = bounties.Values.Count(existing => existing.PosterUuid == packet.Uuid);
                if (alreadyPosted >= maxBountiesPerUser)
                {
                    sendPostBountyResponse(connectionId, false, packet.Token, "", string.Format("You can only have {0} bounties up at once. Cancel one first", maxBountiesPerUser));
                    return;
                }

                bounties.Add(bounty.BountyId, bounty);
                rememberToken(packet.Uuid, packet.Token);  // Receipt, in case our answer gets lost (ServerAgora.Escrow.cs)
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} posted bounty {1}", packet.Uuid.Highlight(HighlightType.UUID), bounty.BountyId)));

            sendPostBountyResponse(connectionId, true, packet.Token, bounty.BountyId, "");
            broadcastSyncBounties();
        }

        /// <summary>
        /// Handles a poster taking their bounty down. Their reward is delivered back to them.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="CancelBountyPacket"/></param>
        private void cancelBountyPacketHandler(string connectionId, CancelBountyPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            Bounty bounty;
            lock (bountiesLock)
            {
                // Only the poster can cancel. Checking ownership before removing matters, otherwise anyone could
                // cancel anyone's bounty (and the reward would still go to the right person, but it'd be rude)
                if (!bounties.TryGetValue(packet.BountyId, out bounty) || bounty.PosterUuid != packet.Uuid) return;

                bounties.Remove(packet.BountyId);
                deliver(bounty.PosterUuid, bounty.BountyId, BountyDeliveryReason.Cancelled, "", bounty.RewardItems);
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} cancelled bounty {1}", packet.Uuid.Highlight(HighlightType.UUID), bounty.BountyId)));

            broadcastSyncBounties();
        }

        /// <summary>
        /// Handles a player handing in the items a bounty wants.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <see cref="FulfilBountyPacket"/></param>
        private void fulfilBountyPacketHandler(string connectionId, FulfilBountyPacket packet)
        {
            // Every refusal below sends Success = false, which makes the fulfiller's client put their items back
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                sendFulfilBountyResponse(connectionId, false, packet.Token, "Session is not valid");
                return;
            }

            // On a companion, only players from the poster's main server can see the bounty, so only they can fulfil it.
            // Checked before the main lock, since it reads the user store (see visibleTo in ServerAgora.cs)
            string posterUuid;
            lock (bountiesLock) posterUuid = bounties.TryGetValue(packet.BountyId, out Bounty peek) ? peek.PosterUuid : null;
            if (posterUuid != null && !userManager.SameHomeServer(packet.Uuid, posterUuid))
            {
                sendFulfilBountyResponse(connectionId, false, packet.Token, "That bounty isn't available any more");
                return;
            }

            Bounty bounty;
            lock (bountiesLock)
            {
                // The bounty might already be gone, e.g. someone else fulfilled it a moment earlier. The lock means
                // only one fulfiller can ever get past this point for a given bounty
                if (!bounties.TryGetValue(packet.BountyId, out bounty))
                {
                    sendFulfilBountyResponse(connectionId, false, packet.Token, "That bounty isn't available any more");
                    return;
                }

                if (bounty.PosterUuid == packet.Uuid)
                {
                    sendFulfilBountyResponse(connectionId, false, packet.Token, "You can't fulfil your own bounty");
                    return;
                }

                if (!ItemsSatisfyBounty(bounty, packet.Items, out string reason))
                {
                    sendFulfilBountyResponse(connectionId, false, packet.Token, reason);
                    return;
                }

                // It's a match. Take the bounty down and hand everything over:
                // what was asked for goes to the poster, the reward goes to the fulfiller
                bounties.Remove(bounty.BountyId);
                deliver(bounty.PosterUuid, bounty.BountyId, BountyDeliveryReason.Fulfilled, packet.Uuid, packet.Items);
                deliver(packet.Uuid, bounty.BountyId, BountyDeliveryReason.Reward, bounty.PosterUuid, bounty.RewardItems);
                rememberToken(packet.Uuid, packet.Token);
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} fulfilled bounty {1}", packet.Uuid.Highlight(HighlightType.UUID), bounty.BountyId)));

            // Tell the fulfiller it worked, so their client gets rid of the items it was holding. (Their reward was
            // already sent by deliver() above. The client handles the two packets separately, so the order doesn't matter.)
            sendFulfilBountyResponse(connectionId, true, packet.Token, "");
            broadcastSyncBounties();
        }

        /// <summary>
        /// Checks whether some handed-in items cover everything a bounty wants.
        /// </summary>
        /// <remarks>
        /// Public and static so the client can run exactly the same check before sending anything. That way the Fulfil
        /// button is only clickable when the server is going to say yes.
        /// <para>
        /// The rules, for each wanted item:
        /// - add up every handed-in item with the same def name whose quality is good enough
        /// - that total has to be at least the amount wanted
        /// Items that have no quality (Quality.None) always count as good enough.
        /// </para>
        /// <para>
        /// Anything handed in that the bounty didn't ask for is refused, so a buggy client can't accidentally throw
        /// away a player's items.
        /// </para>
        /// </remarks>
        /// <param name="bounty">Bounty being fulfilled</param>
        /// <param name="items">Items handed in</param>
        /// <param name="reason">Why the items aren't enough, if they aren't</param>
        /// <returns>Whether the items satisfy the bounty</returns>
        public static bool ItemsSatisfyBounty(Bounty bounty, IEnumerable<ProtoThing> items, out string reason)
        {
            return ItemsSatisfy(bounty.WantedItems, items, out reason);
        }

        /// <summary>
        /// The rules of <see cref="ItemsSatisfyBounty"/> for any list of wanted items. Contracts use it for each delivery.
        /// </summary>
        /// <param name="wantedItems">What's wanted. Each item's Quality is the lowest accepted</param>
        /// <param name="items">Items handed in</param>
        /// <param name="reason">Why the items aren't enough, if they aren't</param>
        /// <returns>Whether the items cover everything wanted</returns>
        public static bool ItemsSatisfy(IEnumerable<ProtoThing> wantedItems, IEnumerable<ProtoThing> items, out string reason)
        {
            // Turn both into lists once, since we loop over them several times
            List<ProtoThing> wantedList = wantedItems.ToList();
            List<ProtoThing> handedIn = items.ToList();

            // Python equivalent: wanted_names = {w.DefName for w in wanted_list}
            HashSet<string> wantedDefNames = new HashSet<string>(wantedList.Select(wanted => wanted.DefName));
            if (handedIn.Any(item => !wantedDefNames.Contains(item.DefName)))
            {
                reason = "Some of those items weren't asked for";
                return false;
            }

            foreach (ProtoThing wanted in wantedList)
            {
                int total = handedIn
                    .Where(item => item.DefName == wanted.DefName && QualityIsEnough(item.Quality, wanted.Quality))
                    .Sum(item => item.StackCount);

                if (total < wanted.StackCount)
                {
                    reason = string.Format("Not enough {0} (needs {1}, got {2})", wanted.DefName, wanted.StackCount, total);
                    return false;
                }
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// Whether an item's quality meets a minimum. Items without a quality always pass.
        /// </summary>
        /// <param name="quality">The item's quality</param>
        /// <param name="minimum">Lowest acceptable quality</param>
        /// <returns>Whether it's good enough</returns>
        public static bool QualityIsEnough(Quality quality, Quality minimum)
        {
            // Quality is an enum, which is really a number underneath (Awful = 0 up to Legendary = 6), so we can
            // compare with >= just like ints. None is 7, so it needs checking first or it'd beat Legendary
            if (quality == Quality.None || minimum == Quality.None) return true;
            return quality >= minimum;
        }

        #endregion

        #region Deliveries, login and expiry

        /// <summary>
        /// Sends items to a player, or saves them for later if the player is offline.
        /// Must be called while holding <see cref="bountiesLock"/>, since it may change <see cref="pendingDeliveries"/>.
        /// </summary>
        /// <param name="recipientUuid">Who gets the items</param>
        /// <param name="bountyId">Bounty the items came from</param>
        /// <param name="reason">Why they're getting them</param>
        /// <param name="otherPartyUuid">The other player involved, or empty</param>
        /// <param name="items">Items to deliver</param>
        /// <param name="detail">Extra explanation shown in the player's letter. Optional, contracts use it</param>
        private void deliver(string recipientUuid, string bountyId, BountyDeliveryReason reason, string otherPartyUuid, IEnumerable<ProtoThing> items, string detail = "")
        {
            BountyDeliveryPacket delivery = new BountyDeliveryPacket
            {
                BountyId = bountyId,
                Reason = reason,
                OtherPartyUuid = otherPartyUuid ?? "",
                Detail = detail ?? ""
            };
            delivery.Items.AddRange(items);
            delivery.DeliveryId = Guid.NewGuid().ToString();

            // Items are changing hands, so get it on disk soon
            SaveRequests.Request();

            // TryGetConnection fails if they're not connected. If they are, try to send it straight away. A client that
            // confirms deliveries has it kept until it does
            bool sent = userManager.TryGetConnection(recipientUuid, out string connectionId) &&
                        netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(delivery).ToByteArray());
            if (sent && !ackingUuids.Contains(recipientUuid)) return;

            // Offline (or the send failed, or we're waiting to hear it arrived), so keep it
            pendingDeliveries.Add(new PendingBountyDelivery { RecipientUuid = recipientUuid, Delivery = delivery });
            if (sent) return;
            RaiseLogEntry(new LogEventArgs(string.Format("Holding a bounty delivery for {0} until they log in", recipientUuid.Highlight(HighlightType.UUID)), LogLevel.DEBUG));
        }

        /// <summary>
        /// Sends a newly logged-in player the bounty board, plus any deliveries that arrived while they were away.
        /// Called from <see cref="loginHandler"/> in ServerAgora.cs.
        /// </summary>
        /// <param name="connectionId">Their connection ID</param>
        /// <param name="uuid">Their UUID</param>
        private void bountyLoginHandler(string connectionId, string uuid)
        {
            sendSyncBounties(connectionId, uuid);

            // And their contracts (in ServerAgora.Contracts.cs)
            sendSyncContracts(connectionId, uuid);

            lock (bountiesLock)
            {
                // Pull out this player's deliveries. ToList() makes a copy, so we can safely remove from the real list
                // while looping (changing a list while a foreach walks over it throws an error in C#, and is a bug
                // waiting to happen in Python too)
                foreach (PendingBountyDelivery pending in pendingDeliveries.Where(p => p.RecipientUuid == uuid).ToList())
                {
                    // Only drop it from the list once it's actually been sent. If sending fails it stays for next time.
                    // A client that confirms deliveries keeps it on the list until it does (ackDeliveriesPacketHandler)
                    if (netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(pending.Delivery).ToByteArray()) &&
                        !ackingUuids.Contains(uuid))
                    {
                        pendingDeliveries.Remove(pending);
                    }
                }
            }
        }

        /// <summary>
        /// Takes down every bounty past its expiry time and returns its reward to the poster.
        /// Runs on <see cref="expiryTimer"/>, every minute.
        /// </summary>
        private void expireBounties()
        {
            // Timers run on a background thread, and an exception escaping one would crash the whole server,
            // so anything that goes wrong gets logged instead
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                List<Bounty> expired;

                lock (bountiesLock)
                {
                    expired = bounties.Values.Where(bounty => bounty.ExpiryTime <= now).ToList();
                    foreach (Bounty bounty in expired)
                    {
                        bounties.Remove(bounty.BountyId);
                        deliver(bounty.PosterUuid, bounty.BountyId, BountyDeliveryReason.Expired, "", bounty.RewardItems);
                    }
                }

                if (expired.Count > 0)
                {
                    RaiseLogEntry(new LogEventArgs(string.Format("Expired {0} bount{1}", expired.Count, expired.Count == 1 ? "y" : "ies")));
                    broadcastSyncBounties();
                }

                // Contracts have deadlines too, so the same timer checks them (in ServerAgora.Contracts.cs)
                checkContracts();
            }
            catch (Exception e)
            {
                RaiseLogEntry(new LogEventArgs("Failed to expire bounties: " + e, LogLevel.ERROR));
            }
        }

        #endregion

        #region Sending

        /// <summary>
        /// Builds a <see cref="SyncBountiesPacket"/> holding every bounty the viewer can see, ready to send.
        /// </summary>
        /// <param name="viewerUuid">Who it's for (only matters on a companion)</param>
        private Google.Protobuf.WellKnownTypes.Any packSyncBounties(string viewerUuid)
        {
            List<Bounty> all;
            lock (bountiesLock)
            {
                all = bounties.Values.ToList();
            }

            SyncBountiesPacket packet = new SyncBountiesPacket();
            packet.Bounties.AddRange(visibleTo(viewerUuid, all, bounty => bounty.PosterUuid));
            return ProtobufPacketHelper.Pack(packet);
        }

        /// <summary>
        /// Sends the bounty board to one connection.
        /// </summary>
        private void sendSyncBounties(string connectionId, string uuid)
        {
            if (!netServer.TrySend(connectionId, MODULE_NAME, packSyncBounties(uuid).ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send SyncBountiesPacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends the bounty board to every logged-in player.
        /// </summary>
        private void broadcastSyncBounties() => broadcastPerViewer(packSyncBounties, "SyncBountiesPacket");

        private void sendPostBountyResponse(string connectionId, bool success, string token, string bountyId, string failureMessage)
        {
            PostBountyResponsePacket packet = new PostBountyResponsePacket
            {
                Success = success,
                Token = token ?? "",
                BountyId = bountyId,
                FailureMessage = failureMessage
            };
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        private void sendFulfilBountyResponse(string connectionId, bool success, string token, string failureMessage)
        {
            FulfilBountyResponsePacket packet = new FulfilBountyResponsePacket
            {
                Success = success,
                Token = token ?? "",
                FailureMessage = failureMessage
            };
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        #endregion

        #region Saving and loading

        /// <summary>
        /// Saves bounties, pending deliveries, contracts and player markets to disk, overwriting the file if it exists.
        /// </summary>
        /// <remarks>
        /// Same approach as <c>ServerTrading.Save</c>: put everything into one protobuf message and write its bytes out.
        /// Protobuf is good at this because it's the same format we already send over the network.
        /// </remarks>
        /// <param name="path">File to save to</param>
        public void Save(string path)
        {
            lock (bountiesLock)
            {
                BountyStore store = new BountyStore();
                store.Bounties.AddRange(bounties.Values);
                store.PendingDeliveries.AddRange(pendingDeliveries);
                store.Contracts.AddRange(contracts.Values);
                forgetOldTokens();
                store.AcceptedTokens.AddRange(acceptedTokens.Values);

                // Player markets hold real items too, and share this lock (see ServerAgora.Shops.cs)
                store.Shops.AddRange(shops.Values);
                store.AckingUuids.AddRange(ackingUuids);

                // Write to a temporary file first, then swap it in. If the server crashed halfway through writing,
                // the old save would still be intact instead of half-overwritten (which would lose people's items)
                SafeFile.WriteAllBytes(path, store.ToByteArray());

                RaiseLogEntry(new LogEventArgs(string.Format("Saved {0} market{1}, {2} bount{3}, {6} contract{7} and {4} pending deliver{5}", shops.Count, shops.Count == 1 ? "" : "s", bounties.Count, bounties.Count == 1 ? "y" : "ies", pendingDeliveries.Count, pendingDeliveries.Count == 1 ? "y" : "ies", contracts.Count, contracts.Count == 1 ? "" : "s")));
            }
        }

        /// <summary>
        /// Loads bounties, pending deliveries, contracts and player markets from disk. Starts empty if the file doesn't exist yet.
        /// </summary>
        /// <param name="path">File to load from</param>
        public void Load(string path)
        {
            lock (bountiesLock)
            {
                // A damaged file throws instead of starting empty, so the items held in it never get saved over (see SafeFile)
                if (!SafeFile.TryRead(path, BountyStore.Parser.ParseFrom, out BountyStore store, out string usedPath))
                {
                    RaiseLogEntry(new LogEventArgs("No bounty database, starting with an empty board"));
                    return;
                }
                if (usedPath != path) RaiseLogEntry(new LogEventArgs(string.Format("The bounty database was damaged or missing, so loaded {0} instead", usedPath), LogLevel.WARNING));

                // ToDictionary is like Python's {b.BountyId: b for b in store.Bounties}
                bounties = store.Bounties.ToDictionary(bounty => bounty.BountyId, bounty => bounty);
                pendingDeliveries = store.PendingDeliveries.ToList();
                contracts = store.Contracts.ToDictionary(contract => contract.ContractId, contract => contract);
                acceptedTokens = store.AcceptedTokens.ToDictionary(receipt => receipt.Token, receipt => receipt);
                shops = store.Shops.ToDictionary(shop => shop.ShopId, shop => shop);
                ackingUuids = new HashSet<string>(store.AckingUuids);

                // Listings from the old market board (store.Listings) are left behind. They were only adverts and held no
                // items, so nothing is lost
                RaiseLogEntry(new LogEventArgs(string.Format("Loaded {0} market{1}, {2} bount{3}, {6} contract{7} and {4} pending deliver{5}", shops.Count, shops.Count == 1 ? "" : "s", bounties.Count, bounties.Count == 1 ? "y" : "ies", pendingDeliveries.Count, pendingDeliveries.Count == 1 ? "y" : "ies", contracts.Count, contracts.Count == 1 ? "" : "s")));
            }
        }

        #endregion
    }
}
