using System;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class TradeRow : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        private const float DEFAULT_SPACING = 10f;

        private const float TRADE_WITH_LABEL_HEIGHT = 25f;

        private const float ACCEPTED_STATE_LABEL_HEIGHT = 15f;

        private const float HEIGHT = TRADE_WITH_LABEL_HEIGHT + ACCEPTED_STATE_LABEL_HEIGHT;

        private const float BUTTON_WIDTH = 80f;

        /// <summary>
        /// Trade ID this trade row represents.
        /// </summary>
        public readonly string TradeId;

        /// <summary>
        /// Whether to draw an alternate, lighter background behind the trade row.
        /// </summary>
        public bool DrawAlternateBackground;

        /// <summary>
        /// Other party's UUID.
        /// </summary>
        public string OtherPartyUuid => otherPartyUuid;
        /// <inheritdoc cref="OtherPartyUuid"/>
        private string otherPartyUuid;

        /// <summary>
        /// A cached copy of the other party's display name.
        /// Refreshed every time <see cref="Update"/> is called.
        /// </summary>
        private string cachedOtherPartyDisplayName;

        /// <summary>
        /// A cached copy of the other party's accepted state.
        /// Refreshed every time <see cref="Update"/> is called.
        /// </summary>
        /// <returns></returns>
        private bool cachedOtherPartyAccepted;

        /// <summary>
        /// The pre-generated trade row.
        /// </summary>
        private readonly Displayable content;

        /// <summary>
        /// Creates a new <see cref="TradeRow"/> from the given trade ID.
        /// </summary>
        /// <param name="tradeId">Trade ID</param>
        /// <param name="drawAlternateBackground">Whether to draw an alternate, lighter background</param>
        /// <exception cref="Exception">Failed to get other party's display name</exception>
        /// <exception cref="Exception">Failed to get whether the other party has accepted or not</exception>
        public TradeRow(string tradeId, bool drawAlternateBackground = false)
        {
            this.TradeId = tradeId;
            this.DrawAlternateBackground = drawAlternateBackground;

            // Try get the other party's UUID and display name
            if (!Client.Instance.TryGetOtherPartyUuid(tradeId, out otherPartyUuid) ||
                !Client.Instance.TryGetDisplayName(otherPartyUuid, out cachedOtherPartyDisplayName))
            {
                // Failed to get the other party's display name
                throw new Exception("Failed to get the other party's display name.");
            }

            // Try get the other party's accepted state
            if (!Client.Instance.TryGetPartyAccepted(tradeId, otherPartyUuid, out cachedOtherPartyAccepted))
            {
                // Failed to get the other party's accepted state
                throw new Exception("Failed to get whether the other party has accepted or not.");
            }

            // Generate the content
            this.content = generateTradeRow();
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Draw a background highlight
            if (DrawAlternateBackground) Widgets.DrawHighlight(inRect);

            // Draw the row
            content.Draw(inRect);
        }

        public override void Update()
        {
            // Try get the other party's UUID and display name
            if (!Client.Instance.TryGetOtherPartyUuid(TradeId, out otherPartyUuid) ||
                !Client.Instance.TryGetDisplayName(otherPartyUuid, out cachedOtherPartyDisplayName))
            {
                // Failed to get the other party's display name
                throw new Exception("Failed to get the other party's display name.");
            }

            // Try get the other party's accepted state
            if (!Client.Instance.TryGetPartyAccepted(TradeId, otherPartyUuid, out cachedOtherPartyAccepted))
            {
                // Failed to get the other party's accepted state
                throw new Exception("Failed to get whether the other party has accepted or not.");
            }

            // Update the pre-generated row
            content.Update();
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return HEIGHT;
        }

        /// <summary>
        /// Generates the trade row based on the cached state variables.
        /// This only needs to be run once.
        /// </summary>
        /// <returns>Generated trade row</returns>
        private Displayable generateTradeRow()
        {
            // Create a row to hold everything
            HorizontalFlexContainer row = new HorizontalFlexContainer(DEFAULT_SPACING);

            // Create a column to hold the text
            VerticalFlexContainer textColumn = new VerticalFlexContainer(0f);

            // Trade with ... label
            textColumn.Add(
                new Container(
                    new DynamicTextWidget(
                        textCallback: () => "Phinix_trade_activeTrade_tradeWithLabel".Translate(TextHelper.StripRichText(cachedOtherPartyDisplayName)),
                        anchor: TextAnchor.MiddleLeft
                    ),
                    height: TRADE_WITH_LABEL_HEIGHT
                )
            );

            // Accepted state label
            textColumn.Add(
                new Container(
                    new DynamicTextWidget(
                        textCallback: () => ("Phinix_trade_activeTrade_theyHave" + (!cachedOtherPartyAccepted ? "Not" : "") + "Accepted").Translate(),
                        font: GameFont.Tiny,
                        anchor: TextAnchor.MiddleLeft
                    ),
                    height: ACCEPTED_STATE_LABEL_HEIGHT
                )
            );

            // Add the text column to the row
            row.Add(textColumn);

            // Open button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_activeTrade_openButton".Translate(),
                        clickAction: () => Find.WindowStack.Add(new TradeWindow(TradeId))
                    ),
                    width: BUTTON_WIDTH
                )
            );

            // Cancel button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_cancelButton".Translate(),
                        clickAction: () => Client.Instance.CancelTrade(TradeId)
                    ),
                    width: BUTTON_WIDTH
                )
            );

            return row;
        }
    }
}