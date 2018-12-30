using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;
using static PhinixClient.Client;

namespace PhinixClient
{
    public class TradeWindow : Window
    {
        public override Vector2 InitialSize => new Vector2(1000f, 750f);
        
        private const float DEFAULT_SPACING = 10f;
        private const float SCROLLBAR_WIDTH = 16f;
        private const float WINDOW_PADDING = 20f;

        private const float TRADE_ARROWS_WIDTH = 140f;
        private const float TRADE_ARROWS_HEIGHT = 190f;

        private const float OFFER_WINDOW_WIDTH = 400f;
        private const float OFFER_WINDOW_HEIGHT = 310f;
        private const float OFFER_WINDOW_TITLE_HEIGHT = 20f;
        private const float OFFER_WINDOW_ROW_HEIGHT = 30f;
        private const float OFFER_CONFIRMATION_HEIGHT = 20f;

        private const float HORIZONTAL_UPPER_LOWER_DIVISION_SPACING = 20f;

        private const float LOWER_HALF_WIDTH = 470f;
        private const float LOWER_HALF_HEIGHT = 310f;

        private const float SORT_HEIGHT = 30f;
        private const float SORT_LABEL_WIDTH = 60f;
        private const float SORT_BUTTON_WIDTH = 100f;

        private const float SEARCH_TEXT_FIELD_WIDTH = 135f;

        private const float TRADE_BUTTON_WIDTH = 140f;
        private const float TRADE_BUTTON_HEIGHT = 30f;

        private const float TITLE_HEIGHT = 30f;

        /// <summary>
        /// The ID of the trade this window is for.
        /// </summary>
        private string tradeId;

        /// <summary>
        /// Whether we accept the trade as it stands.
        /// </summary>
        private bool tradeAccepted = false;

        /// <summary>
        /// Collection of items we have on offer.
        /// Used to update our offer.
        /// </summary>
        private List<Thing> items;

        /// <summary>
        /// Search text for filtering available items.
        /// </summary>
        private string search = "";

        /// <summary>
        /// Scroll position of our offer window.
        /// </summary>
        private Vector2 ourOfferScrollPos = Vector2.zero;
        /// <summary>
        /// Scroll position of the other party's offer window.
        /// </summary>
        private Vector2 theirOfferScrollPos = Vector2.zero;
        /// <summary>
        /// Scroll position of  the available items window.
        /// </summary>
        private Vector2 stockpileItemsScrollPos = Vector2.zero;

        /// <summary>
        /// Creates a new <c>TradeWindow</c> for the given trade ID.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        public TradeWindow(string tradeId)
        {
            this.tradeId = tradeId;

            this.doCloseX = true;
            this.closeOnAccept = false;
            this.closeOnCancel = false;
            this.closeOnClickedOutside = false;
            this.items = new List<Thing>();

            Instance.OnTradeCompleted += OnTradeCompleted;
            Instance.OnTradeCancelled += OnTradeCancelled;
        }

        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);
            
            Instance.OnTradeCompleted -= OnTradeCompleted;
            Instance.OnTradeCancelled -= OnTradeCancelled;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Rect titleRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: TITLE_HEIGHT
            );
            DrawTitle(titleRect);
            
            // Offers
            Rect offersRect = new Rect(
                x: inRect.xMin,
                y: titleRect.yMax,
                width: inRect.width,
                height: OFFER_WINDOW_HEIGHT + DEFAULT_SPACING + OFFER_CONFIRMATION_HEIGHT
            );
            DrawOffers(offersRect);
            
            // Available items
            Rect availableItemsRect = new Rect(
                x: inRect.xMin,
                y: offersRect.yMax + HORIZONTAL_UPPER_LOWER_DIVISION_SPACING,
                width: inRect.width,
                height: LOWER_HALF_HEIGHT
            );
            DrawAvailableItems(availableItemsRect);
        }
        
        /// <summary>
        /// Event handler for the <c>OnTradeCancelled</c> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeCancelled(object sender, CompleteTradeEventArgs args)
        {
            // Try get the other party's display name
            if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
            {
                // Strip formatting
                displayName = TextHelper.StripRichText(displayName);
            }
            else
            {
                // Unknown display name, default to ???
                displayName = "???";
            }

            // Generate a letter
            LetterDef letterDef = new LetterDef {color = Color.yellow};
            Find.LetterStack.ReceiveLetter("Trade cancelled", string.Format("The trade with {0} was cancelled", displayName), letterDef);
            
            // Convert all the received items into their Verse counterparts
            Verse.Thing[] verseItems = args.Items.Select(TradingThingConverter.ConvertThingFromProto).ToArray();

            // Launch drop pods to a trade spot on a home tile
            Map map = Find.AnyPlayerHomeMap;
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, verseItems);
            
            // Close the window
            Close();
        }

        /// <summary>
        /// Event handler for the <c>OnTradeCompleted</c> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeCompleted(object sender, CompleteTradeEventArgs args)
        {
            // Try get the other party's display name
            if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
            {
                // Strip formatting
                displayName = TextHelper.StripRichText(displayName);
            }
            else
            {
                // Unknown display name, default to ???
                displayName = "???";
            }
            
            // Generate a letter
            LetterDef letterDef = new LetterDef {color = Color.cyan};
            Find.LetterStack.ReceiveLetter("Trade success", string.Format("The trade with {0} was successful", displayName), letterDef);

            // Convert all the received items into their Verse counterparts
            Verse.Thing[] verseItems = args.Items.Select(TradingThingConverter.ConvertThingFromProto).ToArray();

            // Launch drop pods to a trade spot on a home tile
            Map map = Find.AnyPlayerHomeMap;
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(dropSpot, map, verseItems);
            
            // Close the window
            Close();
        }

        /// <summary>
        /// Draws the title of the trade window within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawTitle(Rect container)
        {
            // Get the other party's display name
            string displayName = GetOtherPartyDisplayName();
            
            // Set the text style
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            
            // Draw the title
            Widgets.Label(container, "Trade with " + TextHelper.StripRichText(displayName));
            
            // Reset the text style
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        /// <summary>
        /// Gets the display name of the other party of this trade.
        /// Defaults to '???' if any part of the process fails.
        /// </summary>
        /// <returns>Other party's display name</returns>
        private string GetOtherPartyDisplayName()
        {
            // Try to get the other party's UUID and display name
            string displayName;
            if (!Instance.TryGetOtherPartyUuid(tradeId, out string otherPartyUuid))
            {
                displayName = "???";
            }
            if (!Instance.TryGetDisplayName(otherPartyUuid, out displayName))
            {
                displayName = "???";
            }

            return displayName;
        }
        
        /// <summary>
        /// Draws the offer windows and confirmation statuses within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawOffers(Rect container)
        {
            // Our offer
            Rect ourOfferRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: OFFER_WINDOW_WIDTH,
                height: OFFER_WINDOW_HEIGHT
            );
            DrawOurOffer(ourOfferRect);
            
            // Their offer
            Rect theirOfferRect = new Rect(
                x: container.xMax - OFFER_WINDOW_WIDTH,
                y: container.yMin,
                width: OFFER_WINDOW_WIDTH,
                height: OFFER_WINDOW_HEIGHT
            );
            DrawTheirOffer(theirOfferRect);
            
            // Arrows
            Rect tradeArrowsRect = new Rect(
                x: ourOfferRect.xMax + DEFAULT_SPACING,
                y: container.yMin,
                width: TRADE_ARROWS_WIDTH,
                height: TRADE_ARROWS_HEIGHT
            );
            Texture arrowsTexture = ContentFinder<Texture2D>.Get("tradeArrows");
            Widgets.DrawTextureFitted(tradeArrowsRect, arrowsTexture, 1f);
            
            // Update button
            Rect updateButtonRect = new Rect(
                x: tradeArrowsRect.xMin,
                y: tradeArrowsRect.yMax + DEFAULT_SPACING,
                width: TRADE_BUTTON_WIDTH,
                height: TRADE_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(updateButtonRect, "Phinix_trade_updateButton".Translate()))
            {
                // Convert and update trade items
                Instance.UpdateTradeItems(tradeId, items.Select(TradingThingConverter.ConvertThingFromVerse));
            }
            
            // Reset button
            Rect resetButtonRect = new Rect(
                x: tradeArrowsRect.xMin,
                y: updateButtonRect.yMax + DEFAULT_SPACING,
                width: TRADE_BUTTON_WIDTH,
                height: TRADE_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(resetButtonRect, "Phinix_trade_resetButton".Translate()))
            {
                // Clear and update trade items
                items.Clear();
                Instance.UpdateTradeItems(tradeId, new ProtoThing[0]);
            }
            
            // Cancel button
            Rect cancelButtonRect = new Rect(
                x: tradeArrowsRect.xMin,
                y: resetButtonRect.yMax + DEFAULT_SPACING,
                width: TRADE_BUTTON_WIDTH,
                height: TRADE_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(cancelButtonRect, "Phinix_trade_cancelButton".Translate()))
            {
                Instance.CancelTrade(tradeId);
            }
            
            // Our confirmation
            // TODO: Ellipsise display name length if it's going to spill over
            Rect ourConfirmationRect = new Rect(
                x: ourOfferRect.xMin,
                y: ourOfferRect.yMax + DEFAULT_SPACING,
                width: OFFER_WINDOW_WIDTH,
                height: OFFER_CONFIRMATION_HEIGHT
            );
            bool oldTradeAccepted = tradeAccepted;
            Widgets.CheckboxLabeled(
                rect: ourConfirmationRect,
                label: ("Phinix_trade_confirmOurTradeCheckbox" + (tradeAccepted ? "Checked" : "Unchecked")).Translate(), // Janky-looking easter egg, just for you
                checkOn: ref tradeAccepted
            );
            // Check if the accepted state has changed
            if (tradeAccepted != oldTradeAccepted)
            {
                // Update our trade status
                Instance.UpdateTradeStatus(tradeId, tradeAccepted);
            }
            // Check if the backend has updated
            else if (Instance.TryGetPartyAccepted(tradeId, Instance.Uuid, out bool accepted) && tradeAccepted != accepted)
            {
                // Update the GUI's status to match the backend
                tradeAccepted = accepted;
            }
            
            // Their confirmation
            // TODO: Ellipsise display name length if it's going to spill over
            Rect theirConfirmationRect = new Rect(
                x: theirOfferRect.xMin,
                y: theirOfferRect.yMax + DEFAULT_SPACING,
                width: OFFER_WINDOW_WIDTH,
                height: OFFER_CONFIRMATION_HEIGHT
            );
            Instance.TryGetOtherPartyAccepted(tradeId, out bool otherPartyAccepted);
            Widgets.CheckboxLabeled(
                rect: theirConfirmationRect,
                label: ("Phinix_trade_confirmTheirTradeCheckbox" + (otherPartyAccepted ? "Checked" : "Unchecked")).Translate(TextHelper.StripRichText(GetOtherPartyDisplayName())), // Jankier-looking easter egg, just for you
                checkOn: ref otherPartyAccepted
            );
        }

        /// <summary>
        /// Draws our offer within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawOurOffer(Rect container)
        {
            // Title
            Rect titleRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width,
                height: OFFER_WINDOW_TITLE_HEIGHT
            );
            
            // Set the text style
            Text.Anchor = TextAnchor.MiddleCenter;
            
            // Draw the title
            Widgets.Label(titleRect, "Our Offer");
            
            // Reset the text style
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Try get our items on offer
            if (Instance.TryGetItemsOnOffer(tradeId, Instance.Uuid, out IEnumerable<ProtoThing> items))
            {
                // Convert our items to their Verse equivalents
                Verse.Thing[] verseItems = items.Select(TradingThingConverter.ConvertThingFromProto).ToArray();
                
                // Draw our items
                DrawItemList(container.BottomPartPixels(container.height - titleRect.height), verseItems, ref ourOfferScrollPos);
            }
            else
            {
                // Couldn't get our items, draw a blank array
                DrawItemList(container.BottomPartPixels(container.height - titleRect.height), new Verse.Thing[0], ref ourOfferScrollPos);
            }
        }

        /// <summary>
        /// Draws their offer within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawTheirOffer(Rect container)
        {
            // Title
            Rect titleRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width,
                height: OFFER_WINDOW_TITLE_HEIGHT
            );
            
            // Set the text style
            Text.Anchor = TextAnchor.MiddleCenter;
            
            // Draw the title
            Widgets.Label(titleRect, "Their Offer");
            
            // Reset the text style
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Try get their UUID and items on offer
            if (Instance.TryGetOtherPartyUuid(tradeId, out string otherPartyUuid) &&
                Instance.TryGetItemsOnOffer(tradeId, otherPartyUuid, out IEnumerable<ProtoThing> items))
            {
                // Convert their items to their Verse equivalents
                Verse.Thing[] verseItems = items.Select(TradingThingConverter.ConvertThingFromProto).ToArray();
                
                // Draw their items
                DrawItemList(container.BottomPartPixels(container.height - titleRect.height), verseItems, ref theirOfferScrollPos);
            }
            else
            {
                // Couldn't get their items, draw a blank array
                DrawItemList(container.BottomPartPixels(container.height - titleRect.height), new Thing[0], ref theirOfferScrollPos);
            }
        }
        
        /// <summary>
        /// Draws our available items within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawAvailableItems(Rect container)
        {
            // Set the text anchor
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            // 'Sort by' label
            Rect sortByLabelRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: Text.CalcSize("Phinix_trade_sortByLabel".Translate()).x,
                height: SORT_HEIGHT
            );
            Widgets.Label(sortByLabelRect, "Phinix_trade_sortByLabel".Translate());
            
            // Reset the text anchor
            Text.Anchor = oldAnchor;
            
            // First sorting preference
            Rect firstSortButtonRect = new Rect(
                x: sortByLabelRect.xMax + DEFAULT_SPACING,
                y: container.yMin,
                width: SORT_BUTTON_WIDTH,
                height: SORT_HEIGHT
            );
            if (Widgets.ButtonText(firstSortButtonRect, "", active: false))
            {
                // TODO: Sorting
            }
            
            // Second sorting preference
            Rect secondSortButtonRect = new Rect(
                x: firstSortButtonRect.xMax + DEFAULT_SPACING,
                y: container.yMin,
                width: SORT_BUTTON_WIDTH,
                height: SORT_HEIGHT
            );
            if (Widgets.ButtonText(secondSortButtonRect, "", active: false))
            {
                // TODO: Sorting
            }
            
            // Search text field
            Rect searchTextRect = new Rect(
                x: container.xMax - SEARCH_TEXT_FIELD_WIDTH,
                y: container.yMin,
                width: SEARCH_TEXT_FIELD_WIDTH,
                height: SORT_HEIGHT
            );
            search = Widgets.TextField(searchTextRect, search);
            
            // Set the text anchor
            oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            // Search label
            Rect searchLabel = new Rect(
                x: searchTextRect.xMin - (Text.CalcSize("Phinix_trade_searchLabel".Translate()).x + DEFAULT_SPACING),
                y: container.yMin,
                width: Text.CalcSize("Phinix_trade_searchLabel".Translate()).x,
                height: SORT_HEIGHT
            );
            Widgets.Label(searchLabel, "Phinix_trade_searchLabel".Translate());
            
            // Reset the text anchor
            Text.Anchor = oldAnchor;
            
            // Stockpile items list
            Rect availableItemsListRect = new Rect(
                x: container.xMin,
                y: container.yMin + SORT_HEIGHT + DEFAULT_SPACING,
                width: container.width,
                height: container.height - (SORT_HEIGHT + DEFAULT_SPACING)
            );
            IEnumerable<Map> homeMaps = Find.Maps.Where(map => map.IsPlayerHome); // Select all maps that are player homes
            IEnumerable<Zone> stockpiles = homeMaps.SelectMany(map => map.zoneManager.AllZones.Where(zone => zone is Zone_Stockpile)); // From each map, select all zones that are stockpiles
            IEnumerable<Thing> stockpileItems = stockpiles.SelectMany(zone => zone.AllContainedThings.Where(thing => thing.def.category == ThingCategory.Item)); // From each stockpile, select all things that are an item
            IEnumerable<Thing> filteredStockpileItems = stockpileItems.Where(thing => thing.def.label.ToLower().Contains(search.ToLower())); // Select all items that have names containing the search text
            DrawItemList(availableItemsListRect, filteredStockpileItems, ref stockpileItemsScrollPos);
        }
        
        /// <summary>
        /// Draws an item list within the given container.
        /// Used to draw the offer windows.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        /// <param name="items">Items to draw in the list</param>
        /// <param name="scrollPos">List scroll position</param>
        private void DrawItemList(Rect container, IEnumerable<Verse.Thing> items, ref Vector2 scrollPos)
        {
            // Group each item for the list
            Dictionary<string, List<ThingStack>> groupedItems = new Dictionary<string, List<ThingStack>>();
            foreach (Thing item in items)
            {
                // Check if this item type already has a group
                if (groupedItems.ContainsKey(item.def.defName))
                {
                    // Loop over all the item stacks in the group
                    bool stacked = false;
                    foreach (ThingStack itemStack in groupedItems[item.def.defName])
                    {
                        // Check if this item can stack on this stack
                        if (item.CanStackWith(itemStack.Thing))
                        {
                            // Increment this stack's item count by the item's stack count and break the loop
                            itemStack.Count += item.stackCount;
                            stacked = true;
                            break;
                        }
                    }

                    // Check if a stack wasn't found within this group
                    if (!stacked)
                    {
                        // Add a new stack with this item in it
                        groupedItems[item.def.defName].Add(new ThingStack(item, 1));
                    }
                }
                else
                {
                    // Create a new item stack with this item in it
                    ThingStack itemStack = new ThingStack(item, 1);
                    
                    // Add a new group with the item stack
                    groupedItems.Add(item.def.defName, new List<ThingStack>() {itemStack});
                }
            }
            
            // Set up a list to hold our item rows
            List<ItemRow> rows = new List<ItemRow>();
            int iterations = 0;
            foreach (List<ThingStack> itemStacks in groupedItems.Values)
            {
                foreach (ThingStack itemStack in itemStacks)
                {
                    // Create an ItemRow from this item
                    rows.Add(new ItemRow(itemStack.Thing, itemStack.Count, OFFER_WINDOW_ROW_HEIGHT, iterations++ % 2 != 0));
                }
            }
            
            // Create a flex container with our rows
            VerticalFlexContainer flexContainer = new VerticalFlexContainer(container.width - SCROLLBAR_WIDTH, rows.Cast<IDrawable>());

            // Determine if scrolling is necessary
            if (flexContainer.GetHeight(container.width) > container.height)
            {
                // Create an inner 'scrollable' container
                Rect innerContainer = new Rect(
                    x: container.xMin,
                    y: container.yMin,
                    width: container.width - SCROLLBAR_WIDTH,
                    height: flexContainer.GetHeight(container.width - SCROLLBAR_WIDTH)
                );
                
                // Draw a box to contain the list
                Widgets.DrawMenuSection(innerContainer);
                
                // Start scrolling
                Widgets.BeginScrollView(container, ref scrollPos, innerContainer);
            
                // Draw the flex container
                flexContainer.Draw(innerContainer);
            
                // Stop scrolling
                Widgets.EndScrollView();
            }
            else
            {
                // Draw a box to contain the list
                Widgets.DrawMenuSection(container);
                
                // Draw the flex container directly to the container
                flexContainer.Draw(container);
            }
        }
    }

    public class ThingStack
    {
        public Thing Thing;

        public int Count;

        public ThingStack(Thing thing, int count)
        {
            this.Thing = thing;
            this.Count = count;
        }
    }
}