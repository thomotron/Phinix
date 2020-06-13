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
        /// Contents of the container wrapped in another horizontally-padded container.
        /// </summary>
        private HorizontalFlexContainer paddedChild;

        /// <summary>
        /// Height of the container.
        /// </summary>
        private float height;

        public HorizontalPaddedContainer(Displayable child, float height)
        {
            this.child = child;
            this.height = height;

            // Build a horizontally-padded container with the same content
            this.paddedChild = new HorizontalFlexContainer(0f);
            this.paddedChild.Add(new SpacerWidget());
            this.paddedChild.Add(child);
            this.paddedChild.Add(new SpacerWidget());
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
                // Draw the child with padding on either side
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