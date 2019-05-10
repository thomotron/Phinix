using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PhinixClient.GUI;
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

        private const float OFFER_WINDOW_WIDTH = 400f;
        private const float OFFER_WINDOW_TITLE_HEIGHT = 20f;
        private const float OFFER_WINDOW_ROW_HEIGHT = 30f;

        private const float SORT_HEIGHT = 30f;

        private const float SEARCH_TEXT_FIELD_WIDTH = 135f;

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
        /// Collection of stacked items we have available and on offer.
        /// </summary>
        private List<StackedThings> itemStacks;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>itemStacks</c>.
        /// </summary>
        private object itemStacksLock = new object();
        /// <summary>
        /// Collection of stacked items we have sent to the server and are awaiting confirmation for.
        /// Depending on the server's response, these will be spawned or removed.
        /// </summary>
        private Dictionary<string, PendingThings> pendingItemStacks;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>pendingItemStacks</c>.
        /// </summary>
        private object pendingItemStacksLock = new object();

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
        /// Collection of stacked items in our offer.
        /// This is updated only as the trade is to avoid chewing up CPU time each frame.
        /// </summary>
        private List<StackedThings> ourOfferCache;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>ourOfferCache</c>.
        /// </summary>
        private object ourOfferCacheLock = new object();
        /// <summary>
        /// Collection of stacked items in the other party's offer.
        /// This is updated only as the trade is to avoid chewing up CPU time each frame.
        /// </summary>
        private List<StackedThings> theirOfferCache;
        /// <summary>
        /// Lock object to prevent race conditions when accessing <c>theirOfferCache</c>.
        /// </summary>
        private object theirOfferCacheLock = new object();

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
            this.forcePause = true;
            this.itemStacks = new List<StackedThings>();
            this.pendingItemStacks = new Dictionary<string, PendingThings>();
            this.ourOfferCache = new List<StackedThings>();
            this.theirOfferCache = new List<StackedThings>();

            Instance.OnTradeCompleted += OnTradeCompleted;
            Instance.OnTradeCancelled += OnTradeCancelled;
            Instance.OnTradeUpdateSuccess += OnTradeUpdated;
            Instance.OnTradeUpdateFailure += OnTradeUpdated;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            
            // Select all maps that are player homes
            IEnumerable<Map> homeMaps = Find.Maps.Where(map => map.IsPlayerHome);
            
            // From each map, select all zones that are stockpiles
            IEnumerable<Zone> stockpiles = homeMaps.SelectMany(map => map.zoneManager.AllZones.Where(zone => zone is Zone_Stockpile));
            
            // From each stockpile, select all things that are an item
            IEnumerable<Thing> stockpileItems = stockpiles.SelectMany(zone => zone.AllContainedThings.Where(thing => thing.def.category == ThingCategory.Item && !thing.def.IsCorpse));

            // Group all items and cache them for later
            this.itemStacks = StackedThings.GroupThings(stockpileItems);
            
            // Update both our and their offers
            UpdateOffers();
        }

        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);
            
            Instance.OnTradeCompleted -= OnTradeCompleted;
            Instance.OnTradeCancelled -= OnTradeCancelled;
            Instance.OnTradeUpdateSuccess -= OnTradeUpdated;
            Instance.OnTradeUpdateFailure -= OnTradeUpdated;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Get the other party's display name
            string displayName = GetOtherPartyDisplayName();
            
            // Title
            new TextWidget(
                text: "Phinix_trade_tradeTitle".Translate(TextHelper.StripRichText(displayName)),
                font: GameFont.Medium,
                anchor: TextAnchor.MiddleCenter
            ).Draw(inRect.TopPartPixels(TITLE_HEIGHT));
            
            // Offers
            GenerateOffers().Draw(inRect.BottomPartPixels(inRect.height - TITLE_HEIGHT).TopHalf());
            
            // Available items
            GenerateAvailableItems().Draw(inRect.BottomPartPixels(inRect.height - TITLE_HEIGHT).BottomHalf());
        }
        
        /// <summary>
        /// Event handler for the <c>OnTradeCancelled</c> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeCancelled(object sender, CompleteTradeEventArgs args)
        {
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
            // Delete our selected items
            foreach (StackedThings itemStack in itemStacks)
            {
                itemStack.DeleteSelected();
            }
            
            // Close the window
            Close();
        }

        /// <summary>
        /// Event handler for the <c>OnTradeUpdated</c> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnTradeUpdated(object sender, TradeUpdateEventArgs args)
        {
            // Ignore updates for trades other than this one
            if (args.TradeId != tradeId) return;
            
            // Update both our and their offers
            UpdateOffers();
            
            // Check if there is a token we can process
            if (!string.IsNullOrEmpty(args.Token))
            {
                lock (pendingItemStacksLock)
                {
                    // Check if there are pending item stacks for this token
                    if (pendingItemStacks.ContainsKey(args.Token))
                    {
                        if (args.Success)
                        {
                            // Destroy and remove the pending items, they have been received by the server
                            foreach (Thing thing in pendingItemStacks[args.Token].Things)
                            {
                                thing.Destroy();
                            }
                            
                            pendingItemStacks.Remove(args.Token);
                        }
                        else
                        {
                            // Server failed to update the trade
                            // Get all of the selected things from the item stacks and drop them
                            IEnumerable<Thing> things = pendingItemStacks[args.Token].Things;
                            Instance.DropPods(things);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates <c>ourOfferCache</c> and <c>theirOfferCache</c> with the items on offer for each party respectively.
        /// </summary>
        private void UpdateOffers()
        {
            // Try get our items on offer
            if (Instance.TryGetItemsOnOffer(tradeId, Instance.Uuid, out IEnumerable<ProtoThing> ourItems))
            {
                // Convert our items to their Verse equivalents
                Verse.Thing[] verseItems = ourItems.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown).ToArray();

                lock (ourOfferCacheLock)
                {
                    // Update our cached offer
                    ourOfferCache = StackedThings.GroupThings(verseItems);
                }
            }
            else
            {
                lock (ourOfferCacheLock)
                {
                    // Just clear our cached offer
                    ourOfferCache.Clear();
                }
            }
            
            // Try get their UUID and items on offer
            if (Instance.TryGetOtherPartyUuid(tradeId, out string otherPartyUuid) &&
                Instance.TryGetItemsOnOffer(tradeId, otherPartyUuid, out IEnumerable<ProtoThing> theirItems))
            {
                // Convert their items to their Verse equivalents
                Verse.Thing[] verseItems = theirItems.Select(TradingThingConverter.ConvertThingFromProtoOrUnknown).ToArray();

                lock (theirOfferCacheLock)
                {
                    // Update their cached offer
                    theirOfferCache = StackedThings.GroupThings(verseItems);
                }
            }
            else
            {
                lock (theirOfferCacheLock)
                {
                    // Just clear their cached offer
                    theirOfferCache.Clear();
                }
            }
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
        /// Generates a <c>VerticalFlexContainer</c> with the offer windows and confirmation statuses.
        /// </summary>
        /// <returns><c>VerticalFlexContainer</c> with the offer windows and confirmation statuses</returns>
        private VerticalFlexContainer GenerateOffers()
        {
            // Create a new flex container as the main column to store everything in
            VerticalFlexContainer theAllEncompassingColumnOfOmnipotence = new VerticalFlexContainer(DEFAULT_SPACING);
            
            // Create a new flex container as our 'row' to store the offers and the centre column in
            HorizontalFlexContainer offerRow = new HorizontalFlexContainer(DEFAULT_SPACING);
            
            // Our offer
            offerRow.Add(
                new Container(
                    GenerateOurOffer(),
                    width: OFFER_WINDOW_WIDTH
                )
            );
            
            // Create a new flex container as a 'column' to store the trade arrows and buttons in
            VerticalFlexContainer centreColumn = new VerticalFlexContainer(DEFAULT_SPACING);
            
            // Arrows
            centreColumn.Add(
                new FittedTextureWidget(
                    texture: ContentFinder<Texture2D>.Get("tradeArrows")
                )
            );
            
            // Update button
            centreColumn.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_updateButton".Translate(),
                        clickAction: () =>
                        {
                            // Do all of this in a new thread to keep the UI running smoothly
                            new Thread(() =>
                            {
                                try
                                {
                                    // Create a new token
                                    string token = Guid.NewGuid().ToString();

                                    List<Thing> selectedThings = new List<Thing>();
                                    lock (itemStacksLock)
                                    {
                                        // Collect all our things and despawn them all
                                        foreach (StackedThings itemStack in itemStacks)
                                        {
                                            // Pop the selected things from the stack
                                            Thing[] things = itemStack.PopSelected().ToArray();

                                            // Despawn each spawned thing
                                            foreach (Thing thing in things.Where(t => t.Spawned))
                                            {
                                                thing.DeSpawn();
                                            }

                                            // Add them to the selected things list
                                            selectedThings.AddRange(things);
                                        }
                                    }

                                    lock (pendingItemStacksLock)
                                    {
                                        // Add the items to the pending dictionary
                                        pendingItemStacks.Add(token, new PendingThings
                                        {
                                            Things = selectedThings.ToArray(),
                                            Timestamp = DateTime.UtcNow
                                        });
                                        Log.Message("Added items to pending");
                                    }

                                    // Get the items we have on offer and splice in the selected items
                                    Instance.TryGetItemsOnOffer(tradeId, Instance.Uuid, out IEnumerable<ProtoThing> itemsOnOffer);
                                    IEnumerable<ProtoThing> actualOffer = itemsOnOffer.Union(selectedThings.Select(TradingThingConverter.ConvertThingFromVerse));

                                    // Send an update to the server
                                    Instance.UpdateTradeItems(tradeId, actualOffer, token);
                                    Log.Message("Sent update");
                                }
                                catch (Exception e)
                                {
                                    Log.Message(e.ToString());
                                }
                            }).Start();
                        }),
                    height: TRADE_BUTTON_HEIGHT
                )
            );
            
            // Reset button
            centreColumn.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_resetButton".Translate(),
                        clickAction: () =>
                        {
                            // Reset all selected counts to zero
                            foreach (StackedThings itemStack in itemStacks)
                            {
                                itemStack.Selected = 0;
                            }
                
                            // Update trade items
                            Instance.UpdateTradeItems(tradeId, new ProtoThing[0]);
                        }
                    ),
                    height: TRADE_BUTTON_HEIGHT
                )
            );
            
            // Cancel button
            centreColumn.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_cancelButton".Translate(),
                        clickAction: () => Instance.CancelTrade(tradeId)
                    ),
                    height: TRADE_BUTTON_HEIGHT
                )
            );
            
            // Add the centre column to the row
            offerRow.Add(centreColumn);
            
            // Their offer
            offerRow.Add(
                new Container(
                    GenerateTheirOffer(),
                    width: OFFER_WINDOW_WIDTH
                )
            );
            
            // Add the offer row to the main column
            theAllEncompassingColumnOfOmnipotence.Add(offerRow);
            
            // Create a new row to hold the confirmation checkboxes in
            HorizontalFlexContainer offerAcceptanceRow = new HorizontalFlexContainer(DEFAULT_SPACING);
            
            // Check if the backend has updated before we let the user change their offer checkbox
            if (Instance.TryGetPartyAccepted(tradeId, Instance.Uuid, out bool accepted) && tradeAccepted != accepted)
            {
                // Update the GUI's status to match the backend
                tradeAccepted = accepted;
            }
            
            // Our confirmation
            // TODO: Ellipsise display name length if it's going to spill over
            offerAcceptanceRow.Add(
                new Container(
                    new CheckboxLabeledWidget(
                        label: ("Phinix_trade_confirmOurTradeCheckbox" + (tradeAccepted ? "Checked" : "Unchecked")).Translate(), // Janky-looking easter egg, just for you
                        isChecked: tradeAccepted,
                        onChange: (newCheckState) =>
                        {
                            tradeAccepted = newCheckState;
                            Instance.UpdateTradeStatus(tradeId, tradeAccepted);
                        }
                    ),
                    width: OFFER_WINDOW_WIDTH
                )
            );
            
            // Spacer
            offerAcceptanceRow.Add(
                new SpacerWidget()
            );
            
            // Their confirmation
            // TODO: Ellipsise display name length if it's going to spill over
            Instance.TryGetOtherPartyAccepted(tradeId, out bool otherPartyAccepted);
            offerAcceptanceRow.Add(
                new Container(
                    new CheckboxLabeledWidget(
                        label: ("Phinix_trade_confirmTheirTradeCheckbox" + (otherPartyAccepted ? "Checked" : "Unchecked")).Translate(TextHelper.StripRichText(GetOtherPartyDisplayName())), // Jankier-looking easter egg, just for you
                        isChecked: otherPartyAccepted,
                        onChange: null
                    ),
                    width: OFFER_WINDOW_WIDTH
                )
            );
            
            // Add the offer acceptance row to the main column
            theAllEncompassingColumnOfOmnipotence.Add(
                new Container(
                    offerAcceptanceRow,
                    height: SORT_HEIGHT
                )
            );

            // Return the generated main column
            return theAllEncompassingColumnOfOmnipotence;
        }

        /// <summary>
        /// Generates a <c>VerticalFlexContainer</c> containing our offer.
        /// </summary>
        /// <returns><c>VerticalFlexContainer</c> containing our offer</returns>
        private VerticalFlexContainer GenerateOurOffer()
        {
            // Create a flex container as our 'column' to store elements in
            VerticalFlexContainer column = new VerticalFlexContainer(0f);
            
            // Title
            column.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_trade_ourOfferLabel".Translate(),
                        anchor: TextAnchor.MiddleCenter
                    ),
                    height: OFFER_WINDOW_TITLE_HEIGHT
                )
            );
            
            // Draw our items
            lock (ourOfferCacheLock)
            {
                column.Add(
                    GenerateItemList(
                        itemStacks: ourOfferCache,
                        scrollPos: ourOfferScrollPos,
                        scrollUpdate: newScrollPos => ourOfferScrollPos = newScrollPos
                    )
                );
            }

            // Return the generated flex container
            return column;
        }

        /// <summary>
        /// Generates a <c>VerticalFlexContainer</c> containing their offer.
        /// </summary>
        /// <returns><c>VerticalFlexContainer</c> containing their offer</returns>
        private VerticalFlexContainer GenerateTheirOffer()
        {
            // Create a flex container as our 'column' to store elements in
            VerticalFlexContainer column = new VerticalFlexContainer(0f);
            
            // Title
            column.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_trade_theirOfferLabel".Translate(),
                        anchor: TextAnchor.MiddleCenter
                    ),
                    height: OFFER_WINDOW_TITLE_HEIGHT
                )
            );
            
            // Draw their items
            lock (theirOfferCacheLock)
            {
                column.Add(
                    GenerateItemList(
                        itemStacks: theirOfferCache,
                        scrollPos: theirOfferScrollPos,
                        scrollUpdate: newScrollPos => theirOfferScrollPos = newScrollPos
                    )
                );
            }

            // Return the generated flex container
            return column;
        }
        
        /// <summary>
        /// Generates a <c>VerticalFlexContainer</c> containing our available items.
        /// </summary>
        private VerticalFlexContainer GenerateAvailableItems()
        {
//            // Set the text anchor
//            TextAnchor oldAnchor = Text.Anchor;
//            Text.Anchor = TextAnchor.MiddleCenter;
//            
//            // 'Sort by' label
//            Rect sortByLabelRect = new Rect(
//                x: container.xMin,
//                y: container.yMin,
//                width: Text.CalcSize("Phinix_trade_sortByLabel".Translate()).x,
//                height: SORT_HEIGHT
//            );
//            Widgets.Label(sortByLabelRect, "Phinix_trade_sortByLabel".Translate());
//            
//            // Reset the text anchor
//            Text.Anchor = oldAnchor;
//            
//            // First sorting preference
//            Rect firstSortButtonRect = new Rect(
//                x: sortByLabelRect.xMax + DEFAULT_SPACING,
//                y: container.yMin,
//                width: SORT_BUTTON_WIDTH,
//                height: SORT_HEIGHT
//            );
//            if (Widgets.ButtonText(firstSortButtonRect, "", active: false))
//            {
//                // TODO: Sorting
//            }
//            
//            // Second sorting preference
//            Rect secondSortButtonRect = new Rect(
//                x: firstSortButtonRect.xMax + DEFAULT_SPACING,
//                y: container.yMin,
//                width: SORT_BUTTON_WIDTH,
//                height: SORT_HEIGHT
//            );
//            if (Widgets.ButtonText(secondSortButtonRect, "", active: false))
//            {
//                // TODO: Sorting
//            }
            
            // Create a new flex container as our 'column' to store everything in
            VerticalFlexContainer column = new VerticalFlexContainer(DEFAULT_SPACING);
            
            // Create a new flex container as our 'row' to store the search bar in
            HorizontalFlexContainer searchRow = new HorizontalFlexContainer(DEFAULT_SPACING);
            
            // Spacer to push everything to the right
            searchRow.Add(
                new SpacerWidget()
            );
            
            // Search label
            searchRow.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_trade_searchLabel".Translate(),
                        anchor: TextAnchor.MiddleCenter
                    ),
                    width: Text.CalcSize("Phinix_trade_searchLabel".Translate()).x
                )
            );
            
            // Search text field
            searchRow.Add(
                new Container(
                    new TextFieldWidget(
                        text: search,
                        onChange: newSearch => search = newSearch
                    ),
                    width: SEARCH_TEXT_FIELD_WIDTH
                )
            );
            
            // Add the search row to the main column
            column.Add(
                new Container(
                    searchRow,
                    height: SORT_HEIGHT
                )
            );
            
            // Filter the item stacks list for only those containing the search text
            IEnumerable<StackedThings> filteredItemStacks = itemStacks.Where(itemStack =>
            {
                // Make sure the item stack has things in it
                if (itemStack.Things.Count == 0) return false;
                
                // Get the first thing from the item stack
                Thing firstThing = itemStack.Things.First();

                // Return whether the first thing's def label matches the search text
                return firstThing.def.label.ToLower().Contains(search.ToLower());
            });
            
            // Stockpile items list
            column.Add(
                GenerateItemList(filteredItemStacks, stockpileItemsScrollPos, newScrollPos => stockpileItemsScrollPos = newScrollPos, true)
            );
            
            // Return the generated flex container
            return column;
        }

        /// <summary>
        /// Generates a <c>ScrollContainer</c> containing an item list within the given container.
        /// </summary>
        /// <param name="itemStacks">Item stacks to draw in the list</param>
        /// <param name="scrollPos">List scroll position</param>
        /// <param name="scrollUpdate">Action invoked with the scroll position of the item list when it is drawn</param>
        /// <param name="interactive">Whether the item counts should be modifiable by the user</param>
        private ScrollContainer GenerateItemList(IEnumerable<StackedThings> itemStacks, Vector2 scrollPos, Action<Vector2> scrollUpdate, bool interactive = false)
        {
            // Create a new flex container as our 'column' to hold each element
            VerticalFlexContainer column = new VerticalFlexContainer(0f);
            
            // Set up a list to hold our item stack rows
            int iterations = 0;
            foreach (StackedThings itemStack in itemStacks)
            {
                // Create an ItemStackRow from this item
                ItemStackRow row = new ItemStackRow(
                    itemStack: itemStack,
                    interactive: interactive,
                    alternateBackground: iterations++ % 2 != 0, // Be careful of the positioning of ++ here, this should increment /after/ the operation
                    onSelectedChanged: _ =>                     // We don't need the value, so we can just assign it to _
                    {
                        //Client.Instance.UpdateTradeItems(tradeId, this.itemStacks.SelectMany(stack => stack.GetSelectedThingsAsProto()));
                    }
                );
                
                // Contain the row within a minimum-height container
                MinimumContainer container = new MinimumContainer(
                    row,
                    minHeight: OFFER_WINDOW_ROW_HEIGHT
                );
                
                // Add it to the row list
                column.Add(container);
            }
            
            // Return the flex container wrapped in a scroll container
            return new ScrollContainer(column, scrollPos, scrollUpdate);
        }
    }
}