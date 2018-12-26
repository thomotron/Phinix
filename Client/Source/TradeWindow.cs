using RimWorld;
using UnityEngine;
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

        private const float OFFER_WINDOW_WIDTH = 400f;
        private const float OFFER_WINDOW_HEIGHT = 310f;

        private const float TITLE_HEIGHT = 30f;

        /// <summary>
        /// The ID of the trade this window is for.
        /// </summary>
        private string tradeId;

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
        }

        /// <summary>
        /// Draws the title of the trade window within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawTitle(Rect container)
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
            
            // Set the text style
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            
            // Draw the title
            Widgets.Label(container, "Trade with " + displayName);
            
            // Reset the text style
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}