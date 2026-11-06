using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Trading;
using Utils;

namespace Agora
{
    // The player-market half of ServerAgora. See the note on "partial" at the top of ServerAgora.cs.
    //
    // How a market works:
    //
    //   OPEN      A player opens a market with a name (one each). It starts empty.
    //
    //   STOCK     The owner's client takes items off their map and sends them with a silver price per unit. From now on
    //             WE hold them, the same way we hold a bounty's reward, so they can be bought while the owner is offline.
    //
    //   BUY       A buyer's client takes the silver off their map and sends it with how many they want. We check the
    //             price, take that many off the stack, put the silver in the market's till and deliver the items to
    //             the buyer.
    //
    //   COLLECT   The owner asks for the till, and we deliver the silver.
    //
    //   WITHDRAW  The owner takes some or all of a stack off sale, and we deliver it back.
    //
    //   CLOSE     The owner closes the market. Everything left in it, and the till, is delivered back.
    //
    // Markets live under bountiesLock with everything else that holds items, and are saved with them (see Save in
    // ServerAgora.Bounties.cs). deliver() handles players who are offline.
    public partial class ServerAgora
    {
        /// <summary>
        /// Every open market, by shop ID. Protected by <see cref="bountiesLock"/>.
        /// </summary>
        private Dictionary<string, Shop> shops = new Dictionary<string, Shop>();

        /// <summary>
        /// Most stacks one market can hold.
        /// </summary>
        private int maxStockPerShop;

        /// <summary>
        /// Limits how often each user can open, rename or stock their market, to stop spam. Looser than the bounty limit,
        /// since setting up a market takes a few goes.
        /// </summary>
        private RateLimiter shopRateLimiter;

        /// <summary>
        /// How many times looser the market limit is than the bounty limit.
        /// </summary>
        private const int SHOP_RATE_LIMIT_MULTIPLIER = 5;

        /// <summary>
        /// Longest a market's name can be, in characters.
        /// </summary>
        public const int MAX_SHOP_NAME_LENGTH = 40;

        /// <summary>
        /// Highest price per unit, in silver. Keeps price × amount well inside what the numbers can hold.
        /// </summary>
        public const int MAX_PRICE_PER_UNIT = 100000;

        /// <summary>
        /// Def name of the item every market is paid in.
        /// </summary>
        public const string SILVER_DEF_NAME = "Silver";

        /// <summary>
        /// Biggest single stack of silver to put in one delivery. The client splits stacks to the game's stack limit
        /// anyway; this only keeps one ProtoThing's count sensible.
        /// </summary>
        private const int MAX_SILVER_PER_THING = 1000000;

        /// <summary>
        /// Sets up the market half. Called from the constructor in ServerAgora.cs.
        /// </summary>
        private void initShops(int maxStockPerShop, int postRateLimitPosts, int postRateLimitSeconds)
        {
            this.maxStockPerShop = maxStockPerShop;
            this.shopRateLimiter = new RateLimiter(postRateLimitPosts * SHOP_RATE_LIMIT_MULTIPLIER, TimeSpan.FromSeconds(postRateLimitSeconds));
        }

        #region Packet handlers

        /// <summary>
        /// Opens a player's market, or renames it if they already have one.
        /// </summary>
        private void openShopPacketHandler(string connectionId, OpenShopPacket packet)
        {
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "Session is not valid");
                return;
            }

            string name = TextHelper.StripRichText(packet.Name ?? "").Trim();
            if (name.Length == 0)
            {
                sendShopResponse(connectionId, false, "", "Your market needs a name");
                return;
            }
            if (name.Length > MAX_SHOP_NAME_LENGTH) name = name.Substring(0, MAX_SHOP_NAME_LENGTH);

            if (!shopRateLimiter.TryAct(packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "You are doing that too fast!");
                return;
            }

            bool opened;
            lock (bountiesLock)
            {
                Shop shop = shopOwnedBy(packet.Uuid);
                opened = shop == null;
                if (opened)
                {
                    shop = new Shop
                    {
                        ShopId = Guid.NewGuid().ToString(),
                        OwnerUuid = packet.Uuid,
                        OpenedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    shops.Add(shop.ShopId, shop);
                }
                shop.Name = name;
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} {1} their market \"{2}\"", packet.Uuid.Highlight(HighlightType.UUID), opened ? "opened" : "renamed", name)));

            sendShopResponse(connectionId, true, "", "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Closes a player's market, sending back everything left in it and the till.
        /// </summary>
        private void closeShopPacketHandler(string connectionId, CloseShopPacket packet)
        {
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "Session is not valid");
                return;
            }

            lock (bountiesLock)
            {
                Shop shop = shopOwnedBy(packet.Uuid);
                if (shop == null)
                {
                    sendShopResponse(connectionId, false, "", "You don't have a market open");
                    return;
                }

                // Hand everything back in the same locked block that removes the market, so nothing can be bought in between
                shops.Remove(shop.ShopId);
                if (shop.Stock.Count > 0) deliver(packet.Uuid, shop.ShopId, BountyDeliveryReason.ShopReturned, "", shop.Stock.Select(stock => stock.Item));
                if (shop.Till > 0) deliver(packet.Uuid, shop.ShopId, BountyDeliveryReason.ShopTill, "", silver(shop.Till));
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} closed their market", packet.Uuid.Highlight(HighlightType.UUID))));

            sendShopResponse(connectionId, true, "", "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Puts items up for sale in the owner's market. The items have already left the owner's map.
        /// </summary>
        private void stockShopPacketHandler(string connectionId, StockShopPacket packet)
        {
            // Every refusal sends Success = false with the token, which makes the owner's client put the items back
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, packet.Token, "Session is not valid");
                return;
            }

            if (packet.Stock.Count == 0)
            {
                sendShopResponse(connectionId, false, packet.Token, "Nothing was sent");
                return;
            }

            foreach (ShopStock stock in packet.Stock)
            {
                if (stock.Item == null || string.IsNullOrEmpty(stock.Item.DefName) || stock.Item.StackCount < 1)
                {
                    sendShopResponse(connectionId, false, packet.Token, "One of those items is empty");
                    return;
                }

                // An animal or colonist is always one of its own. More would mean the same pawn sold twice
                if ((stock.Item.Animal != null || stock.Item.Colonist != null) && stock.Item.StackCount != 1)
                {
                    sendShopResponse(connectionId, false, packet.Token, "Animals and colonists are sold one at a time");
                    return;
                }

                if (stock.PricePerUnit < 1 || stock.PricePerUnit > MAX_PRICE_PER_UNIT)
                {
                    sendShopResponse(connectionId, false, packet.Token, string.Format("Prices must be between 1 and {0} silver", MAX_PRICE_PER_UNIT));
                    return;
                }
            }

            if (!shopRateLimiter.TryAct(packet.Uuid))
            {
                sendShopResponse(connectionId, false, packet.Token, "You are doing that too fast!");
                return;
            }

            lock (bountiesLock)
            {
                Shop shop = shopOwnedBy(packet.Uuid);
                if (shop == null)
                {
                    sendShopResponse(connectionId, false, packet.Token, "Open a market first");
                    return;
                }

                // The limit check and the add happen inside the same lock, like posting a bounty
                if (shop.Stock.Count + packet.Stock.Count > maxStockPerShop)
                {
                    sendShopResponse(connectionId, false, packet.Token, string.Format("A market can hold {0} stacks at once", maxStockPerShop));
                    return;
                }

                foreach (ShopStock stock in packet.Stock)
                {
                    // Copy it and give it our own ID (never trust the client's)
                    ShopStock added = stock.Clone();
                    added.StockId = Guid.NewGuid().ToString();
                    shop.Stock.Add(added);
                }
                rememberToken(packet.Uuid, packet.Token);  // Receipt, in case our answer gets lost (ServerAgora.Escrow.cs)
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} stocked their market with {1} stack{2}", packet.Uuid.Highlight(HighlightType.UUID), packet.Stock.Count, packet.Stock.Count == 1 ? "" : "s")));

            sendShopResponse(connectionId, true, packet.Token, "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Changes the price of one of the owner's stacks.
        /// </summary>
        private void setShopPricePacketHandler(string connectionId, SetShopPricePacket packet)
        {
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "Session is not valid");
                return;
            }

            if (packet.PricePerUnit < 1 || packet.PricePerUnit > MAX_PRICE_PER_UNIT)
            {
                sendShopResponse(connectionId, false, "", string.Format("Prices must be between 1 and {0} silver", MAX_PRICE_PER_UNIT));
                return;
            }

            lock (bountiesLock)
            {
                ShopStock stock = shopOwnedBy(packet.Uuid)?.Stock.FirstOrDefault(entry => entry.StockId == packet.StockId);
                if (stock == null)
                {
                    sendShopResponse(connectionId, false, "", "That item isn't in your market any more");
                    return;
                }

                stock.PricePerUnit = packet.PricePerUnit;
            }

            sendShopResponse(connectionId, true, "", "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Takes some or all of a stack off sale and delivers it back to the owner.
        /// </summary>
        private void withdrawShopStockPacketHandler(string connectionId, WithdrawShopStockPacket packet)
        {
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "Session is not valid");
                return;
            }

            lock (bountiesLock)
            {
                Shop shop = shopOwnedBy(packet.Uuid);
                ShopStock stock = shop?.Stock.FirstOrDefault(entry => entry.StockId == packet.StockId);
                if (stock == null)
                {
                    sendShopResponse(connectionId, false, "", "That item isn't in your market any more");
                    return;
                }

                int count = packet.Count <= 0 ? stock.Item.StackCount : Math.Min(packet.Count, stock.Item.StackCount);
                deliver(packet.Uuid, shop.ShopId, BountyDeliveryReason.ShopReturned, "", new[] { takeFrom(shop, stock, count) });
            }

            sendShopResponse(connectionId, true, "", "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Sends the owner the silver in their market's till.
        /// </summary>
        private void collectShopTillPacketHandler(string connectionId, CollectShopTillPacket packet)
        {
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, "", "Session is not valid");
                return;
            }

            lock (bountiesLock)
            {
                Shop shop = shopOwnedBy(packet.Uuid);
                if (shop == null || shop.Till <= 0)
                {
                    sendShopResponse(connectionId, false, "", "There's no silver in your till");
                    return;
                }

                deliver(packet.Uuid, shop.ShopId, BountyDeliveryReason.ShopTill, "", silver(shop.Till));
                shop.Till = 0;
            }

            sendShopResponse(connectionId, true, "", "");
            broadcastSyncShops();
        }

        /// <summary>
        /// Sells some of a stack to a buyer. Their silver has already left their map.
        /// </summary>
        private void buyFromShopPacketHandler(string connectionId, BuyFromShopPacket packet)
        {
            // Every refusal sends Success = false with the token, which makes the buyer's client put their silver back
            if (!validSession(connectionId, packet.SessionId, packet.Uuid))
            {
                sendShopResponse(connectionId, false, packet.Token, "Session is not valid");
                return;
            }

            if (packet.Count < 1)
            {
                sendShopResponse(connectionId, false, packet.Token, "Buy at least one");
                return;
            }

            // Payment must be nothing but silver
            if (packet.Payment.Any(item => item.DefName != SILVER_DEF_NAME || item.StackCount < 0))
            {
                sendShopResponse(connectionId, false, packet.Token, "Markets only take silver");
                return;
            }
            long paid = packet.Payment.Sum(item => (long) item.StackCount);

            // On a companion, only players from the owner's main server can see the market, so only they can buy.
            // Checked before the main lock, since it reads the user store (see visibleTo in ServerAgora.cs)
            string ownerUuid;
            lock (bountiesLock) ownerUuid = shops.TryGetValue(packet.ShopId, out Shop peek) ? peek.OwnerUuid : null;
            if (ownerUuid != null && !userManager.SameHomeServer(packet.Uuid, ownerUuid))
            {
                sendShopResponse(connectionId, false, packet.Token, "That market isn't open any more");
                return;
            }

            ShopStock bought;
            string itemName;
            lock (bountiesLock)
            {
                // The market or stack might be gone, e.g. someone bought the last of it a moment earlier. The lock
                // means only one buyer can ever get past these checks for the same items
                if (!shops.TryGetValue(packet.ShopId, out Shop shop))
                {
                    sendShopResponse(connectionId, false, packet.Token, "That market isn't open any more");
                    return;
                }
                if (shop.OwnerUuid == packet.Uuid)
                {
                    sendShopResponse(connectionId, false, packet.Token, "You can't buy from your own market");
                    return;
                }

                ShopStock stock = shop.Stock.FirstOrDefault(entry => entry.StockId == packet.StockId);
                if (stock == null)
                {
                    sendShopResponse(connectionId, false, packet.Token, "That item has sold out");
                    return;
                }
                if (packet.Count > stock.Item.StackCount)
                {
                    sendShopResponse(connectionId, false, packet.Token, string.Format("Only {0} left", stock.Item.StackCount));
                    return;
                }
                if (stock.PricePerUnit != packet.ExpectedPricePerUnit)
                {
                    sendShopResponse(connectionId, false, packet.Token, "The price changed. Have another look");
                    return;
                }

                // Exactly the right amount, so a buggy client can't overpay and lose silver
                long price = (long) stock.PricePerUnit * packet.Count;
                if (paid != price)
                {
                    sendShopResponse(connectionId, false, packet.Token, string.Format("That costs {0} silver", price));
                    return;
                }

                // It's a sale. The silver goes in the till, the items go to the buyer
                ProtoThing items = takeFrom(shop, stock, packet.Count);
                shop.Till += price;
                deliver(packet.Uuid, shop.ShopId, BountyDeliveryReason.ShopPurchase, shop.OwnerUuid, new[] { items });
                rememberToken(packet.Uuid, packet.Token);

                bought = new ShopStock { Item = items, PricePerUnit = stock.PricePerUnit };
                itemName = items.DefName;
                ownerUuid = shop.OwnerUuid;
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} bought {1}x {2} from {3}'s market", packet.Uuid.Highlight(HighlightType.UUID), bought.Item.StackCount, itemName, ownerUuid.Highlight(HighlightType.UUID))));

            sendShopResponse(connectionId, true, packet.Token, "");
            notifySale(ownerUuid, packet.ShopId, packet.Uuid, bought);
            broadcastSyncShops();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The same "who are you?" check every packet needs.
        /// </summary>
        private bool validSession(string connectionId, string sessionId, string uuid)
        {
            return authenticator.IsAuthenticated(connectionId, sessionId) && userManager.IsLoggedIn(connectionId, uuid);
        }

        /// <summary>
        /// A player's market, or null if they haven't opened one. Must be called while holding <see cref="bountiesLock"/>.
        /// </summary>
        private Shop shopOwnedBy(string uuid) => shops.Values.FirstOrDefault(shop => shop.OwnerUuid == uuid);

        /// <summary>
        /// Takes some of a stack out of a market, dropping the stack once it's empty. Must be called while holding
        /// <see cref="bountiesLock"/>.
        /// </summary>
        /// <returns>The items taken, as their own ProtoThing</returns>
        private static ProtoThing takeFrom(Shop shop, ShopStock stock, int count)
        {
            ProtoThing taken = stock.Item.Clone();
            taken.StackCount = count;

            stock.Item.StackCount -= count;
            if (stock.Item.StackCount <= 0) shop.Stock.Remove(stock);

            return taken;
        }

        /// <summary>
        /// Silver as items to deliver, in stacks no bigger than <see cref="MAX_SILVER_PER_THING"/>.
        /// </summary>
        private static IEnumerable<ProtoThing> silver(long amount)
        {
            List<ProtoThing> stacks = new List<ProtoThing>();
            while (amount > 0)
            {
                int count = (int) Math.Min(amount, MAX_SILVER_PER_THING);
                stacks.Add(new ProtoThing { DefName = SILVER_DEF_NAME, StackCount = count, Quality = Quality.None });
                amount -= count;
            }
            return stacks;
        }

        /// <summary>
        /// Tells a market's owner someone bought from it, if they're online. Nothing is kept for later: the till already
        /// shows what sold, and a pile of "someone bought..." messages on login wouldn't help anyone.
        /// </summary>
        private void notifySale(string ownerUuid, string shopId, string buyerUuid, ShopStock bought)
        {
            if (!userManager.TryGetConnection(ownerUuid, out string connectionId)) return;

            BountyDeliveryPacket notice = new BountyDeliveryPacket
            {
                BountyId = shopId,
                Reason = BountyDeliveryReason.ShopSale,
                OtherPartyUuid = buyerUuid,
                Detail = ((long) bought.PricePerUnit * bought.Item.StackCount).ToString()
            };
            // The item rides along so the owner's client can name it. Its count is how many sold; nothing is delivered
            notice.Items.Add(bought.Item);
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(notice).ToByteArray());
        }

        #endregion

        #region Sending

        /// <summary>
        /// Builds a <see cref="SyncShopsPacket"/> holding every market the viewer can see, ready to send. Other people's
        /// tills are left out.
        /// </summary>
        /// <param name="viewerUuid">Who it's for</param>
        private Google.Protobuf.WellKnownTypes.Any packSyncShops(string viewerUuid)
        {
            List<Shop> all;
            lock (bountiesLock)
            {
                // Clone, so hiding the till below doesn't change the real market
                all = shops.Values.Select(shop => shop.Clone()).ToList();
            }

            SyncShopsPacket packet = new SyncShopsPacket();
            foreach (Shop shop in visibleTo(viewerUuid, all, shop => shop.OwnerUuid))
            {
                if (shop.OwnerUuid != viewerUuid) shop.Till = 0;
                packet.Shops.Add(shop);
            }
            return ProtobufPacketHelper.Pack(packet);
        }

        /// <summary>
        /// Sends the markets to one connection.
        /// </summary>
        private void sendSyncShops(string connectionId, string uuid)
        {
            if (!netServer.TrySend(connectionId, MODULE_NAME, packSyncShops(uuid).ToByteArray()))
            {
                RaiseLogEntry(new LogEventArgs("Failed to send SyncShopsPacket to connection " + connectionId.Highlight(HighlightType.ConnectionID), LogLevel.ERROR));
            }
        }

        /// <summary>
        /// Sends the markets to every logged-in player. Each gets their own copy, since only owners see their till.
        /// </summary>
        private void broadcastSyncShops()
        {
            foreach (string connectionId in userManager.GetConnections())
            {
                if (userManager.TryGetUuid(connectionId, out string viewerUuid)) sendSyncShops(connectionId, viewerUuid);
            }
        }

        /// <summary>
        /// Answers any market packet.
        /// </summary>
        private void sendShopResponse(string connectionId, bool success, string token, string failureMessage)
        {
            ShopResponsePacket packet = new ShopResponsePacket
            {
                Success = success,
                Token = token ?? "",
                FailureMessage = failureMessage ?? ""
            };
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        #endregion
    }
}
