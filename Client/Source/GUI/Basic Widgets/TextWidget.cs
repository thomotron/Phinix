// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class TextWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        public override bool IsFluidWidth => wrap;

        /// <summary>
        /// The label's text content.
        /// </summary>
        private string text;

        /// <summary>
        /// Font of the text.
        /// </summary>
        private GameFont font;

        /// <summary>
        /// Where the text should be anchored.
        /// </summary>
        private TextAnchor anchor;

        /// <summary>
        /// Whether text should wrap onto a new line.
        /// </summary>
        private bool wrap;

        public TextWidget(string text, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            this.text = text;
            this.font = font;
            this.anchor = anchor;
            this.wrap = wrap;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            SetStyle();
            float height = Text.CalcHeight(text, width);
            ClearStyle();

            return height;
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            // Calculating width with wrapping is pointless, so we won't do that
            if (wrap) return FLUID;

            SetStyle();
            float width = Text.CalcSize(text).x;
            ClearStyle();

            return width;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            SetStyle();
            Widgets.Label(inRect, text);
            ClearStyle();
        }

        /// <summary>
        /// Sets the text font and anchor.
        /// </summary>
        private void SetStyle()
        {
            Text.Anchor = this.anchor;
            Text.Font = this.font;
            Text.WordWrap = this.wrap;
        }

        /// <summary>
        /// Resets the text font and anchor to their defaults.
        /// </summary>
        private void ClearStyle()
        {
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            Text.WordWrap = true;
        }
    }
}
