using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhinixClient.GUI
{
    public class VerticalFlexContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        /// <summary>
        /// List of contents to draw.
        /// </summary>
        private List<Displayable> contents;

        /// <summary>
        /// Spacing between elements.
        /// </summary>
        private float spacing;

        /// <summary>
        /// Creates a new <c>VerticalFlexContainer</c> with the given width.
        /// </summary>
        public VerticalFlexContainer(float spacing = 10f)
        {
            this.spacing = spacing;
            
            this.contents = new List<Displayable>();
        }
        
        /// <summary>
        /// Creates a new <c>VerticalFlexContainer</c> with the given width and contents.
        /// </summary>
        /// <param name="contents">Drawable contents</param>
        public VerticalFlexContainer(IEnumerable<Displayable> contents, float spacing = 10f)
        {
            this.contents = contents.ToList();
            this.spacing = spacing;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Get the height taken up by fixed-height elements
            float fixedHeight = contents.Where(item => !item.IsFluidHeight).Sum(item => item.CalcHeight(container.width));

            // Divvy out the remaining height to each fluid element
            float remainingHeight = container.height - fixedHeight;
            int fluidItems = contents.Count(item => item.IsFluidHeight);
            float heightPerFluid = remainingHeight / fluidItems;
            
            // Draw each item
            float yOffset = 0f;
            for (int i = 0; i < contents.Count; i++)
            {
                Displayable item = contents[i];
                Rect rect;

                // Give fluid items special treatment
                if (item.IsFluidHeight)
                {
                    // Give the item a container with a share of the remaining height
                    rect = new Rect(
                        x: container.xMin,
                        y: container.yMin + yOffset,
                        width: container.width,
                        height: heightPerFluid
                    );
                }
                else
                {
                    // Give the item a container with fixed width and dynamic height
                    rect = new Rect(
                        x: container.xMin,
                        y: container.yMin + yOffset,
                        width: container.width,
                        height: item.CalcHeight(container.width)
                    );
                }

                // Draw the item
                item.Draw(rect);

                // Increment the y offset by the item's height
                yOffset += rect.height;
                
                // Add spacing to the y offset if applicable
                if (i < contents.Count - 1) yOffset += spacing;
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            // Return the sum of each item's height, ignoring fluid items
            return contents.Where(item => !item.IsFluidHeight).Sum(item => item.CalcHeight(width));
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return FLUID;
        }

        /// <summary>
        /// Adds an <c>IDrawable</c> item to the container.
        /// </summary>
        /// <param name="item">Drawable item to add</param>
        public void Add(Displayable item)
        {
            contents.Add(item);
        }
    }
}