using System;
using System.Collections.Generic;
using System.Linq;
using Agora;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// One player's market. Buyers see what's for sale and can buy some of any stack; the owner can change prices,
    /// take things back, add items, collect the till, rename or close the market.
    /// </summary>
    /// <remarks>
    /// The window only remembers the market's ID and looks it up every frame, so it always shows the latest stock and
    /// prices as the server sends them, and says so if the market closes while it's open.
    /// </remarks>
    public class ShopWindow : Window
    {
        private const float SPACING = 10f;
        private const float ROW_HEIGHT = 36f;
        private const float TITLE_HEIGHT = 35f;
        private const float SCROLLBAR_WIDTH = 16f;
        private const float BUTTON_HEIGHT = 30f;
        private const float BUTTON_WIDTH = 110f;
        private const float NUMBER_FIELD_WIDTH = 70f;
        private const float PRICE_LABEL_WIDTH = 130f;
        private const float TOTAL_LABEL_WIDTH = 100f;

        public override Vector2 InitialSize => new Vector2(820f, 600f);

        private readonly string shopId;

        /// <summary>
        /// What the player has typed in each row's boxes, by stock ID. RimWorld's number boxes need somewhere to keep
        /// their text between frames.
        /// </summary>
        private class RowState
        {
            public int Count = 1;
            public string CountBuffer = "1";
            public int Price;
            public string PriceBuffer;
        }
        private readonly Dictionary<string, RowState> rows = new Dictionary<string, RowState>();

        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// The colony's silver, counted at most once a second rather than for every row on every frame (counting means
        /// looking through every stockpile).
        /// </summary>
        private int colonySilver;
        private float colonySilverCountedAt = -999f;
        private int ColonySilver
        {
            get
            {
                if (Time.realtimeSinceStartup - colonySilverCountedAt > 1f)
                {
                    colonySilver = ShopItems.ColonySilver();
                    colonySilverCountedAt = Time.realtimeSinceStartup;
                }
                return colonySilver;
            }
        }

        /// <param name="shopId">ID of the market to show</param>
        public ShopWindow(string shopId)
        {
            this.shopId = shopId;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shouldn't do anything by accident while typing in the boxes
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Shop shop = Client.Instance.Online ? Client.Instance.GetShop(shopId) : null;
            if (shop == null)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_shop_closed".Translate());
                return;
            }

            bool ours = shop.OwnerUuid == Client.Instance.Uuid;
            if (!Client.Instance.TryGetDisplayName(shop.OwnerUuid, out string ownerName)) ownerName = "???";
            ownerName = TextHelper.StripRichText(ownerName);

            // Title: the market's name, with who runs it underneath
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TITLE_HEIGHT), shop.Name);
            Text.Font = GameFont.Small;
            float y = inRect.y + TITLE_HEIGHT;
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, y, inRect.width / 2, Text.LineHeight), "Phinix_shop_byOwner".Translate(ownerName));
            UnityEngine.GUI.color = Color.white;

            // Buyers see how much silver they have to spend, top right
            if (!ours)
            {
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(inRect.x + inRect.width / 2, y, inRect.width / 2, Text.LineHeight), "Phinix_shop_yourSilver".Translate(ColonySilver));
                Text.Anchor = TextAnchor.UpperLeft;
            }
            y += Text.LineHeight + SPACING;

            // The owner's buttons: till, add items, rename, close
            if (ours)
            {
                drawOwnerBar(new Rect(inRect.x, y, inRect.width, BUTTON_HEIGHT), shop);
                y += BUTTON_HEIGHT + SPACING;
            }

            drawStock(new Rect(inRect.x, y, inRect.width, inRect.yMax - y), shop, ours);
        }

        /// <summary>
        /// Draws the owner's row of controls: how much is in the till, and buttons to collect it, add items, rename and close.
        /// </summary>
        private void drawOwnerBar(Rect inRect, Shop shop)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(inRect.LeftPartPixels(220f), "Phinix_shop_till".Translate(shop.Till));
            Text.Anchor = TextAnchor.UpperLeft;

            // Buttons from the right: Close, Rename, Add items, Collect
            float x = inRect.xMax - BUTTON_WIDTH;
            if (Widgets.ButtonText(new Rect(x, inRect.y, BUTTON_WIDTH, BUTTON_HEIGHT), "Phinix_shop_closeButton".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Phinix_shop_confirmClose".Translate(), () => Client.Instance.CloseShop(), destructive: true));
            }
            x -= BUTTON_WIDTH + SPACING;
            if (Widgets.ButtonText(new Rect(x, inRect.y, BUTTON_WIDTH, BUTTON_HEIGHT), "Phinix_shop_renameButton".Translate()))
            {
                Find.WindowStack.Add(new NameShopWindow(shop.Name));
            }
            x -= BUTTON_WIDTH + SPACING;
            if (Widgets.ButtonText(new Rect(x, inRect.y, BUTTON_WIDTH, BUTTON_HEIGHT), "Phinix_shop_addItemsButton".Translate()))
            {
                Find.WindowStack.Add(new StockShopWindow());
            }
            x -= BUTTON_WIDTH + SPACING;
            bool canCollect = shop.Till > 0;
            if (Widgets.ButtonText(new Rect(x, inRect.y, BUTTON_WIDTH, BUTTON_HEIGHT), "Phinix_shop_collectButton".Translate(), active: canCollect) && canCollect)
            {
                Client.Instance.CollectShopTill();
            }
        }

        /// <summary>
        /// Draws every stack for sale, one row each.
        /// </summary>
        private void drawStock(Rect inRect, Shop shop, bool ours)
        {
            Widgets.DrawMenuSection(inRect);

            if (shop.Stock.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(inRect, (ours ? "Phinix_shop_ownerNoStock" : "Phinix_shop_noStock").Translate());
                return;
            }

            Rect outRect = inRect.ContractedBy(4f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - SCROLLBAR_WIDTH, ROW_HEIGHT * shop.Stock.Count);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < shop.Stock.Count; i++)
            {
                Rect rowRect = new Rect(0f, ROW_HEIGHT * i, viewRect.width, ROW_HEIGHT);
                if (i % 2 != 0) Widgets.DrawHighlight(rowRect);

                if (ours) drawOwnerRow(rowRect, shop.Stock[i]);
                else drawBuyerRow(rowRect, shop, shop.Stock[i]);
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// The remembered box contents for a stack, made on first use. Clamps the amount if some sold in the meantime.
        /// </summary>
        private RowState rowFor(ShopStock stock)
        {
            if (!rows.TryGetValue(stock.StockId, out RowState row))
            {
                row = new RowState { Price = stock.PricePerUnit, PriceBuffer = stock.PricePerUnit.ToString() };
                rows[stock.StockId] = row;
            }

            if (row.Count > stock.Item.StackCount)
            {
                row.Count = stock.Item.StackCount;
                row.CountBuffer = row.Count.ToString();
            }
            return row;
        }

        /// <summary>
        /// Draws the left part every row shares: icon, name and how many are left, up to <paramref name="rightEdge"/>.
        /// </summary>
        private static void drawItem(Rect rowRect, ShopStock stock, float rightEdge)
        {
            ShopItems.DrawIcon(rowRect.LeftPartPixels(ROW_HEIGHT).ContractedBy(3f), stock.Item);

            Rect labelRect = new Rect(rowRect.x + ROW_HEIGHT + 4f, rowRect.y, rightEdge - rowRect.x - ROW_HEIGHT - 8f, rowRect.height);

            // Animals and colonists get an info button that opens the pawn inspector, and no "1x"
            bool pawn = ShopItems.IsPawn(stock.Item);
            if (pawn)
            {
                Rect infoRect = new Rect(labelRect.x, rowRect.center.y - 12f, 24f, 24f);
                TooltipHandler.TipRegion(infoRect, "Phinix_pawn_inspectTip".Translate());
                if (Widgets.ButtonImage(infoRect, TexButton.Info)) Find.WindowStack.Add(new PawnInspectWindow(stock.Item));
                labelRect.xMin += 28f;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            if (!ShopItems.IsIdentifiable(stock.Item))
            {
                UnityEngine.GUI.color = ColorLibrary.RedReadable;
            }
            else if (pawn && PawnCheck.Describe(stock.Item) is string missing)
            {
                // Parts of this pawn our game doesn't have (a modded bionic, a gene...) won't come with them
                UnityEngine.GUI.color = TradeWindow.MISSING_PARTS_COLOR;
                TooltipHandler.TipRegion(labelRect, "Phinix_pawnCheck_rowTip".Translate(missing));
            }
            Widgets.Label(labelRect, (pawn ? "" : stock.Item.StackCount + "x ") + ShopItems.Label(stock.Item));
            UnityEngine.GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// A row in someone else's market: the item, its price, an amount box, the total and a Buy button.
        /// </summary>
        private void drawBuyerRow(Rect rowRect, Shop shop, ShopStock stock)
        {
            RowState row = rowFor(stock);
            bool identifiable = ShopItems.IsIdentifiable(stock.Item);

            // Lay the row out right to left: Buy, total, amount box, price
            Rect buyRect = new Rect(rowRect.xMax - BUTTON_WIDTH, rowRect.center.y - BUTTON_HEIGHT / 2, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect totalRect = new Rect(buyRect.x - SPACING - TOTAL_LABEL_WIDTH, rowRect.y, TOTAL_LABEL_WIDTH, rowRect.height);
            Rect countRect = new Rect(totalRect.x - SPACING - NUMBER_FIELD_WIDTH, rowRect.center.y - 12f, NUMBER_FIELD_WIDTH, 24f);
            Rect priceRect = new Rect(countRect.x - SPACING - PRICE_LABEL_WIDTH, rowRect.y, PRICE_LABEL_WIDTH, rowRect.height);

            drawItem(rowRect, stock, priceRect.x);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(priceRect, "Phinix_shop_priceEach".Translate(stock.PricePerUnit));
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.TextFieldNumeric(countRect, ref row.Count, ref row.CountBuffer, 1, stock.Item.StackCount);

            long total = (long) stock.PricePerUnit * row.Count;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(totalRect, "Phinix_shop_total".Translate(total));
            Text.Anchor = TextAnchor.UpperLeft;

            // Why the Buy button might be greyed out, shown when hovering it
            string blockedReason = null;
            if (!identifiable) blockedReason = "Phinix_shop_cantIdentify".Translate();
            else if (total > ColonySilver) blockedReason = "Phinix_shop_notEnoughSilver".Translate(total);

            bool canBuy = blockedReason == null && row.Count >= 1;
            if (blockedReason != null) TooltipHandler.TipRegion(buyRect, blockedReason);
            if (Widgets.ButtonText(buyRect, "Phinix_shop_buyButton".Translate(), active: canBuy) && canBuy)
            {
                // Say what won't come with an animal or colonist before taking anyone's silver
                string missing = PawnCheck.DescribeAll(new[] { stock.Item });
                int count = row.Count;
                if (missing == null) buy(shop, stock, count, (int) total);
                else Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Phinix_pawnCheck_confirmBuy".Translate(missing), () => buy(shop, stock, count, (int) total), destructive: true));
            }
        }

        /// <summary>
        /// A row in our own market: the item, a price box with Set price, and an amount box with Take back.
        /// </summary>
        private void drawOwnerRow(Rect rowRect, ShopStock stock)
        {
            RowState row = rowFor(stock);

            // Right to left: Take back, amount box, Set price, price box
            Rect withdrawRect = new Rect(rowRect.xMax - BUTTON_WIDTH, rowRect.center.y - BUTTON_HEIGHT / 2, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect countRect = new Rect(withdrawRect.x - SPACING - NUMBER_FIELD_WIDTH, rowRect.center.y - 12f, NUMBER_FIELD_WIDTH, 24f);
            Rect setPriceRect = new Rect(countRect.x - SPACING * 2 - BUTTON_WIDTH, withdrawRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect priceRect = new Rect(setPriceRect.x - SPACING - NUMBER_FIELD_WIDTH, countRect.y, NUMBER_FIELD_WIDTH, 24f);

            drawItem(rowRect, stock, priceRect.x - SPACING);

            Widgets.TextFieldNumeric(priceRect, ref row.Price, ref row.PriceBuffer, 1, ServerAgora.MAX_PRICE_PER_UNIT);
            TooltipHandler.TipRegion(priceRect, "Phinix_shop_priceLabel".Translate());
            bool priceChanged = row.Price != stock.PricePerUnit && row.Price >= 1;
            if (Widgets.ButtonText(setPriceRect, "Phinix_shop_setPriceButton".Translate(), active: priceChanged) && priceChanged)
            {
                Client.Instance.SetShopPrice(stock.StockId, row.Price);
            }

            Widgets.TextFieldNumeric(countRect, ref row.Count, ref row.CountBuffer, 1, stock.Item.StackCount);
            TooltipHandler.TipRegion(countRect, "Phinix_shop_countLabel".Translate());
            if (Widgets.ButtonText(withdrawRect, "Phinix_shop_withdrawButton".Translate()))
            {
                Client.Instance.WithdrawShopStock(stock.StockId, row.Count);
            }
        }

        /// <summary>
        /// Takes the silver off the map and asks the server for the items.
        /// </summary>
        private static void buy(Shop shop, ShopStock stock, int count, int total)
        {
            ItemEscrow.Request[] payment =
            {
                new ItemEscrow.Request { Def = ThingDefOf.Silver, Count = total, MinQuality = Quality.Awful }
            };

            // The silver is held under the market's server, so it's settled with that server if the connection drops
            if (!ItemEscrow.TryTake(payment, out string token, out List<ProtoThing> silver, Client.Instance.BoardEscrowModule))
            {
                // Some silver was spent since the window drew the button. Nothing was taken
                Messages.Message("Phinix_shop_notEnoughSilver".Translate(total), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // If the request couldn't even go out (we went offline), put the silver straight back
            if (!Client.Instance.BuyFromShop(token, shop.ShopId, stock.StockId, count, stock.PricePerUnit, silver))
            {
                ItemEscrow.Resolve(token, false);
            }

            // Otherwise the server answers with the same token (Client.cs settles the silver), and the items follow by drop pod
        }
    }
}
