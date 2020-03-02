// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class Container : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => height.Equals(FLUID);

        /// <inheritdoc />
        public override bool IsFluidWidth => width.Equals(FLUID);
        
        /// <summary>
        /// Width of the container.
        /// </summary>
        private float width;
        
        /// <summary>
        /// Height of the container.
        /// </summary>
        private float height;
        
        /// <summary>
        /// Contents of the container.
        /// </summary>
        private Displayable child;
        
        public Container(Displayable child, float width = FLUID, float height = FLUID)
        {
            this.child = child;
            this.width = width;
            this.height = height;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return height;
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return width;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            if (!IsFluidHeight)
            {
                // Clip the rect to the container's height
                inRect = inRect.TopPartPixels(height);
            }
            if (!IsFluidWidth)
            {
                // Clip the rect to the container's width
                inRect = inRect.LeftPartPixels(width);
            }

            // Draw the container's contents
            child.Draw(inRect);
        }
    }
}
