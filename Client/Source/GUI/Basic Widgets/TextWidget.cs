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

        public TextWidget(string text, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            this.text = text;
            this.font = font;
            this.anchor = anchor;
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
        }

        /// <summary>
        /// Resets the text font and anchor to their defaults.
        /// </summary>
        private void ClearStyle()
        {
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
