using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhinixClient
{
    public class HorizontalFlexContainer : IDrawable
    {
        /// <summary>
        /// List of contents to draw.
        /// </summary>
        private List<IDrawable> contents;

        /// <summary>
        /// Height of the flex container.
        /// </summary>
        private float height;

        /// <summary>
        /// Creates a new <c>HorizontalFlexContainer</c> with the given height.
        /// </summary>
        /// <param name="height">Height of the container</param>
        public HorizontalFlexContainer(float height)
        {
            this.contents = new List<IDrawable>();
        }
        
        /// <summary>
        /// Creates a new <c>HorizontalFlexContainer</c> with the given height and contents.
        /// </summary>
        /// <param name="height">Height of the container</param>
        /// <param name="contents">Drawable contents</param>
        public HorizontalFlexContainer(float height, IEnumerable<IDrawable> contents)
        {
            this.height = height;
            this.contents = contents.ToList();
        }
        
        /// <inheritdoc />
        public void Draw(Rect container)
        {
            // Draw each item
            float xOffset = 0f;
            foreach (IDrawable item in contents)
            {
                // Draw the item with fixed height and dynamic width
                Rect rect = new Rect(
                    x: container.xMin,
                    y: container.yMin + xOffset,
                    width: item.GetWidth(height),
                    height: height
                );
                item.Draw(rect);
                
                // Increment the x offset by the item's width
                xOffset += rect.width;
            }
        }

        /// <inheritdoc />
        public float GetHeight(float width)
        {
            return height;
        }

        /// <inheritdoc />
        public float GetWidth(float height)
        {
            // Calculate the height of each item
            float width = 0f;
            foreach (IDrawable item in contents)
            {
                // Increment width by the item's width
                width += item.GetWidth(height);
            }
            
            return width;
        }

        /// <summary>
        /// Adds an <c>IDrawable</c> item to the container.
        /// </summary>
        /// <param name="item">Drawable item to add</param>
        public void Add(IDrawable item)
        {
            contents.Add(item);
        }
    }
}