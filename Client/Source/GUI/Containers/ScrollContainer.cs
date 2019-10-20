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
            // Create a container for the 'view' (i.e. the window we look at scrollable content through)
            Rect viewRect = inRect.LeftPartPixels(inRect.width - SCROLL_BAR_WIDTH);

            // We calculate the overflowed size the children will take
            // Only supports y-overflow at the moment
            float widthChild = viewRect.width;
            float heightChild = child.CalcHeight(viewRect.width);
            if (heightChild == FLUID)
            {
                // If the child is height-fluid, we attribute the available space
                heightChild = viewRect.height;
            }
            
            // Create an inner container that will hold the scrollable content
            Rect childRect = new Rect(0, 0, widthChild, heightChild);
            
            // Begin scrolling
            Widgets.BeginScrollView(inRect, ref scrollPosition, childRect);
            
            // Invoke the scroll callback
            onScroll?.Invoke(scrollPosition);
            
            // Draw the contents
            child.Draw(childRect);

            // Stop scrolling
            Widgets.EndScrollView();
        }
    }
}
