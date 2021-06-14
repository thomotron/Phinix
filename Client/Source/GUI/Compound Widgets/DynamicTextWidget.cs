// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class DynamicTextWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => underlyingWidget.IsFluidHeight;
        /// <inheritdoc />
        public override bool IsFluidWidth => underlyingWidget.IsFluidWidth;

        /// <summary>
        /// The label's text content callback.
        /// </summary>
        private Func<string> textCallback;

        /// <summary>
        /// The underlying <see cref="TextWidget"/> used for rendering the result of <see cref="textCallback"/>.
        /// </summary>
        private TextWidget underlyingWidget;

        /// <summary>
        /// Creates a new <see cref="DynamicTextWidget"/> based on the given callback and formatting options.
        /// </summary>
        /// <remarks>
        /// This widget uses an underlying <see cref="TextWidget"/> to handle rendering, so the formatting options are
        /// not handled directly.
        ///
        /// The callback is also re-evaluated every time <see cref="Update"/> is called, not every time it is drawn, so
        /// be careful to call it when the content is expected to change. This is purely for performance reasons.
        /// </remarks>
        /// <param name="textCallback">Text callback</param>
        /// <param name="font">Font of the text</param>
        /// <param name="anchor">Where the text should be anchored</param>
        /// <param name="wrap">Whether text should wrap onto a new line</param>
        /// <exception cref="ArgumentException">Text callback cannot be null</exception>
        public DynamicTextWidget(Func<string> textCallback, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            // Refuse null callbacks
            if (textCallback == null) throw new ArgumentNullException(nameof(textCallback), "Text callback cannot be null.");

            this.textCallback = textCallback;
            this.underlyingWidget = new TextWidget(textCallback.Invoke(), font, anchor, wrap);
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            underlyingWidget.Draw(inRect);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return underlyingWidget.CalcHeight(width);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return underlyingWidget.CalcWidth(height);
        }

        /// <inheritdoc />
        public override void Update()
        {
            underlyingWidget.Text = textCallback.Invoke();
            underlyingWidget.Update();
        }
    }
}
