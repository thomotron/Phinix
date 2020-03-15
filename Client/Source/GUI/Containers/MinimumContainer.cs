using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class MinimumContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => minHeight.Equals(FLUID);

        /// <inheritdoc />
        public override bool IsFluidWidth => minWidth.Equals(FLUID);
        
        /// <summary>
        /// Minimum width of the container.
        /// </summary>
        private float minWidth;
        
        /// <summary>
        /// Minimum height of the container.
        /// </summary>
        private float minHeight;
        
        /// <summary>
        /// Contents of the container.
        /// </summary>
        private Displayable child;
        
        public MinimumContainer(Displayable child, float minWidth = FLUID, float minHeight = FLUID)
        {
            this.child = child;
            this.minWidth = minWidth;
            this.minHeight = minHeight;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            if (IsFluidHeight)
            {
                return FLUID;
            }
            else
            {
                return Math.Max(minHeight, child.CalcHeight(width));
            }
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            if (IsFluidWidth)
            {
                return FLUID;
            }
            else
            {
                return Math.Max(minWidth, child.CalcWidth(height));
            }
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Draw the container's contents
            child.Draw(inRect);
        }

        /// <inheritdoc />
        public override void Update()
        {
            child.Update();
        }
    }
}