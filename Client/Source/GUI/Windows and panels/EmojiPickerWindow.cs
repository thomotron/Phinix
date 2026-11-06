using System;
using System.Collections.Generic;
using System.Linq;
using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// A grid of emojis to pick from, with a search box and a tab for each category. Clicking one adds its :shortcode:
    /// to the message being written; the window stays open so several can be added.
    /// </summary>
    public class EmojiPickerWindow : Window
    {
        private const float CELL_SIZE = 30f;
        private const float ICON_SIZE = 24f;
        private const float SEARCH_HEIGHT = 28f;
        private const float TAB_SIZE = 32f;
        private const float SPACING = 6f;

        public override Vector2 InitialSize => new Vector2(8 * TAB_SIZE + 2 * Margin + 16f, 380f);

        private readonly Action<string> onPick;
        private string search = "";
        private int category;
        private Vector2 scroll = Vector2.zero;

        /// <param name="onPick">Called with an emoji's shortcode (without colons) when it's clicked</param>
        public EmojiPickerWindow(Action<string> onPick)
        {
            this.onPick = onPick;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            draggable = true;
            preventCameraMotion = false;
        }

        protected override void SetInitialSizeAndPosition()
        {
            // Open just above the mouse, where the button was
            Vector2 size = InitialSize;
            Vector2 mouse = UI.MousePositionOnUIInverted;
            windowRect = new Rect(
                Mathf.Clamp(mouse.x - size.x / 2f, 0f, UI.screenWidth - size.x),
                Mathf.Clamp(mouse.y - size.y - 10f, 0f, UI.screenHeight - size.y),
                size.x,
                size.y
            ).Rounded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            // Search box, with a faint hint while it's empty
            Rect searchRect = new Rect(inRect.x, y, inRect.width - 30f, SEARCH_HEIGHT);
            search = Widgets.TextField(searchRect, search);
            if (search.Length == 0)
            {
                PhimeStyle.Label(new Rect(searchRect.x + 6f, searchRect.y, searchRect.width - 6f, searchRect.height), "Phinix_emoji_searchHint".Translate(), PhimeStyle.Dim, GameFont.Small, TextAnchor.MiddleLeft);
            }
            y += SEARCH_HEIGHT + SPACING;

            // A tab per category, shown by its first emoji. They're not used while searching
            bool searching = !string.IsNullOrWhiteSpace(search);
            for (int i = 0; i < EmojiIndex.Categories.Length; i++)
            {
                Rect tabRect = new Rect(inRect.x + i * TAB_SIZE, y, TAB_SIZE, TAB_SIZE);
                if (!searching && i == category) Widgets.DrawBoxSolid(tabRect, PhimeStyle.GoldHighlight);
                else if (Mouse.IsOver(tabRect)) Widgets.DrawHighlight(tabRect);

                string first = EmojiIndex.All.First(emoji => emoji.Category == i).Name;
                Emoji.Draw(tabRect.ContractedBy((TAB_SIZE - ICON_SIZE) / 2f), first);
                TooltipHandler.TipRegion(tabRect, EmojiIndex.Categories[i]);

                if (Widgets.ButtonInvisible(tabRect))
                {
                    category = i;
                    search = "";
                    scroll = Vector2.zero;
                }
            }
            y += TAB_SIZE + SPACING;

            // The grid
            List<string> shown = emojisToShow(searching);
            Rect gridRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Widgets.DrawMenuSection(gridRect);
            Rect outRect = gridRect.ContractedBy(4f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((outRect.width - 16f) / CELL_SIZE));
            int rows = Mathf.CeilToInt(shown.Count / (float) columns);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, rows * CELL_SIZE);

            if (shown.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(outRect, "Phinix_emoji_noMatches".Translate());
                return;
            }

            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            for (int i = 0; i < shown.Count; i++)
            {
                Rect cellRect = new Rect(i % columns * CELL_SIZE, i / columns * CELL_SIZE, CELL_SIZE, CELL_SIZE);

                // Only draw what's in view, there are a lot of them
                if (cellRect.yMax < scroll.y || cellRect.y > scroll.y + outRect.height) continue;

                if (Mouse.IsOver(cellRect)) Widgets.DrawHighlight(cellRect);
                Emoji.Draw(cellRect.ContractedBy((CELL_SIZE - ICON_SIZE) / 2f), shown[i]);
                TooltipHandler.TipRegion(cellRect, ":" + shown[i] + ":");

                if (Widgets.ButtonInvisible(cellRect)) onPick(shown[i]);
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// The emojis in the open category, or every emoji with a shortcode containing the search text.
        /// </summary>
        private List<string> emojisToShow(bool searching)
        {
            if (!searching)
            {
                return EmojiIndex.All.Where(emoji => emoji.Category == category).Select(emoji => emoji.Name).ToList();
            }

            string term = search.Trim().Trim(':').ToLowerInvariant().Replace(' ', '_');
            return EmojiIndex.All
                .Where(emoji => emoji.Name.Contains(term) || emoji.Aliases.Any(alias => alias.Contains(term)))
                .Select(emoji => emoji.Name)
                .ToList();
        }
    }
}
