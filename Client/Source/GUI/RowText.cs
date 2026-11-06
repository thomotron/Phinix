using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Lays out the text lines of a board row (market, bounties, contracts) so each line gets as much height as it
    /// needs. A long note or a long item list wraps onto more lines and the row grows, instead of being squashed into
    /// one line where most of it gets cut off.
    /// </summary>
    /// <remarks>
    /// Drawing happens in two passes, because a scroll view needs its total height before anything is drawn:
    /// <see cref="Measure"/> works out how tall a row will be, then <see cref="Draw"/> draws it.
    /// <para>
    /// Each line is a (Text, Small) pair. Small lines use the tiny grey font for notes and times; the rest use the
    /// normal font. In Python terms the list is like [("Wanted · Bob", False), ("\"hello\"  just now", True)].
    /// </para>
    /// </remarks>
    public static class RowText
    {
        /// <summary>
        /// Shortest a normal line can be, so one-line rows keep the height they always had.
        /// </summary>
        public const float LINE_HEIGHT = 22f;

        /// <summary>
        /// Shortest a small line can be.
        /// </summary>
        public const float SMALL_LINE_HEIGHT = 18f;

        /// <summary>
        /// Total height of the lines when wrapped to <paramref name="width"/>.
        /// </summary>
        public static float Measure(List<(string Text, bool Small)> lines, float width)
        {
            float total = 0f;
            foreach ((string text, bool small) in lines) total += lineHeight(text, small, width);
            return total;
        }

        /// <summary>
        /// Draws the lines one under another, starting at (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        public static void Draw(List<(string Text, bool Small)> lines, float x, float y, float width)
        {
            // Save the text settings so we can put them back afterwards (other widgets expect the defaults)
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWrap = Text.WordWrap;
            Text.WordWrap = true;

            foreach ((string text, bool small) in lines)
            {
                float height = lineHeight(text, small, width);

                Text.Font = small ? GameFont.Tiny : GameFont.Small;
                // A single line looks best centred in its slot; wrapped text needs to start at the top
                Text.Anchor = height > (small ? SMALL_LINE_HEIGHT : LINE_HEIGHT) ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
                if (small) UnityEngine.GUI.color = Color.gray;

                Widgets.Label(new Rect(x, y, width, height), text);

                UnityEngine.GUI.color = Color.white;
                y += height;
            }

            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
            Text.WordWrap = previousWrap;
        }

        /// <summary>
        /// How tall one line needs to be: its wrapped height, but never less than the usual line height.
        /// </summary>
        private static float lineHeight(string text, bool small, float width)
        {
            // CalcHeight measures with the current font, so switch to the right one first and switch back after
            GameFont previousFont = Text.Font;
            Text.Font = small ? GameFont.Tiny : GameFont.Small;
            float height = Text.CalcHeight(text, width);
            Text.Font = previousFont;

            return Mathf.Max(height, small ? SMALL_LINE_HEIGHT : LINE_HEIGHT);
        }
    }
}
