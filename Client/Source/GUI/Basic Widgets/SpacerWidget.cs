using PhinixClient.GUI;
using UnityEngine;

namespace PhinixClient
{
    /// <summary>
    /// An empty widget that takes up the configured amount of space.
    /// </summary>
    public class SpacerWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => height.Equals(FLUID);

        /// <inheritdoc />
        public override bool IsFluidWidth => width.Equals(FLUID);

        /// <summary>
        /// Height of the spacer.
        /// </summary>
        private float height;

        /// <summary>
        /// Width of the spacer.
        /// </summary>
        private float width;

        public SpacerWidget(float width = FLUID, float height = FLUID)
        {
            this.width = width;
            this.height = height;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Do nothing
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return width;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return height;
        }
    }
}