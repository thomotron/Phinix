using System;
using UnityEngine;

namespace PhinixClient.GUI
{
    /// <summary>
    /// An open-ended adapter class used for drawing custom or complex widgets.
    /// </summary>
    public class AdapterWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight { get; }
        /// <inheritdoc />
        public override bool IsFluidWidth { get; }

        /// <summary>
        /// Callback invoked when the adapter is drawn.
        /// </summary>
        private Action<Rect> drawCallback;

        /// <summary>
        /// Callback invoked when the adapter's width is requested.
        /// </summary>
        private Func<float, float> getWidthCallback;

        /// <summary>
        /// Callback invoked when the adapter's height is requested.
        /// </summary>
        private Func<float, float> getHeightCallback;

        public AdapterWidget(Action<Rect> drawCallback, Func<float, float> getWidthCallback = null, Func<float, float> getHeightCallback = null)
        {
            this.drawCallback = drawCallback;
            this.getWidthCallback = getWidthCallback;
            this.getHeightCallback = getHeightCallback;

            IsFluidWidth = getWidthCallback == null;
            IsFluidHeight = getHeightCallback == null;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            drawCallback(inRect);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return getWidthCallback == null ? FLUID : getWidthCallback(height);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return getHeightCallback == null ? FLUID : getHeightCallback(width);
        }
    }
}