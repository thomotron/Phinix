using UnityEngine;

namespace PhinixClient.GUI
{
    public class VerticalPaddedContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => true;

        /// <inheritdoc />
        public override bool IsFluidWidth => false;

        /// <summary>
        /// Contents of the container.
        /// </summary>
        private Displayable child;

        /// <summary>
        /// Width of the container.
        /// </summary>
        private float width;

        public VerticalPaddedContainer(Displayable child, float width)
        {
            this.child = child;
            this.width = width;
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return width;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Check if the child is non-fluid and smaller than the allocated space
            if (!child.IsFluidHeight && inRect.height > child.CalcHeight(inRect.width))
            {
                // Create a flex container to hold the child and spacers
                VerticalFlexContainer column = new VerticalFlexContainer(0f);

                // Sandwich the child between two spacers
                column.Add(new SpacerWidget());
                column.Add(child);
                column.Add(new SpacerWidget());

                // Draw the container
                column.Draw(inRect);
            }
            else
            {
                // Just draw the child, there's no padding to be done here
                child.Draw(inRect);
            }
        }
    }
}