using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class HorizontalFlexContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidWidth => false;

        /// <summary>
        /// List of contents to draw.
        /// </summary>
        public readonly List<Displayable> Contents;

        /// <summary>
        /// Spacing between elements.
        /// </summary>
        private float spacing;

        /// <summary>
        /// Creates a new <see cref="HorizontalFlexContainer"/> with the given spacing.
        /// </summary>
        public HorizontalFlexContainer(float spacing = 10f)
        {
            this.spacing = spacing;
            
            this.Contents = new List<Displayable>();
        }

        /// <summary>
        /// Creates a new <see cref="HorizontalFlexContainer"/> with the given contents and spacing.
        /// </summary>
        /// <param name="contents">Drawable contents</param>
        /// <param name="spacing">Spacing between elements</param>
        public HorizontalFlexContainer(IEnumerable<Displayable> contents, float spacing = 10f)
        {
            this.Contents = contents.ToList();
            this.spacing = spacing;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Get the width taken up by fixed-width elements
            float fixedWidth = Contents.Where(item => !item.IsFluidWidth).Sum(item => item.CalcWidth(container.height));

            // Divvy out the remaining width to each fluid element
            float remainingWidth = container.width - fixedWidth;
            remainingWidth -= (Contents.Count - 1) * spacing; // Remove spacing between each element
            int fluidItems = Contents.Count(item => item.IsFluidWidth);
            float widthPerFluid = remainingWidth / fluidItems;
            
            // Draw each item
            float xOffset = 0f;
            for (int i = 0; i < Contents.Count; i++)
            {
                Displayable item = Contents[i];
                Rect rect;

                // Give fluid items special treatment
                if (item.IsFluidWidth)
                {
                    // Give the item a container with a share of the remaining width
                    rect = new Rect(
                        x: container.xMin + xOffset,
                        y: container.yMin,
                        width: widthPerFluid,
                        height: container.height
                    );
                }
                else
                {
                    // Give the item a container with fixed height and dynamic width
                    rect = new Rect(
                        x: container.xMin + xOffset,
                        y: container.yMin,
                        width: item.CalcWidth(container.height),
                        height: container.height
                    );
                }

                // Draw the item
                item.Draw(rect);

                // Increment the x offset by the item's width
                xOffset += rect.width;
                
                // Add spacing to the x offset if applicable
                if (i < Contents.Count - 1) xOffset += spacing;
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return FLUID;
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            // Return the sum of each item's width, ignoring fluid items, and the spacing between each
            return Contents.Where(item => !item.IsFluidWidth).Sum(item => item.CalcWidth(height)) + (spacing * (Contents.Count - 1));
        }

        /// <summary>
        /// Adds an <see cref="Displayable"/> item to the container.
        /// </summary>
        /// <param name="item">Drawable item to add</param>
        public void Add(Displayable item)
        {
            Contents.Add(item);
        }
    }
}
