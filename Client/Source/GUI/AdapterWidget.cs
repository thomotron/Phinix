using System;
using UnityEngine;

namespace PhinixClient.GUI
{
    /// <summary>
    /// An open-ended adapter class used for drawing custom or complex widgets.
    /// </summary>
    public class AdapterWidget : Displayable
    {
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
        
        public AdapterWidget(Action<Rect> drawCallback, Func<float, float> getWidthCallback, Func<float, float> getHeightCallback)
        {
            this.drawCallback = drawCallback;
            this.getWidthCallback = getWidthCallback;
            this.getHeightCallback = getHeightCallback;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            drawCallback(inRect);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return getWidthCallback(height);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return getHeightCallback(width);
        }
    }
}