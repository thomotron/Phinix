using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PhinixClient.GUI;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;
using static PhinixClient.GUI.GUIUtils;

namespace PhinixClient
{
    public class TradeWindow : Window
    {
        private const float SCROLLBAR_WIDTH = 16f;

        private const float DEFAULT_SPACING = 10f;

        private const float OFFER_WINDOW_WIDTH = 400f;
        private const float OFFER_WINDOW_TITLE_HEIGHT = 20f;
        private const float OFFER_WINDOW_ROW_HEIGHT = 30f;
        private const float OFFER_WINDOW_CHECKBOX_HEIGHT = 40f;

        private const float SORT_HEIGHT = 30f;

        private const float SEARCH_TEXT_FIELD_WIDTH = 135f;

        private const float TRADE_BUTTON_HEIGHT = 30f;

        private const float TITLE_HEIGHT = 30f;

        public override Vector2 InitialSize => new Vector2(1000f, 750f);

        private readonly Regex itemCountInputRegex = new Regex("\\d*");
        private readonly Texture2D tradeArrows = ContentFinder<Texture2D>.Get("tradeArrows");

        private Vector2 ourOfferScrollPos = Vector2.zero;
        private Vector2 theirOfferScrollPos = Vector2.zero;
        private Vector2 availableItemsScrollPos = Vector2.zero;

        private List<StackedThings> ourOfferCache = new List<StackedThings>();
        private List<StackedThings> theirOfferCache = new List<StackedThings>();

        /// <summary>
        /// The trade this window contains.
        /// Will be overwritten with <see cref="updatedTrade"/> by the UI thread if <see cref="tradeUpdated"/> is set.
        /// </summary>
        private ImmutableTrade trade;
        /// <summary>
        /// Updated copy of <see cref="trade"/>.
        /// </summary>
        private ImmutableTrade updatedTrade;
        /// <summary>
        /// Whether <see cref="updatedTrade"/> has been changed and should be copied into <see cref="trade"/> by the UI thread.
        /// </summary>
        private bool tradeUpdated = false;
        /// <summary>
        /// Lock object protecting <see cref="updatedTrade"/>.
        /// </summary>
        private object updatedTradeLock = new object();

        /// <summary>
        /// All items that can be added to the trade.
        /// </summary>
        private List<StackedThings> availableItems = new List<StackedThings>();

        /// <summary>
        /// Creates a new <see cref="TradeWindow"/> for the given trade ID.
        /// </summary>
        /// <param name="trade">Trade details</param>
        public TradeWindow(ImmutableTrade trade)
        {
            this.trade = trade;

            this.doCloseX = true;
            this.closeOnAccept = false;
            this.closeOnCancel = false;
            this.closeOnClickedOutside = false;
            this.forcePause = true;
            this.draggable = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // Subscribe to events
            Client.Instance.OnTradeCompleted += OnTradeFinished;
            Client.Instance.OnTradeCancelled += OnTradeFinished;
            Client.Instance.OnTradeUpdateSuccess += OnTradeUpdated;
            Client.Instance.OnTradeUpdateFailure += OnTradeUpdated;

            // Select things from all maps that are player homes
            IEnumerable<Map> homeMaps = Find.Maps.Where(map => map.IsPlayerHome);
            IEnumerable<Thing> things;
            if (Client.Instance.AllItemsTradable)
            {
                // Get *everything*
                things = homeMaps.SelectMany(map => map.listerThings.AllThings);
            }
            else
            {
                // From each map, select all haul destinations, then everything stored there
                IEnumerable<SlotGroup> haulDestinations = homeMaps.SelectMany(map => map.haulDestinationManager.AllGroups);
                things = haulDestinations.SelectMany(haulDestination => haulDestination.HeldThings);
            }

            // Group all items and cache them for later
            availableItems = StackedThings.GroupThings(things.Where(thing => thing.def.category == ThingCategory.Item && !thing.def.IsCorpse));

            // Pre-fill offer caches as well
            ourOfferCache = StackedThings.GroupThings(trade.ItemsOnOffer.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown));
            theirOfferCache = StackedThings.GroupThings(trade.OtherPartyItemsOnOffer.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown));
        }

        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);

            // Unsubscribe from events
            Client.Instance.OnTradeCompleted -= OnTradeFinished;
            Client.Instance.OnTradeCancelled -= OnTradeFinished;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Update trade if requested
            if (tradeUpdated)
            {
                if (Monitor.TryEnter(updatedTradeLock))
                {
                    // Copy the new trade details into place
                    trade = updatedTrade;

                    // Refresh trade caches
                    ourOfferCache = StackedThings.GroupThings(trade.ItemsOnOffer.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown));
                    theirOfferCache = StackedThings.GroupThings(trade.OtherPartyItemsOnOffer.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown));

                    // Reset the update flag
                    tradeUpdated = false;

                    Monitor.Exit(updatedTradeLock);
                }
            }

            // Build layout rects
            Rect titleRect = inRect.TopPartPixels(TITLE_HEIGHT);

            Rect offerHalfRect = new Rect(inRect.xMin, titleRect.yMax + DEFAULT_SPACING, inRect.width, (inRect.height - titleRect.height - DEFAULT_SPACING) / 2 - DEFAULT_SPACING / 2);
            Rect ourOfferRect = offerHalfRect.LeftPartPixels(OFFER_WINDOW_WIDTH);
            Rect theirOfferRect = offerHalfRect.RightPartPixels(OFFER_WINDOW_WIDTH);

            Rect centreColumnRect = new Rect(ourOfferRect.xMax + DEFAULT_SPACING, offerHalfRect.yMin, (theirOfferRect.xMin - DEFAULT_SPACING) - (ourOfferRect.xMax + DEFAULT_SPACING), offerHalfRect.height);
            Rect cancelButtonRect = centreColumnRect.BottomPartPixels(TRADE_BUTTON_HEIGHT);
            Rect resetButtonRect = new Rect(centreColumnRect.xMin, cancelButtonRect.yMin - (TRADE_BUTTON_HEIGHT + DEFAULT_SPACING * 2), centreColumnRect.width, TRADE_BUTTON_HEIGHT);
            Rect updateButtonRect = new Rect(centreColumnRect.xMin, resetButtonRect.yMin - (TRADE_BUTTON_HEIGHT + DEFAULT_SPACING), centreColumnRect.width, TRADE_BUTTON_HEIGHT);
            Rect tradeArrowsRect = centreColumnRect.TopPartPixels(centreColumnRect.height - (cancelButtonRect.yMax - updateButtonRect.yMin) - DEFAULT_SPACING);

            Rect availableItemsRect = new Rect(inRect.xMin, offerHalfRect.yMax + DEFAULT_SPACING, inRect.width, inRect.yMax - offerHalfRect.yMax - DEFAULT_SPACING);

            // Save the current text settings
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.LabelFit(titleRect, "Phinix_trade_tradeTitle".Translate(TextHelper.StripRichText(trade.OtherPartyDisplayName)));

            // Restore the text settings
            Text.Font = previousFont;
            Text.Anchor = previousAnchor;

            // Trade arrows
            Widgets.DrawTextureFitted(tradeArrowsRect, tradeArrows, 1f);

            // Our offer
            // TODO: Maybe render our own checkbox with loading state based on trade update token???
            bool ourOfferAccepted = trade.Accepted;
            drawOffer(inRect: ourOfferRect,
                title: "Phinix_trade_ourOfferLabel".Translate(),
                itemStacks: ourOfferCache,
                scrollPos: ref ourOfferScrollPos,
                accepted: ref ourOfferAccepted,
                acceptedLabel: ("Phinix_trade_confirmOurTradeCheckbox" + (trade.Accepted ? "Checked" : "Unchecked")).Translate()
            );
            if (ourOfferAccepted != trade.Accepted)
            {
                // Update our accepted state
                // TODO: Keep a handle on a single thread for this to prevent excessive calls
                new Thread(() => Client.Instance.UpdateTradeStatus(trade.TradeId, accepted: ourOfferAccepted)).Start();
            }

            // Their offer
            bool theirOfferAccepted = trade.OtherPartyAccepted;
            drawOffer(inRect: theirOfferRect,
                title: "Phinix_trade_theirOfferLabel".Translate(),
                itemStacks: theirOfferCache,
                scrollPos: ref theirOfferScrollPos,
                accepted: ref theirOfferAccepted,
                acceptedLabel: ("Phinix_trade_confirmTheirTradeCheckbox" + (trade.OtherPartyAccepted ? "Checked" : "Unchecked")).Translate(TextHelper.StripRichText(trade.OtherPartyDisplayName))
            );

            // Update button
            if (Widgets.ButtonText(updateButtonRect, "Phinix_trade_updateButton".Translate()))
            {
                // Split off all selected items
                IEnumerable<Thing> things = availableItems.SelectMany(stack => stack.PopSelected());

                // Send them up to the server
                Client.Instance.AddTradeItems(trade.TradeId, things, Guid.NewGuid().ToString());
            }

            // Reset button
            if (Widgets.ButtonText(resetButtonRect, "Phinix_trade_resetButton".Translate()))
            {
                // Reset the trade
                Client.Instance.ResetTradeItems(trade.TradeId);

                // Reset all selected counts to zero
                foreach (StackedThings stack in availableItems)
                {
                    stack.Selected = 0;
                }
            }

            // Save GUI colour
            Color previousColour = UnityEngine.GUI.color;

            // Cancel button
            UnityEngine.GUI.color = Color.red;
            if (Widgets.ButtonText(cancelButtonRect, "Phinix_trade_cancelButton".Translate()))
            {
                new Thread(() => Client.Instance.CancelTrade(trade.TradeId)).Start();
            }

            // Restore GUI colour
            UnityEngine.GUI.color = previousColour;

            // Available items
            drawItemStackList(availableItemsRect, availableItems, ref availableItemsScrollPos, true);
        }

        /// <summary>
        /// Event handler for the <see cref="Client.OnTradeCompleted"/> and <see cref="Client.OnTradeCancelled"/> events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeFinished(object sender, CompleteTradeEventArgs args)
        {
            Close();
        }

        /// <summary>
        /// Event handler for the <see cref="Client.OnTradeUpdateSuccess"/> and <see cref="Client.OnTradeUpdateFailure"/> events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeUpdated(object sender, UITradeUpdateEventArgs args)
        {
            // Save the updated trade and flag the current one to be replaced
            lock (updatedTradeLock)
            {
                updatedTrade = args.Trade;
                tradeUpdated = true;
            }
        }

        /// <summary>
        /// Draws an offer containing a title, list of items on offer, and toggle-able checkbox with whether it's been accepted.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        /// <param name="title">Title text</param>
        /// <param name="itemStacks">Item stacks on offer</param>
        /// <param name="scrollPos">Item list scroll position</param>
        /// <param name="accepted">Offer accepted state</param>
        /// <param name="acceptedLabel">Accepted state label</param>
        private void drawOffer(Rect inRect, string title, List<StackedThings> itemStacks, ref Vector2 scrollPos, ref bool accepted, string acceptedLabel)
        {
            Rect titleRect = inRect.TopPartPixels(OFFER_WINDOW_TITLE_HEIGHT);
            Rect acceptedStateRect = inRect.BottomPartPixels(OFFER_WINDOW_CHECKBOX_HEIGHT);
            Rect itemListRect = new Rect(inRect.xMin, titleRect.yMax, inRect.width, acceptedStateRect.yMin - DEFAULT_SPACING - titleRect.yMax);

            // Save the current text settings
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;

            // Title
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.LabelFit(titleRect, title);

            // Accepted state
            Widgets.CheckboxLabeled(acceptedStateRect, acceptedLabel, ref accepted);

            // Restore the text settings
            Text.Font = previousFont;
            Text.Anchor = previousAnchor;

            // Items on offer
            drawItemStackList(itemListRect, itemStacks, ref scrollPos, false);
        }

        /// <summary>
        /// Draws a list of item stacks.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        /// <param name="stacks">List of item stacks to draw</param>
        /// <param name="scrollPos">Scroll position</param>
        /// <param name="interactive">Whether to draw interactive buttons and quantity fields</param>
        private void drawItemStackList(Rect inRect, List<StackedThings> stacks, ref Vector2 scrollPos, bool interactive = false)
        {
            float ICON_WIDTH = 30f;
            float ROW_HEIGHT = ICON_WIDTH;
            float BUTTON_WIDTH = 40f;
            float QUANTITY_FIELD_WIDTH = 70f;
            float AVAILABLE_COUNT_WIDTH = 50f;
            float RIGHT_PADDING = 5f;

            // Set up the content rect and start scrolling
            bool scrollbarsPresent = ROW_HEIGHT * stacks.Count > inRect.height;
            int nonEmptyStacks = stacks.Count(stack => stack.Count != 0);
            Rect contentRect = new Rect(inRect.xMin, inRect.yMin, scrollbarsPresent ? inRect.width - SCROLLBAR_WIDTH : inRect.width, ROW_HEIGHT * nonEmptyStacks);
            bool scrollRequired = contentRect.height > inRect.height;
            if (scrollRequired) Widgets.BeginScrollView(inRect, ref scrollPos, contentRect);

            bool alternateBackground = false;
            float currentY = contentRect.yMin;
            foreach (StackedThings stack in stacks)
            {
                // Don't deal with empty stacks
                if (stack.Things.Count == 0) continue;

                Rect rowRect = new Rect(contentRect.xMin, currentY, contentRect.width, ROW_HEIGHT);
                Rect iconRect = rowRect.LeftPartPixels(ICON_WIDTH);

                // Background
                if (alternateBackground) Widgets.DrawHighlight(rowRect);

                // Icon
                Widgets.ThingIcon(iconRect, stack.ThingDef, stack.StuffDef, stack.StyleDef, 0.9f);

                Rect itemNameRect;
                if (interactive)
                {
                    float buttonAreaWidth = ((BUTTON_WIDTH * 6) + QUANTITY_FIELD_WIDTH + AVAILABLE_COUNT_WIDTH + (DEFAULT_SPACING * 3));
                    Rect buttonAreaRect = new Rect(rowRect.xMax - (RIGHT_PADDING + buttonAreaWidth), rowRect.yMin, buttonAreaWidth, rowRect.height);
                    Rect quantityButton1Rect = new Rect(buttonAreaRect.xMin, buttonAreaRect.yMin, BUTTON_WIDTH, buttonAreaRect.height);
                    Rect quantityButton2Rect = quantityButton1Rect.TranslatedBy(BUTTON_WIDTH);
                    Rect quantityButton3Rect = quantityButton2Rect.TranslatedBy(BUTTON_WIDTH);
                    Rect quantityFieldRect = new Rect(quantityButton3Rect.xMax + DEFAULT_SPACING, buttonAreaRect.yMin, QUANTITY_FIELD_WIDTH, buttonAreaRect.height);
                    Rect availableCountRect = new Rect(quantityFieldRect.xMax + DEFAULT_SPACING, buttonAreaRect.yMin, AVAILABLE_COUNT_WIDTH, buttonAreaRect.height);
                    Rect quantityButton4Rect = new Rect(availableCountRect.xMax + DEFAULT_SPACING, buttonAreaRect.yMin, BUTTON_WIDTH, buttonAreaRect.height);
                    Rect quantityButton5Rect = quantityButton4Rect.TranslatedBy(BUTTON_WIDTH);
                    Rect quantityButton6Rect = quantityButton5Rect.TranslatedBy(BUTTON_WIDTH);

                    itemNameRect = new Rect(iconRect.xMax + DEFAULT_SPACING, rowRect.yMin, buttonAreaRect.xMin - iconRect.xMax - (DEFAULT_SPACING * 2), rowRect.height);

                    // -100 button
                    if (Widgets.ButtonText(quantityButton1Rect, "-100")) stack.Selected = Clamp(stack.Selected - 100, 0, stack.Count);

                    // -10 button
                    if (Widgets.ButtonText(quantityButton2Rect, "-10")) stack.Selected = Clamp(stack.Selected - 10, 0, stack.Count);

                    // -1 button
                    if (Widgets.ButtonText(quantityButton3Rect, "-1")) stack.Selected = Clamp(stack.Selected - 1, 0, stack.Count);

                    // +1 button
                    if (Widgets.ButtonText(quantityButton4Rect, "+1")) stack.Selected = Clamp(stack.Selected + 1, 0, stack.Count);

                    // +10 button
                    if (Widgets.ButtonText(quantityButton5Rect, "+10")) stack.Selected = Clamp(stack.Selected + 10, 0, stack.Count);

                    // +100 button
                    if (Widgets.ButtonText(quantityButton6Rect, "+100")) stack.Selected = Clamp(stack.Selected + 100, 0, stack.Count);

                    // Quantity text field
                    string buf = stack.Selected == 0 ? "" : stack.Selected.ToString();
                    buf = Widgets.TextField(quantityFieldRect, buf, 100, itemCountInputRegex);
                    stack.Selected = string.IsNullOrEmpty(buf) ? 0 : Clamp(int.Parse(buf), 0, stack.Count);

                    // Available count
                    SaveTextFormat();
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(availableCountRect, $"/ {stack.Count.ToStringSI()}");
                    RestoreTextFormat();
                }
                else
                {
                    Rect itemCountRect = new Rect(rowRect.xMax - QUANTITY_FIELD_WIDTH - RIGHT_PADDING, rowRect.yMin, QUANTITY_FIELD_WIDTH, rowRect.height);
                    itemNameRect = new Rect(iconRect.xMax + DEFAULT_SPACING, rowRect.yMin, itemCountRect.xMin - iconRect.xMax - DEFAULT_SPACING, rowRect.height);

                    // Item count
                    SaveTextFormat();
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(itemCountRect, stack.Count.ToStringSI());
                    RestoreTextFormat();
                }

                // Item name
                SaveTextFormat();
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.LabelFit(itemNameRect, stack.Label);
                RestoreTextFormat();

                // Toggle alternate background colour
                alternateBackground = !alternateBackground;

                currentY += ROW_HEIGHT;
            }

            if (scrollRequired) Widgets.EndScrollView();
        }
    }
}
