using System.Collections.Generic;
using System.Linq;
using System.Text;
using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Past trades with one player, newest first: when, whether it went through, and what went each way.
    /// </summary>
    public class TradeHistoryWindow : Window
    {
        private const float TITLE_HEIGHT = 32f;
        private const float CARD_PADDING = 8f;
        private const float CARD_SPACING = 8f;
        private const float SCROLLBAR_WIDTH = 16f;

        public override Vector2 InitialSize => new Vector2(560f, 620f);

        private readonly string uuid;
        private readonly string name;
        private Vector2 scroll = Vector2.zero;

        public TradeHistoryWindow(string uuid, string name)
        {
            this.uuid = uuid;
            this.name = name;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            draggable = true;
            preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            PhimeStyle.Label(inRect.TopPartPixels(TITLE_HEIGHT), "Phinix_tradeHistory_title".Translate(name), Color.white, GameFont.Medium, TextAnchor.MiddleLeft);

            Rect listRect = new Rect(inRect.x, inRect.y + TITLE_HEIGHT + 6f, inRect.width, inRect.height - TITLE_HEIGHT - 6f);
            List<TradeHistory.Entry> entries = TradeHistory.For(uuid);
            if (entries.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(listRect, "Phinix_tradeHistory_none".Translate());
                return;
            }

            float textWidth = listRect.width - SCROLLBAR_WIDTH - CARD_PADDING * 2;
            Text.Font = GameFont.Small;
            List<(string text, float height)> cards = entries.Select(entry =>
            {
                string text = describe(entry);
                return (text, Text.CalcHeight(text, textWidth) + CARD_PADDING * 2);
            }).ToList();

            Rect viewRect = new Rect(0f, 0f, listRect.width - SCROLLBAR_WIDTH, cards.Sum(card => card.height + CARD_SPACING));
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            foreach ((string text, float height) in cards)
            {
                Rect cardRect = new Rect(0f, y, viewRect.width, height);
                if (cardRect.yMax >= scroll.y && cardRect.y <= scroll.y + listRect.height)
                {
                    PhimeStyle.Card(cardRect, PhimeStyle.CardFill, PhimeStyle.CardBorder);
                    Widgets.Label(cardRect.ContractedBy(CARD_PADDING), text);
                }
                y += height + CARD_SPACING;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// A trade as text: "2 Nov 2026, 18:10 · Completed", then what went each way.
        /// </summary>
        private string describe(TradeHistory.Entry entry)
        {
            StringBuilder text = new StringBuilder();
            text.Append(PhimeStyle.Colored(entry.Time.ToString("d MMM yyyy, HH:mm"), PhimeStyle.Muted));
            text.Append("  ");
            text.Append(entry.Completed
                ? PhimeStyle.Colored("Phinix_tradeHistory_completed".Translate(), PhimeStyle.Green)
                : PhimeStyle.Colored("Phinix_tradeHistory_cancelled".Translate(), PhimeStyle.Warning));

            if (entry.Completed)
            {
                appendSide(text, "Phinix_tradeHistory_gave".Translate(), entry.Gave);
                appendSide(text, "Phinix_tradeHistory_got".Translate(name), entry.Got);
            }
            else if (entry.Gave.Count > 0)
            {
                appendSide(text, "Phinix_tradeHistory_cameBack".Translate(), entry.Gave);
            }
            return text.ToString();
        }

        private static void appendSide(StringBuilder text, string heading, List<TradeHistory.Line> lines)
        {
            text.Append("\n\n");
            float total = lines.Sum(line => line.Value);
            text.Append(heading);
            if (total > 0f) text.Append(PhimeStyle.Colored("  " + total.Money(), PhimeStyle.Muted));

            if (lines.Count == 0)
            {
                text.Append("\n   " + PhimeStyle.Colored("Phinix_tradeHistory_nothing".Translate(), PhimeStyle.Dim));
                return;
            }
            foreach (TradeHistory.Line line in lines)
            {
                text.Append("\n   " + (line.Count > 1 ? line.Count + " x " : "") + line.Label);
            }
        }
    }
}
