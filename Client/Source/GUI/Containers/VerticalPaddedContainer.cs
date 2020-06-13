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
        /// Contents of the container wrapped in another horizontally-padded container.
        /// </summary>
        private VerticalFlexContainer paddedChild;

        /// <summary>
        /// Width of the container.
        /// </summary>
        private float width;

        public VerticalPaddedContainer(Displayable child, float width)
        {
            this.child = child;
            this.width = width;

            // Build a vertically-padded container with the same content
            this.paddedChild = new VerticalFlexContainer(0f);
            this.paddedChild.Add(new SpacerWidget());
            this.paddedChild.Add(child);
            this.paddedChild.Add(new SpacerWidget());
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
                // Draw the child with padding above and below
                paddedChild.Draw(inRect);
            }
            else
            {
                // Just draw the child, there's no padding to be done here
                child.Draw(inRect);
            }
        }

        /// <inheritdoc />
        public override void Update()
        {
            // Just update the child. No need to update the padded one since it contains the same object.
            child.Update();
        }
    }
}