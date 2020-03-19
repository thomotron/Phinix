// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class HorizontalScrollContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => child.IsFluidHeight;

        private const float SCROLL_BAR_WIDTH = 16f;

        /// <summary>
        /// Contents of the container.
        /// </summary>
        private Displayable child;

        /// <summary>
        /// Callback invoked when the scroll position is changed.
        /// </summary>
        private Action<Vector2> onScroll;

        /// <summary>
        /// Scroll position of the container.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        public HorizontalScrollContainer(Displayable child, Vector2 scrollPosition, Action<Vector2> onScroll)
        {
            this.scrollPosition = scrollPosition;
            this.child = child;
            this.onScroll = onScroll;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // We calculate the overflowed size the children will take
            // Only supports x-overflow at the moment
            float widthChild = child.CalcWidth(inRect.height);
            float heightChild = inRect.height - SCROLL_BAR_WIDTH;
            if (widthChild == FLUID)
            {
                // If the child is width-fluid, we attribute all available space
                widthChild = inRect.width;
            }

            // Create an inner container that will hold the scrollable content
            Rect viewRect = new Rect(inRect.xMin, inRect.yMin, widthChild, heightChild);

            // Begin scrolling
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            // Draw the contents
            child.Draw(viewRect);

            // Stop scrolling
            Widgets.EndScrollView();

            // Invoke the scroll callback
            onScroll?.Invoke(scrollPosition);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            // We can't determine a height if the child is fluid
            if (child.IsFluidHeight) return FLUID;

            // Compensate for the scrollbar width
            return child.CalcHeight(width) + SCROLL_BAR_WIDTH;
        }
    }
}
