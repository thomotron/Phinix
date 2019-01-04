using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhinixClient.GUI
{
    public class VerticalFlexContainer : IDrawable
    {
        /// <summary>
        /// List of contents to draw.
        /// </summary>
        private List<IDrawable> contents;

        /// <summary>
        /// Width of the flex container.
        /// </summary>
        private float width;

        /// <summary>
        /// Creates a new <c>VerticalFlexContainer</c> with the given width.
        /// </summary>
        /// <param name="width">Width of the container</param>
        public VerticalFlexContainer(float width)
        {
            this.width = width;
            this.contents = new List<IDrawable>();
        }
        
        /// <summary>
        /// Creates a new <c>VerticalFlexContainer</c> with the given width and contents.
        /// </summary>
        /// <param name="width">Width of the container</param>
        /// <param name="contents">Drawable contents</param>
        public VerticalFlexContainer(float width, IEnumerable<IDrawable> contents)
        {
            this.width = width;
            this.contents = contents.ToList();
        }
        
        /// <inheritdoc />
        public void Draw(Rect container)
        {
            // Draw each item
            float yOffset = 0f;
            foreach (IDrawable item in contents)
            {
                // Draw the item with fixed width and dynamic height
                Rect rect = new Rect(
                    x: container.xMin,
                    y: container.yMin + yOffset,
                    width: width,
                    height: item.GetHeight(width)
                );
                item.Draw(rect);
                
                // Increment the y offset by the item's height
                yOffset += rect.height;
            }
        }

        /// <inheritdoc />
        public float GetHeight(float width)
        {
            // Calculate the height of each item
            float height = 0f;
            foreach (IDrawable item in contents)
            {
                // Increment height by the item's height
                height += item.GetHeight(width);
            }

            return height;
        }

        /// <inheritdoc />
        public float GetWidth(float height)
        {
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