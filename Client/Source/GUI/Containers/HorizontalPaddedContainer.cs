using UnityEngine;

namespace PhinixClient.GUI
{
    public class HorizontalPaddedContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        /// <inheritdoc />
        public override bool IsFluidWidth => true;

        /// <summary>
        /// Contents of the container.
        /// </summary>
        private Displayable child;

        /// <summary>
        /// Height of the container.
        /// </summary>
        private float height;

        public HorizontalPaddedContainer(Displayable child, float height)
        {
            this.child = child;
            this.height = height;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return height;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Check if the child is non-fluid and smaller than the allocated space
            if (!child.IsFluidWidth && inRect.width > child.CalcWidth(inRect.height))
            {
                // Create a flex container to hold the child and spacers
                HorizontalFlexContainer row = new HorizontalFlexContainer(0f);

                // Sandwich the child between two spacers
                row.Add(new SpacerWidget());
                row.Add(child);
                row.Add(new SpacerWidget());

                // Draw the container
                row.Draw(inRect);
            }
            else
            {
                // Just draw the child, there's no padding to be done here
                child.Draw(inRect);
            }
        }
    }
}