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
        /// Trade ID this trade row represents
        /// </summary>
        private string tradeId;

        public TradeRow(string tradeId)
        {
            this.tradeId = tradeId;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Try get the other party's UUID and display name
            if (!Client.Instance.TryGetOtherPartyUuid(tradeId, out string otherPartyUuid) ||
                !Client.Instance.TryGetDisplayName(otherPartyUuid, out string otherPartyDisplayName))
            {
                // Failed to get the other party's display name
                throw new Exception("Failed to get the other party's display name when drawing a TradeRow");
            }

            // Try get the other party's accepted state
            if (!Client.Instance.TryGetPartyAccepted(tradeId, otherPartyUuid, out bool otherPartyAccepted))
            {
                // Failed to get the other party's accepted state
                throw new Exception("Failed to get whether the other party has accepted or not when drawing a TradeRow");
            }
            
            // Create a row to hold everything
            HorizontalFlexContainer row = new HorizontalFlexContainer(DEFAULT_SPACING);
            
            // Create a column to hold the text
            VerticalFlexContainer textColumn = new VerticalFlexContainer(0f);
            
            // Trade with ... label
            textColumn.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_trade_activeTrade_tradeWithLabel".Translate(TextHelper.StripRichText(otherPartyDisplayName)),
                        anchor: TextAnchor.MiddleLeft
                    ),
                    height: TRADE_WITH_LABEL_HEIGHT
                )
            );
            
            // Accepted state label
            textColumn.Add(
                new Container(
                    new TextWidget(
                        text: ("Phinix_trade_activeTrade_theyHave" + (!otherPartyAccepted ? "Not" : "") + "Accepted").Translate(),
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
                        clickAction: () => Find.WindowStack.Add(new TradeWindow(tradeId))
                    ),
                    width: BUTTON_WIDTH
                )
            );
            
            // Cancel button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_trade_cancelButton".Translate(),
                        clickAction: () => Client.Instance.CancelTrade(tradeId)
                    ),
                    width: BUTTON_WIDTH
                )
            );
            
            // Draw the row
            row.Draw(inRect);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return HEIGHT;
        }
    }
}