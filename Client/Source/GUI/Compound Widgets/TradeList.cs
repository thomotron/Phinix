using System;
using System.Collections.Generic;
using System.Threading;
using Trading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class TradeList
    {
        private const float SCROLLBAR_WIDTH = 16f;

        private const float DEFAULT_SPACING = 10f;

        private const float TRADE_TITLE_LABEL_HEIGHT = 20f;

        private const float ACCEPTED_STATE_LABEL_HEIGHT = 18f;

        private const float ROW_HEIGHT = TRADE_TITLE_LABEL_HEIGHT + ACCEPTED_STATE_LABEL_HEIGHT;

        private const float BUTTON_WIDTH = 80f;

        /// <summary>
        /// List of active trades.
        /// </summary>
        private readonly List<ImmutableTrade> trades = new List<ImmutableTrade>();
        /// <summary>
        /// Filtered list of active trades.
        /// </summary>
        private readonly List<ImmutableTrade> filteredTrades = new List<ImmutableTrade>();
        /// <summary>
        /// Whether <see cref="trades"/> has been updated and <see cref="filteredTrades"/> should be repopulated by the
        /// UI thread.
        /// </summary>
        private bool tradesChanged = false;
        /// <summary>
        /// Lock object protecting <see cref="trades"/>.
        /// </summary>
        private readonly object tradesLock = new object();

        /// <summary>
        /// Creates a new <see cref="TradeList"/>.
        /// </summary>
        public TradeList()
        {
            // Subscribe to update events
            Client.Instance.OnTradesSynced += onTradesSyncedHandler;
            Client.Instance.OnTradeCancelled += onTradeCompletedOrCancelledHandler;
            Client.Instance.OnTradeCompleted += onTradeCompletedOrCancelledHandler;
            Client.Instance.OnTradeCreationSuccess += onTradeCreationSuccessHandler;
            Client.Instance.OnTradeUpdateFailure += onTradeUpdateHandler;
            Client.Instance.OnTradeUpdateSuccess += onTradeUpdateHandler;
            Client.Instance.OnUserDisplayNameChanged += onUserDisplayNameChangedHandler;

            // Pre-fill the trade rows
            repopulateTradeRows();
        }

        public void Draw(Rect inRect)
        {
            if (tradesChanged)
            {
                // Try lock the unfiltered list, otherwise wait until the next frame to refresh content
                if (Monitor.TryEnter(tradesLock))
                {
                    // Repopulate the list content
                    filteredTrades.Clear();
                    filteredTrades.AddRange(trades);

                    // Unset the changed flag and release the lock
                    tradesChanged = false;
                    Monitor.Exit(tradesLock);
                }
            }

            // Draw a placeholder if there are no trades
            if (!filteredTrades.Any())
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_trade_noActiveTradesPlaceholder".Translate());
                return;
            }

            // Set up the scrollable container
            Rect contentRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: ROW_HEIGHT * filteredTrades.Count
            );
            if (contentRect.height > inRect.height) contentRect.width = inRect.width - SCROLLBAR_WIDTH;

            // Draw the list
            float currentY = contentRect.yMin;
            for (int i = 0; i < filteredTrades.Count; i++, currentY += ROW_HEIGHT)
            {
                ImmutableTrade trade = filteredTrades[i];

                Rect rowRect = new Rect(contentRect.xMin, currentY, contentRect.width, ROW_HEIGHT);
                Rect buttonAreaRect = new Rect(rowRect.xMax - (BUTTON_WIDTH * 2 + DEFAULT_SPACING), currentY, BUTTON_WIDTH * 2 + DEFAULT_SPACING, ROW_HEIGHT);
                Rect tradeTitleRect = new Rect(rowRect.xMin, currentY, rowRect.width - buttonAreaRect.width - DEFAULT_SPACING, TRADE_TITLE_LABEL_HEIGHT);
                Rect acceptedStateRect = new Rect(rowRect.xMin, tradeTitleRect.yMax, rowRect.width - buttonAreaRect.width - DEFAULT_SPACING, ACCEPTED_STATE_LABEL_HEIGHT);

                // Background highlight
                if (i % 2 != 0) Widgets.DrawHighlight(rowRect);

                // Save the current text settings
                GameFont previousFont = Text.Font;
                TextAnchor previousAnchor = Text.Anchor;

                // Trade with ... label
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.LabelFit(tradeTitleRect, "Phinix_trade_activeTrade_tradeWithLabel".Translate(TextHelper.StripRichText(trade.OtherPartyDisplayName)));

                // Accepted state label
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.LowerLeft;
                Widgets.Label(acceptedStateRect, ("Phinix_trade_activeTrade_theyHave" + (!trade.OtherPartyAccepted ? "Not" : "") + "Accepted").Translate());

                // Restore the text settings
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;

                // Open button
                if (Widgets.ButtonText(buttonAreaRect.LeftPartPixels(BUTTON_WIDTH), "Phinix_trade_activeTrade_openButton".Translate()))
                {
                    Find.WindowStack.Add(new TradeWindow(trade));
                }

                // Cancel button
                if (Widgets.ButtonText(buttonAreaRect.RightPartPixels(BUTTON_WIDTH), "Phinix_trade_activeTrade_cancelButton".Translate()))
                {
                    Client.Instance.CancelTrade(trade.TradeId);
                }
            }
        }

        /// <summary>
        /// Repopulates <see cref="trades"/> with the current active trades.
        /// </summary>
        /// <seealso cref="Client.GetTrades()"/>
        private void repopulateTradeRows()
        {
            lock (tradesLock)
            {
                // Clear and repopulate the list with active trades
                trades.Clear();
                trades.AddRange(Client.Instance.GetTrades());

                // Mark the rows to be updated
                tradesChanged = true;

                Client.Instance.Log(new LogEventArgs("Repopulated trade list", LogLevel.DEBUG));
            }
        }

        private void onTradesSyncedHandler(object sender, UITradesSyncedEventArgs args)
        {
            lock (tradesLock)
            {
                // Repopulate trade list with this set and mark the rows to be updated
                trades.Clear();
                trades.AddRange(args.Trades);
                tradesChanged = true;
            }
        }

        private void onTradeCompletedOrCancelledHandler(object sender, UICompleteTradeEventArgs args)
        {
            lock (tradesLock)
            {
                // Remove the trade and mark the rows to be updated
                trades.RemoveAll(t => t.TradeId == args.TradeId);
                tradesChanged = true;
            }
        }

        private void onTradeCreationSuccessHandler(object sender, UICreateTradeEventArgs args)
        {
            lock (tradesLock)
            {
                // Add the trade and mark the rows to be updated
                trades.Add(args.Trade);
                tradesChanged = true;
            }
        }

        private void onTradeUpdateHandler(object sender, UITradeUpdateEventArgs args)
        {
            lock (tradesLock)
            {
                // Update the trade row
                int index = trades.FindIndex(t => t.TradeId == args.TradeId);
                if (index >= 0)
                {
                    trades[index] = args.Trade;
                }

                // Mark the rows to be updated
                tradesChanged = true;
            }
        }

        private void onUserDisplayNameChangedHandler(object sender, UserDisplayNameChangedEventArgs args)
        {
            lock (trades)
            {
                // Update any trade rows with the updated user
                int matchIndex = trades.FindIndex(0, t => t.OtherPartyUuid == args.Uuid);
                while (matchIndex >= 0)
                {
                    // Update the user's display name and replace the trade
                    ImmutableUser user = trades[matchIndex].OtherParty;
                    user = new ImmutableUser(
                        uuid: user.Uuid,
                        displayName: args.NewDisplayName,
                        loggedIn: user.LoggedIn,
                        acceptingTrades: user.AcceptingTrades
                    );

                    trades[matchIndex] = new ImmutableTrade(
                        tradeId: trades[matchIndex].TradeId,
                        otherParty: user,
                        ourItemsOnOffer: trades[matchIndex].ItemsOnOffer,
                        otherPartyItemsOnOffer: trades[matchIndex].OtherPartyItemsOnOffer,
                        accepted: trades[matchIndex].Accepted,
                        otherPartyAccepted: trades[matchIndex].OtherPartyAccepted
                    );

                    try
                    {
                        matchIndex = trades.FindIndex(matchIndex + 1, t => t.OtherPartyUuid == args.Uuid);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Hit the end of the list, stop searching
                        break;
                    }
                }

                // Mark the rows to be updated if we made any changes
                tradesChanged = matchIndex >= 0;
            }
        }
    }
}