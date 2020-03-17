// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class ScrollContainer : Displayable
    {
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

        public ScrollContainer(Displayable child, Vector2 scrollPosition, Action<Vector2> onScroll)
        {
            this.scrollPosition = scrollPosition;
            this.child = child;
            this.onScroll = onScroll;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Calculate the overflowed size the children will take
            // Only supports y-overflow at the moment
            float widthChild = inRect.width - SCROLL_BAR_WIDTH;
            float heightChild = child.CalcHeight(widthChild);
            if (heightChild == FLUID)
            {
                // If the child is height-fluid, we attribute all available space
                heightChild = inRect.height;
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
    }
}
