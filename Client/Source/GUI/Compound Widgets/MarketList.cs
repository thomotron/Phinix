using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Utils;
using Verse;
using Shop = Agora.Shop;
using ShopStock = Agora.ShopStock;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Scrollable list of every player market as a card: its name and owner, a row of item icons with prices, and a
    /// button to browse it (or manage it, if it's ours, with the silver waiting in its till).
    /// </summary>
    public class MarketList
    {
        private const float SCROLLBAR_WIDTH = 16f;
        private const float CARD_HEIGHT = 92f;
        private const float CARD_GAP = 6f;
        private const float BUTTON_WIDTH = 100f;
        private const float BUTTON_HEIGHT = 30f;
        private const float SIDE_WIDTH = 130f;
        private const float TILE_SIZE = 40f;
        private const float TILE_GAP = 8f;

        /// <summary>
        /// Most items shown on a card before a "+n" tile.
        /// </summary>
        private const int PREVIEW_ITEMS = 8;

        /// <summary>
        /// How far down the list is scrolled. Unity changes this for us as the player scrolls.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Draws the list. Called every frame while the Market tab is open.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public void Draw(Rect inRect)
        {
            // Our own market first, then everyone else's, newest first (GetShops already sorts by newest)
            Shop[] shops = Client.Instance.GetVisibleShops()
                .OrderByDescending(shop => shop.OwnerUuid == Client.Instance.Uuid)
                .ToArray();

            if (shops.Length == 0)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_shop_noShops".Translate());
                return;
            }

            HashSet<string> online = new HashSet<string>(Client.Instance.GetUsers(true).Select(user => user.Uuid));

            // The scroll bar only takes space when the cards don't all fit
            float contentHeight = shops.Length * (CARD_HEIGHT + CARD_GAP) - CARD_GAP;
            float cardWidth = inRect.width - (contentHeight > inRect.height ? SCROLLBAR_WIDTH : 0f);
            Rect viewRect = new Rect(0f, 0f, cardWidth, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            for (int i = 0; i < shops.Length; i++)
            {
                drawCard(new Rect(0f, i * (CARD_HEIGHT + CARD_GAP), cardWidth, CARD_HEIGHT), shops[i], online);
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws one market's card.
        /// </summary>
        private static void drawCard(Rect rect, Shop shop, HashSet<string> online)
        {
            bool ours = shop.OwnerUuid == Client.Instance.Uuid;
            bool empty = shop.Stock.Count == 0;

            // Ours stands out with a gold edge
            if (ours)
            {
                PhimeStyle.Card(rect, PhimeStyle.GoldCardFill, PhimeStyle.GoldCardBorder);
                PhimeStyle.AccentEdge(rect);
            }
            else
            {
                PhimeStyle.Card(rect, PhimeStyle.CardFill, PhimeStyle.CardBorder);
            }

            Rect sideRect = new Rect(rect.xMax - SIDE_WIDTH - 12f, rect.y, SIDE_WIDTH, rect.height);
            float mainRight = sideRect.x - 12f;

            // Top line: "Rook's Armory  ● Rook", or "your market", with "opened 5 hours ago" on the right
            float x = rect.x + 14f;
            float titleY = rect.y + 6f;
            string age = shop.OpenedTime > 0 ? PhimeStyle.OpenedAgo(shop.OpenedTime) : "";
            float ageWidth = PhimeStyle.TextWidth(age, GameFont.Tiny);
            PhimeStyle.Label(new Rect(mainRight - ageWidth, titleY, ageWidth + 2f, 22f), age, PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleRight);

            float titleSpace = mainRight - ageWidth - 20f - x;
            string title = PhimeStyle.Fit(shop.Name, titleSpace * 0.6f, GameFont.Small);
            float titleWidth = PhimeStyle.TextWidth(title, GameFont.Small);
            PhimeStyle.Label(new Rect(x, titleY, titleWidth + 2f, 22f), title, empty ? PhimeStyle.Muted : Color.white, GameFont.Small, TextAnchor.MiddleLeft);
            x += titleWidth + 10f;

            if (ours)
            {
                PhimeStyle.Label(new Rect(x, titleY, 150f, 22f), "Phinix_shop_yours".Translate(), PhimeStyle.Gold, GameFont.Tiny, TextAnchor.MiddleLeft);
            }
            else
            {
                if (!Client.Instance.TryGetDisplayName(shop.OwnerUuid, out string rawOwner)) rawOwner = "???";
                bool ownerOnline = online.Contains(shop.OwnerUuid);
                PhimeStyle.Dot(new Vector2(x + 3f, titleY + 12f), 7f, ownerOnline ? PhimeStyle.Green : PhimeStyle.OfflineDot);
                x += 11f;

                string owner = TextHelper.StripRichText(rawOwner);
                if (!ownerOnline) owner = "Phinix_shop_ownerOffline".Translate(owner);
                Rect ownerRect = new Rect(x, titleY, mainRight - ageWidth - 12f - x, 22f);
                PhimeStyle.Label(ownerRect, PhimeStyle.Fit(owner, ownerRect.width, GameFont.Tiny), ownerOnline ? PhimeStyle.NameColor(rawOwner, Color.white) : PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
            }

            // Middle: a row of item tiles with prices, or a note when it's empty
            if (empty)
            {
                PhimeStyle.Label(new Rect(rect.x + 14f, rect.y + 40f, mainRight - rect.x - 14f, 20f), "Phinix_shop_noStock".Translate(), PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
            }
            else
            {
                drawStrip(new Rect(rect.x + 14f, rect.y + 32f, mainRight - rect.x - 14f, TILE_SIZE + 18f), shop);
            }

            // Right: Manage (ours) or Browse, and the till or the number of listings under it
            Rect buttonRect = new Rect(sideRect.xMax - BUTTON_WIDTH, rect.y + 20f, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (PhimeStyle.Button(buttonRect, (ours ? "Phinix_shop_manageButton" : "Phinix_shop_browseButton").Translate()))
            {
                Find.WindowStack.Add(new ShopWindow(shop.ShopId));
            }

            Rect noteRect = new Rect(sideRect.x, buttonRect.yMax + 6f, sideRect.width, 20f);
            if (ours && shop.Till > 0)
            {
                string till = "Phinix_shop_tillToCollect".Translate(shop.Till);
                float tillWidth = PhimeStyle.TextWidth(till, GameFont.Tiny);
                Widgets.ThingIcon(new Rect(noteRect.xMax - tillWidth - 18f, noteRect.center.y - 7f, 14f, 14f), ThingDefOf.Silver);
                PhimeStyle.Label(noteRect, till, PhimeStyle.Gold, GameFont.Tiny, TextAnchor.MiddleRight);
            }
            else if (!empty)
            {
                string listings = shop.Stock.Count == 1 ? "Phinix_shop_listing".Translate().ToString() : "Phinix_shop_listings".Translate(shop.Stock.Count).ToString();
                PhimeStyle.Label(noteRect, listings, PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleRight);
            }
        }

        /// <summary>
        /// Draws up to <see cref="PREVIEW_ITEMS"/> item tiles (fewer if the card is narrow) with the stack count in the
        /// corner and the price under each, then a "+n" tile for the rest. Hovering a tile shows the item's full name.
        /// </summary>
        private static void drawStrip(Rect rect, Shop shop)
        {
            int fits = Mathf.Max(1, Mathf.FloorToInt((rect.width + TILE_GAP) / (TILE_SIZE + TILE_GAP)));
            int shown = Mathf.Min(shop.Stock.Count, Mathf.Min(PREVIEW_ITEMS, fits));
            int more = shop.Stock.Count - shown;
            // Make room for the "+n" tile by showing one fewer item
            if (more > 0 && shown == fits)
            {
                shown--;
                more++;
            }

            for (int i = 0; i < shown; i++)
            {
                ShopStock stock = shop.Stock[i];
                Rect tileRect = new Rect(rect.x + i * (TILE_SIZE + TILE_GAP), rect.y, TILE_SIZE, TILE_SIZE);
                bool known = ShopItems.IsIdentifiable(stock.Item);

                PhimeStyle.Card(tileRect, known ? PhimeStyle.TileFill : new Color(0.165f, 0.118f, 0.114f), known ? PhimeStyle.TileBorder : new Color(0.54f, 0.29f, 0.27f));
                ShopItems.DrawIcon(tileRect.ContractedBy(4f), stock.Item);

                // Stack count in the bottom-right corner, with a dark copy behind it so it reads over any icon
                int count = stock.Item.StackCount;
                if (count > 1)
                {
                    Rect countRect = new Rect(tileRect.x, tileRect.y, tileRect.width - 2f, tileRect.height);
                    PhimeStyle.Label(new Rect(countRect.x + 1f, countRect.y + 1f, countRect.width, countRect.height), count.ToString(), Color.black, GameFont.Tiny, TextAnchor.LowerRight);
                    PhimeStyle.Label(countRect, count.ToString(), Color.white, GameFont.Tiny, TextAnchor.LowerRight);
                }

                // Price under the tile: "45 ea" for a stack, just the price for one. Items this game doesn't know
                // can't be bought, so they say so instead
                Rect priceRect = new Rect(tileRect.x - 4f, tileRect.yMax + 1f, tileRect.width + 8f, 16f);
                if (!known)
                {
                    PhimeStyle.Label(priceRect, "Phinix_shop_unknownShort".Translate(), PhimeStyle.Warning, GameFont.Tiny, TextAnchor.MiddleCenter);
                }
                else
                {
                    string price = count > 1 ? "Phinix_shop_each".Translate(stock.PricePerUnit).ToString() : stock.PricePerUnit.ToString();
                    PhimeStyle.Label(priceRect, price, PhimeStyle.Silver, GameFont.Tiny, TextAnchor.MiddleCenter);
                }

                TooltipHandler.TipRegion(tileRect, ShopItems.Label(stock.Item));
            }

            if (more > 0)
            {
                Rect moreRect = new Rect(rect.x + shown * (TILE_SIZE + TILE_GAP), rect.y, TILE_SIZE, TILE_SIZE);
                Color previous = UnityEngine.GUI.color;
                UnityEngine.GUI.color = PhimeStyle.TileBorder;
                Widgets.DrawBox(moreRect);
                UnityEngine.GUI.color = previous;
                PhimeStyle.Label(moreRect, "+" + more, PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
        }
    }
}
