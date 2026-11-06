using System;
using System.Collections.Generic;
using System.Linq;
using Agora;
using RimWorld;
using Trading;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up for putting items, animals and colonists up for sale in our market. The player picks how many of each
    /// from a list like the game's caravan window, types a price per unit, and presses the button.
    /// </summary>
    /// <remarks>
    /// Rows are grouped the way the caravan window groups them (<see cref="ColonyTransferables"/>), so "normal cloth
    /// dusters" and "good cloth dusters" (or a worn one and a new one) are listed and priced separately. The game's own
    /// list widget has no room for a price, so this window draws the list itself out of the same pieces the game uses
    /// (<see cref="TransferableUIUtility"/>), with a price box on the end of each row. Like a bounty's reward,
    /// everything leaves the map as soon as the button is pressed and waits in <see cref="ItemEscrow"/> until the server
    /// confirms.
    /// </remarks>
    public class StockShopWindow : Window
    {
        private const float SPACING = 10f;
        private const float TITLE_HEIGHT = 35f;
        private const float SUBTITLE_HEIGHT = 22f;
        private const float BUTTON_HEIGHT = 40f;
        private const float BUTTON_WIDTH = 100f;
        private const float STOCK_BUTTON_WIDTH = 160f;
        private const float BOTTOM_HEIGHT = 50f;
        private const float SCROLLBAR_WIDTH = 16f;

        // The same sizes as the game's caravan list, so the rows line up the same way
        private const float LIST_PADDING = 17f;
        private const float TOP_AREA_HEIGHT = 37f;
        private const float FIRST_ROW_Y = 6f;
        private const float ROW_HEIGHT = 30f;
        private const float SECTION_TITLE_SPACE = 5f;
        private const float COUNT_COLUMN_WIDTH = 75f;
        private const float ADJUST_COLUMN_WIDTH = 240f;
        private const float PRICE_COLUMN_WIDTH = 100f;
        private const float PRICE_FIELD_WIDTH = 62f;

        public override Vector2 InitialSize => new Vector2(860f, 720f);

        /// <summary>
        /// One row of the list: the game's row (what it is and how many are picked) plus the price typed for it.
        /// </summary>
        private class Row
        {
            public TransferableOneWay Transferable;
            public int Price;
            /// <summary>Text in the price box while the player is typing. RimWorld's number box needs one</summary>
            public string PriceBuffer;

            public bool IsPawn => Transferable.AnyThing is Pawn;
        }

        private List<Row> pawnRows;
        private List<Row> itemRows;

        /// <summary>
        /// The rows that match the search, in the chosen order. Worked out again only when the search or sorting
        /// changes, not every frame, the same as the game's list.
        /// </summary>
        private List<Row> shownPawnRows;
        private List<Row> shownItemRows;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private TransferableSorterDef sorter1 = TransferableSorterDefOf.Category;
        private TransferableSorterDef sorter2 = TransferableSorterDefOf.MarketValue;
        private Vector2 scroll = Vector2.zero;
        /// <summary>How tall the list was last frame, for the scroll bar</summary>
        private float listHeight;

        public StockShopWindow()
        {
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shouldn't put things up for sale by accident while typing
            draggable = true;

            refreshRows();
        }

        /// <summary>
        /// Lists the colony's animals, colonists and items again from scratch, with nothing picked.
        /// </summary>
        private void refreshRows()
        {
            pawnRows = ColonyTransferables.Pawns().Select(toRow).ToList();
            // Markets are paid in silver, so selling it makes no sense
            itemRows = ColonyTransferables.Items(thing => thing.def != ThingDefOf.Silver).Select(toRow).ToList();
            sortRows();
        }

        /// <summary>
        /// Wraps one of the game's rows in ours, starting the price at what one is worth to a trader.
        /// </summary>
        private static Row toRow(TransferableOneWay transferable)
        {
            int price = Math.Max(1, Mathf.RoundToInt(transferable.AnyThing.MarketValue));
            return new Row { Transferable = transferable, Price = price, PriceBuffer = price.ToString() };
        }

        /// <summary>
        /// Works out which rows match the search and puts them in the chosen order, the same way the game's list does.
        /// </summary>
        private void sortRows()
        {
            shownPawnRows = filterAndSort(pawnRows);
            shownItemRows = filterAndSort(itemRows);
        }

        private List<Row> filterAndSort(List<Row> rows)
        {
            // The (Transferable) cast tells C# which comparer to use: the game's sorters compare any Transferable
            return rows.Where(row => search.filter.Matches(row.Transferable.Label))
                .OrderBy(row => (Transferable) row.Transferable, sorter1.Comparer)
                .ThenBy(row => (Transferable) row.Transferable, sorter2.Comparer)
                .ThenBy(row => TransferableUIUtility.DefaultListOrderPriority(row.Transferable))
                .ToList();
        }

        private IEnumerable<Row> pickedRows => pawnRows.Concat(itemRows).Where(row => row.Transferable.CountToTransfer > 0);

        public override void DoWindowContents(Rect inRect)
        {
            // Title, with a line under it
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TITLE_HEIGHT), "Phinix_shop_stockTitle".Translate());
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + TITLE_HEIGHT, inRect.width, SUBTITLE_HEIGHT), "Phinix_shop_stockSubtitle".Translate());
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // The list, in a box like the caravan window's. BeginGroup makes (0, 0) the box's corner, which is where the
            // game's sort buttons and search box draw themselves
            float top = inRect.y + TITLE_HEIGHT + SUBTITLE_HEIGHT + SPACING;
            Rect boxRect = new Rect(inRect.x, top, inRect.width, inRect.yMax - BOTTOM_HEIGHT - SPACING - top);
            Widgets.DrawMenuSection(boxRect);
            Rect listRect = boxRect.ContractedBy(LIST_PADDING);
            Widgets.BeginGroup(listRect);
            drawList(listRect.AtZero());
            Widgets.EndGroup();

            drawBottom(new Rect(inRect.x, inRect.yMax - BOTTOM_HEIGHT, inRect.width, BOTTOM_HEIGHT));
        }

        /// <summary>
        /// Draws the sort buttons, the search box and the scrolling list of rows.
        /// </summary>
        private void drawList(Rect inRect)
        {
            // Lambdas ("x => ...") are little unnamed functions: here, what to do when the player picks a new sorter
            TransferableUIUtility.DoTransferableSorters(sorter1, sorter2, sorter => { sorter1 = sorter; sortRows(); }, sorter => { sorter2 = sorter; sortRows(); });
            search.noResultsMatched = shownPawnRows.Count == 0 && shownItemRows.Count == 0;
            TransferableUIUtility.DoTransferableSearcher(search, sortRows);

            // "Price each" over the price column
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            Text.Anchor = TextAnchor.LowerLeft;
            Widgets.Label(new Rect(inRect.xMax - SCROLLBAR_WIDTH - PRICE_COLUMN_WIDTH + 24f, 0f, PRICE_COLUMN_WIDTH, TOP_AREA_HEIGHT), "Phinix_shop_priceColumn".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect mainRect = new Rect(inRect.x, inRect.y + TOP_AREA_HEIGHT, inRect.width, inRect.height - TOP_AREA_HEIGHT);
            if (search.noResultsMatched)
            {
                UnityEngine.GUI.color = Color.gray;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(mainRect, "NoneBrackets".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                UnityEngine.GUI.color = Color.white;
                return;
            }

            Rect viewRect = new Rect(0f, 0f, mainRect.width - SCROLLBAR_WIDTH, Math.Max(listHeight, mainRect.height));
            Widgets.BeginScrollView(mainRect, ref scroll, viewRect);
            float curY = FIRST_ROW_Y;
            drawSection(ref curY, viewRect.width, "Phinix_shop_pawnsSection".Translate(), shownPawnRows, mainRect.height);
            drawSection(ref curY, viewRect.width, "Phinix_shop_itemsSection".Translate(), shownItemRows, mainRect.height);
            listHeight = curY;
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws a section's title and rows, moving <paramref name="curY"/> down past them. Rows scrolled out of
        /// sight are skipped, which keeps a colony with hundreds of kinds of item fast.
        /// </summary>
        private void drawSection(ref float curY, float width, string title, List<Row> rows, float visibleHeight)
        {
            if (rows.Count == 0) return;

            Widgets.ListSeparator(ref curY, width, title);
            curY += SECTION_TITLE_SPACE;
            for (int i = 0; i < rows.Count; i++)
            {
                if (curY > scroll.y - ROW_HEIGHT && curY < scroll.y + visibleHeight)
                {
                    drawRow(new Rect(0f, curY, width, ROW_HEIGHT), rows[i], i);
                }
                curY += ROW_HEIGHT;
            }
        }

        /// <summary>
        /// Draws one row like the caravan window does, right to left: price, the &lt;&lt; &lt; [n] &gt; &gt;&gt; buttons,
        /// how many we have, then the info button, icon and name in what's left.
        /// </summary>
        private void drawRow(Rect rect, Row row, int index)
        {
            TransferableOneWay transferable = row.Transferable;
            if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
            Text.Font = GameFont.Small;
            Widgets.BeginGroup(rect);
            float width = rect.width;

            // Price: a silver icon and a number box
            Rect priceRect = new Rect(width - PRICE_COLUMN_WIDTH, 0f, PRICE_COLUMN_WIDTH, rect.height);
            Widgets.ThingIcon(new Rect(priceRect.x + 4f, (rect.height - 20f) / 2f, 20f, 20f), ThingDefOf.Silver);
            Rect priceFieldRect = new Rect(priceRect.x + 28f, (rect.height - 24f) / 2f, PRICE_FIELD_WIDTH, 24f);
            // "ref" lets the number box change Price and PriceBuffer directly, like passing a mutable box in Python
            Widgets.TextFieldNumeric(priceFieldRect, ref row.Price, ref row.PriceBuffer, 1, ServerAgora.MAX_PRICE_PER_UNIT);
            width -= PRICE_COLUMN_WIDTH;

            // The game's own count buttons (a tick box for a single colonist or named animal)
            int maxCount = transferable.MaxCount;
            Rect adjustRect = new Rect(width - ADJUST_COLUMN_WIDTH, 0f, ADJUST_COLUMN_WIDTH, rect.height);
            TransferableUIUtility.DoCountAdjustInterface(adjustRect, transferable, index, 0, maxCount);
            width -= ADJUST_COLUMN_WIDTH;

            // How many we have, left out for a single colonist like the game does
            Pawn pawn = transferable.AnyThing as Pawn;
            if (!(pawn != null && pawn.RaceProps.Humanlike && maxCount == 1))
            {
                Rect countRect = new Rect(width - COUNT_COLUMN_WIDTH, 0f, COUNT_COLUMN_WIDTH, rect.height);
                Widgets.DrawHighlightIfMouseover(countRect);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(countRect.x + 5f, 0f, countRect.width - 10f, rect.height), maxCount.ToStringCached());
                TooltipHandler.TipRegion(countRect, "Phinix_picker_inColonyTip".Translate());
            }
            width -= COUNT_COLUMN_WIDTH;

            // Bonded and pregnant icons for animals, then the info button, icon and name
            TransferableUIUtility.DoExtraIcons(transferable, rect, ref width);
            TransferableUIUtility.DrawTransferableInfo(transferable, new Rect(0f, 0f, width, rect.height), Color.white);
            GenUI.ResetLabelAlign();
            Widgets.EndGroup();
        }

        /// <summary>
        /// Draws the asking-price total on the left, and Reset, Cancel and the stock button on the right.
        /// </summary>
        private void drawBottom(Rect inRect)
        {
            Rect stockRect = new Rect(inRect.xMax - STOCK_BUTTON_WIDTH, inRect.yMax - BUTTON_HEIGHT, STOCK_BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect cancelRect = new Rect(stockRect.x - SPACING - BUTTON_WIDTH, stockRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect resetRect = new Rect(cancelRect.x - SPACING - BUTTON_WIDTH, stockRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            float leftWidth = resetRect.x - SPACING * 2 - inRect.x;

            List<Row> picked = pickedRows.ToList();
            long total = picked.Sum(row => (long) row.Price * row.Transferable.CountToTransfer);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, stockRect.y, leftWidth, 22f), "Phinix_shop_stockSummary".Translate(picked.Count, total.ToString("N0")));
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, stockRect.y + 22f, leftWidth, 18f), "Phinix_shop_priceHint".Translate());
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonText(resetRect, "ResetButton".Translate()))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                refreshRows();
            }
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate())) Close();

            bool canStock = picked.Count > 0;
            if (Widgets.ButtonText(stockRect, "Phinix_shop_stockButton".Translate(), active: canStock) && canStock)
            {
                stock();
            }
        }

        /// <summary>
        /// Puts everything picked up for sale, checking first if any colonists are going, since they leave the colony at once.
        /// </summary>
        private void stock()
        {
            List<string> colonists = pickedRows
                .Where(row => row.IsPawn && ((Pawn) row.Transferable.AnyThing).RaceProps.Humanlike)
                .Select(row => row.Transferable.AnyThing.LabelShortCap)
                .ToList();
            if (colonists.Count == 0)
            {
                stockNow();
                return;
            }

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Phinix_shop_confirmColonists".Translate(string.Join(", ", colonists)), stockNow, destructive: true));
        }

        /// <summary>
        /// Takes the picked items and pawns off the map, sends them to the server with their prices and closes the window.
        /// </summary>
        /// <remarks>
        /// Items and pawns go as two separate requests with their own tokens, since they're taken off the map in
        /// different ways: items piece by piece (<see cref="ColonyTransferables.TryTakeItems"/>), pawns whole
        /// (<see cref="ItemEscrow.Hold"/>).
        /// </remarks>
        private void stockNow()
        {
            List<Row> picked = pickedRows.ToList();
            List<Row> pickedItems = picked.Where(row => !row.IsPawn).ToList();
            List<Row> pickedPawns = picked.Where(row => row.IsPawn).ToList();

            if (pickedItems.Count > 0 && !stockItems(pickedItems)) return;
            if (pickedPawns.Count > 0) stockPawns(pickedPawns);

            Close();
        }

        /// <summary>
        /// Takes the picked pawns off the map and sends them to the server, one stack of one each.
        /// </summary>
        private static void stockPawns(List<Row> pawnRows)
        {
            // The first however-many of each row, skipping any that died or left since the window opened
            List<(Pawn pawn, int price)> going = new List<(Pawn, int)>();
            foreach (Row row in pawnRows)
            {
                IEnumerable<Pawn> stillHere = row.Transferable.things.OfType<Pawn>().Where(pawn => pawn.Spawned && !pawn.Dead);
                going.AddRange(stillHere.Take(row.Transferable.CountToTransfer).Select(pawn => (pawn, row.Price)));
            }
            if (going.Count == 0) return;

            // Remember each one's map (despawning clears it), take them off, then convert them for sending, the same way
            // the trade window does. ConvertThingForSending sends a colonist's gear as full copies (art, mod data,
            // biocoding to them), which the plain conversion would lose
            List<Thing> things = going.Select(entry => (Thing) entry.pawn).ToList();
            List<Map> maps = going.Select(entry => entry.pawn.Map).ToList();
            foreach (Thing thing in things) thing.DeSpawn();
            List<ShopStock> stock = going.Select(entry => new ShopStock
            {
                Item = TradingThingConverter.ConvertThingForSending(entry.pawn),
                PricePerUnit = entry.price
            }).ToList();

            string token = ItemEscrow.Hold(things, maps, Client.Instance.BoardEscrowModule);
            if (!Client.Instance.StockShop(token, stock))
            {
                // The request couldn't even go out (we went offline), so put them straight back
                ItemEscrow.Resolve(token, false);
            }
        }

        /// <summary>
        /// Takes the picked items off the map and sends them to the server with their prices.
        /// </summary>
        /// <returns>Whether to carry on (false if the items couldn't be taken, and the window should stay open)</returns>
        private bool stockItems(List<Row> itemRows)
        {
            List<TransferableOneWay> transferables = itemRows.Select(row => row.Transferable).ToList();
            if (!ColonyTransferables.TryTakeItems(transferables, Client.Instance.BoardEscrowModule, out string token, out List<List<ProtoThing>> itemsPerRow))
            {
                // Something changed since the window opened (e.g. a pawn used some of it). Nothing was taken, so list
                // what's there now
                Messages.Message("Phinix_shop_notEnough".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                refreshRows();
                return false;
            }

            // Each row's items at that row's price. Things that stack come as one item per row; things that don't,
            // like dusters, one each
            List<ShopStock> stock = new List<ShopStock>();
            for (int i = 0; i < itemRows.Count; i++)
            {
                int price = itemRows[i].Price;
                stock.AddRange(itemsPerRow[i].Select(item => new ShopStock { Item = item, PricePerUnit = price }));
            }

            if (!Client.Instance.StockShop(token, stock))
            {
                // The request couldn't even go out (we went offline), so put everything straight back
                ItemEscrow.Resolve(token, false);
                return false;
            }

            // Otherwise the server answers with the same token, and Client.cs hands it to ItemEscrow.Resolve
            return true;
        }
    }
}
