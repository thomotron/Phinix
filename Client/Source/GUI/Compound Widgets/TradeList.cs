using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Trading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient.GUI.Compound_Widgets
{
    public class TradeList : Displayable
    {
        /// <inheritdoc cref="Displayable.IsFluidHeight"/>
        public override bool IsFluidHeight => constructedLayout.IsFluidHeight;
        /// <inheritdoc cref="Displayable.IsFluidWidth"/>
        public override bool IsFluidWidth => constructedLayout.IsFluidWidth;

        /// <summary>
        /// Collection containing the current active trade rows.
        /// </summary>
        private readonly List<TradeRow> tradeRows;
        /// <summary>
        /// Whether <see cref="tradeRows"/> has been updated since the last time it was drawn.
        /// </summary>
        /// <remarks>
        /// This is essentially the base data for the trade rows. Any additions to or deletions from
        /// <see cref="tradeRows"/> should set this flag to true so that they can be copied into
        /// <see cref="tradeRowsFlexContainer"/> on the next call to <see cref="Draw"/>.
        ///
        /// This flag does not need to be raised for changes to existing objects.
        /// </remarks>
        private bool tradesUpdated = false;
        /// <summary>
        /// Lock object to prevent a race condition when accessing <see cref="tradeRows"/> or <see cref="tradesUpdated"/>.
        /// </summary>
        private readonly object tradeRowsLock = new object();

        /// <summary>
        /// The constructed trade list.
        /// </summary>
        private readonly ConditionalContainer constructedLayout;

        /// <summary>
        /// All current <see cref="tradeRows"/> ready to be drawn wrapped in a <see cref="VerticalFlexContainer"/>.
        /// Updates made to the trade list should be reflected here.
        /// </summary>
        private readonly VerticalFlexContainer tradeRowsFlexContainer;

        /// <summary>
        /// Creates a new <see cref="TradeList"/>.
        /// </summary>
        public TradeList()
        {
            this.tradeRows = new List<TradeRow>();

            // Create the initial layout
            tradeRowsFlexContainer = new VerticalFlexContainer();

            // Wrap the column in a scroll container
            VerticalScrollContainer scrolledColumn = new VerticalScrollContainer(tradeRowsFlexContainer);

            // Make sure we have active trades before attempting to draw them
            ConditionalContainer activeTradesConditional = new ConditionalContainer(
                childIfTrue: scrolledColumn,
                childIfFalse: new PlaceholderWidget("Phinix_trade_noActiveTradesPlaceholder".Translate()),
                condition: () => tradeRowsFlexContainer.Contents.Any()
            );

            // Make sure we are online above all else
            ConditionalContainer onlineConditional = new ConditionalContainer(
                childIfTrue: activeTradesConditional,
                childIfFalse: new PlaceholderWidget("Phinix_chat_pleaseLogInPlaceholder".Translate()),
                condition: () => Client.Instance.Online
            );

            // Save the layout ready for the draw thread
            constructedLayout = onlineConditional;

            // Subscribe to update events
            Client.Instance.OnTradesSynced += (s, e) => repopulateTradeRows();
            Client.Instance.OnTradeCancelled += onTradeCompletedOrCancelledHandler;
            Client.Instance.OnTradeCompleted += onTradeCompletedOrCancelledHandler;
            Client.Instance.OnTradeCreationSuccess += onTradeCreationSuccessHandler;
            Client.Instance.OnTradeUpdateFailure += onTradeUpdateHandler;
            Client.Instance.OnTradeUpdateSuccess += onTradeUpdateHandler;
            Client.Instance.OnUserDisplayNameChanged += onUserDisplayNameChangedHandler;

            // Pre-fill the trade rows
            repopulateTradeRows();
        }

        public override void Draw(Rect inRect)
        {
            // Try refresh the trade list, passing until the next frame if it is occupied
            if (Monitor.TryEnter(tradeRowsLock))
            {
                if (tradesUpdated)
                {
                    // Clear out the inner container and repopulate it with the new trades
                    tradeRowsFlexContainer.Contents.Clear();
                    for (int i = 0; i < tradeRows.Count; i++)
                    {
                        // Update the alternate background state
                        tradeRows[i].DrawAlternateBackground = i % 2 != 0;

                        tradeRowsFlexContainer.Add(tradeRows[i]);
                    }

                    // Reset the updated flag
                    tradesUpdated = false;
                }

                Monitor.Exit(tradeRowsLock);
            }

            // Draw the list
            constructedLayout.Draw(inRect);
        }

        /// <inheritdoc cref="Displayable.Update"/>
        public override void Update()
        {
            repopulateTradeRows();
        }

        /// <inheritdoc cref="Displayable.CalcWidth"/>
        public override float CalcWidth(float height)
        {
            return constructedLayout.CalcWidth(height);
        }

        /// <inheritdoc cref="Displayable.CalcHeight"/>
        public override float CalcHeight(float width)
        {
            return constructedLayout.CalcHeight(width);
        }

        /// <summary>
        /// Repopulates <see cref="tradeRows"/> with the current active trades.
        /// </summary>
        /// <seealso cref="Client.GetTrades()"/>
        private void repopulateTradeRows()
        {
            lock (tradeRowsLock)
            {
                // Clear the current trade list
                tradeRows.Clear();

                // Populate TradeRows with the current active trades
                tradeRows.AddRange(Client.Instance.GetTrades().Select(tradeId => new TradeRow(tradeId)));

                // Mark the rows to be updated
                tradesUpdated = true;

                Client.Instance.Log(new LogEventArgs("Repopulated"));
            }
        }

        private void onTradeCompletedOrCancelledHandler(object sender, CompleteTradeEventArgs args)
        {
            lock (tradeRowsLock)
            {
                // Remove the trade and mark the rows to be updated
                tradeRows.RemoveAll(tradeRow => tradeRow.TradeId == args.TradeId);
                tradesUpdated = true;
            }
        }

        private void onTradeCreationSuccessHandler(object sender, CreateTradeEventArgs args)
        {
            lock (tradeRowsLock)
            {
                // Add the trade and mark the rows to be updated
                tradeRows.Add(new TradeRow(args.TradeId));
                tradesUpdated = true;
            }
        }

        private void onTradeUpdateHandler(object sender, TradeUpdateEventArgs args)
        {
            lock (tradeRowsLock)
            {
                // Update the trade row
                tradeRows.Find(tradeRow => tradeRow.TradeId == args.TradeId)?.Update();
            }
        }

        private void onUserDisplayNameChangedHandler(object sender, UserDisplayNameChangedEventArgs args)
        {
            lock (tradeRows)
            {
                // Update any trade rows with the updated user
                foreach (TradeRow tradeRow in tradeRows.Where(tradeRow => tradeRow.OtherPartyUuid == args.Uuid))
                {
                    tradeRow.Update();
                }
            }
        }
    }
}