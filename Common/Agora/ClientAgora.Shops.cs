using System;
using System.Collections.Generic;
using System.Linq;
using Trading;

namespace Agora
{
    // The player-market half of ClientAgora. See the note on "partial" at the top of ClientAgora.cs.
    //
    // Like the bounty half, this only does networking: it keeps a copy of the markets the server sends, sends market
    // requests, and raises events when the server answers. Taking items or silver off the map, and dropping deliveries
    // in, happens in the client mod (PhinixClient), which can see RimWorld.
    public partial class ClientAgora
    {
        /// <summary>
        /// Raised whenever a new copy of the markets arrives.
        /// </summary>
        public event EventHandler OnShopsChanged;

        /// <summary>
        /// Raised when the server answers any market request. Requests that handed items over carry their token.
        /// </summary>
        public event EventHandler<ShopResponsePacket> OnShopResponse;

        /// <summary>
        /// Our copy of every market we can see, replaced whenever the server sends a new one.
        /// </summary>
        private List<Shop> shops = new List<Shop>();
        /// <summary>
        /// Lock object protecting <see cref="shops"/>, since packets arrive on the network thread while the UI reads them
        /// on the game thread.
        /// </summary>
        private readonly object shopsLock = new object();

        /// <summary>
        /// Returns a copy of every market we can see, newest first.
        /// </summary>
        public Shop[] GetShops()
        {
            lock (shopsLock)
            {
                return shops.OrderByDescending(shop => shop.OpenedTime).ToArray();
            }
        }

        /// <summary>
        /// Opens our market with the given name, or renames it if it's already open.
        /// </summary>
        /// <returns>Whether the request went out</returns>
        public bool OpenShop(string name)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new OpenShopPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, Name = name ?? "" });
            return true;
        }

        /// <summary>
        /// Closes our market. Unsold stock and the till come back through <see cref="OnBountyDelivery"/>.
        /// </summary>
        /// <returns>Whether the request went out</returns>
        public bool CloseShop()
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new CloseShopPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid });
            return true;
        }

        /// <summary>
        /// Puts items up for sale in our market. The items must already be off the map.
        /// The answer arrives through <see cref="OnShopResponse"/> carrying the same <paramref name="token"/>.
        /// </summary>
        /// <param name="token">Random ID matching this request to its answer</param>
        /// <param name="stock">The items, each with its price per unit</param>
        /// <returns>Whether the request went out. If false, the caller should put the items back</returns>
        public bool StockShop(string token, IEnumerable<ShopStock> stock)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            StockShopPacket packet = new StockShopPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, Token = token };
            packet.Stock.AddRange(stock);
            send(packet);
            return true;
        }

        /// <summary>
        /// Changes the price of one of our stacks.
        /// </summary>
        /// <returns>Whether the request went out</returns>
        public bool SetShopPrice(string stockId, int pricePerUnit)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new SetShopPricePacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, StockId = stockId, PricePerUnit = pricePerUnit });
            return true;
        }

        /// <summary>
        /// Takes some of one of our stacks off sale. It comes back through <see cref="OnBountyDelivery"/>.
        /// </summary>
        /// <param name="stockId">Which stack</param>
        /// <param name="count">How many, or 0 for all of it</param>
        /// <returns>Whether the request went out</returns>
        public bool WithdrawShopStock(string stockId, int count)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new WithdrawShopStockPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, StockId = stockId, Count = count });
            return true;
        }

        /// <summary>
        /// Asks for the silver in our market's till. It comes back through <see cref="OnBountyDelivery"/>.
        /// </summary>
        /// <returns>Whether the request went out</returns>
        public bool CollectShopTill()
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new CollectShopTillPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid });
            return true;
        }

        /// <summary>
        /// Buys some of a stack. The payment (silver) must already be off the map.
        /// The answer arrives through <see cref="OnShopResponse"/> carrying the same <paramref name="token"/>, and the
        /// items through <see cref="OnBountyDelivery"/>.
        /// </summary>
        /// <param name="token">Random ID matching this request to its answer</param>
        /// <param name="shopId">Market to buy from</param>
        /// <param name="stockId">Stack to buy from</param>
        /// <param name="count">How many</param>
        /// <param name="expectedPricePerUnit">The price we saw, so a price change in the meantime is refused</param>
        /// <param name="payment">The silver</param>
        /// <returns>Whether the request went out. If false, the caller should put the silver back</returns>
        public bool BuyFromShop(string token, string shopId, string stockId, int count, int expectedPricePerUnit, IEnumerable<ProtoThing> payment)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            BuyFromShopPacket packet = new BuyFromShopPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                ShopId = shopId,
                StockId = stockId,
                Count = count,
                ExpectedPricePerUnit = expectedPricePerUnit
            };
            packet.Payment.AddRange(payment);
            send(packet);
            return true;
        }

        /// <summary>
        /// Replaces our copy of the markets with the one the server sent.
        /// </summary>
        private void syncShopsPacketHandler(SyncShopsPacket packet)
        {
            lock (shopsLock)
            {
                shops = packet.Shops.ToList();
            }

            // Only an Agora server sends markets, and it does so as soon as we log in
            Available = true;

            OnShopsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forgets the markets, e.g. when we disconnect.
        /// </summary>
        private void clearShops()
        {
            lock (shopsLock)
            {
                shops.Clear();
            }

            OnShopsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
