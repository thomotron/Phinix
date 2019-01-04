// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    [StaticConstructorOnStartup]
    internal class ListContainer : Displayable
    {
        /// <summary>
        /// The background texture used for alternate rows.
        /// </summary>
        public static readonly Texture2D alternateBackground = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 1f, 1f, 0.04f));
        
        /// <summary>
        /// Some kind of spacing idk.
        /// </summary>
        public const float SPACE = 10f;

        /// <summary>
        /// Collection of displayable children.
        /// </summary>
        private List<Displayable> children;
        
        /// <summary>
        /// Type of flow child elements will be drawn with.
        /// </summary>
        private ListFlow flow;
        
        /// <summary>
        /// Direction the list will be drawn.
        /// </summary>
        private ListDirection direction;
        
        /// <summary>
        /// Space between elements.
        /// </summary>
        public float spaceBetween = 0f;
        
        /// <summary>
        /// Whether to draw the alternate background texture for every second element.
        /// </summary>
        public bool drawAlternateBackground = false;

        public ListContainer(List<Displayable> children, ListFlow flow = ListFlow.COLUMN, ListDirection direction = ListDirection.NORMAL)
        {
            this.children = children;
            this.flow = flow;
            this.direction = direction;
        }

        public ListContainer() : this(new List<Displayable>())
        {

        }

        public ListContainer(ListFlow flow = ListFlow.COLUMN, ListDirection direction = ListDirection.NORMAL) : this(new List<Displayable>(), flow, direction)
        {

        }

        /// <summary>
        /// Append the given element to the list.
        /// </summary>
        /// <param name="element">Element to add</param>
        public void Add(Displayable element)
        {
            children.Add(element);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            if (IsFluidHeight())
            {
                return FLUID;
            }
            else
            {
                // Return the combined height of each child and the space between them
                return children.Sum((c) => c.CalcHeight(width)) + (children.Count - 1) * spaceBetween;
            }
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            if (IsFluidWidth())
            {
                return FLUID;
            }
            else
            {
                // Return the combined width of each child 
                return children.Sum((c) => c.CalcWidth(height));
            }
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Draw the following as a group, localising the co-ordinate space to the container
            UnityEngine.GUI.BeginGroup(inRect);

            if (flow == ListFlow.COLUMN)
            {
                // Draw the list as a single column
                DrawColumn(inRect);
            }
            else
            {
                // Draw the list as a single row
                DrawRow(inRect);
            }

            // Close the group
            UnityEngine.GUI.EndGroup();
        }

        /// <summary>
        /// Draws the list as a single row within the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void DrawRow(Rect inRect)
        {
            float height = inRect.height;

            // We first calculate the remaining space that will be given to the fluid element
            float takenWidth;
            int countFluidElements = 0;
            if (IsFluidWidth())
            {
                // Get the sum of each child's width
                takenWidth = children.Sum((c) =>
                {
                    float childWidth = c.CalcWidth(height);
                    if (!childWidth.Equals(FLUID))
                    {
                        // Child is not fluid, return its width
                        return childWidth;
                    }
                    else
                    {
                        // Child is fluid, add one to the fluid count and move on
                        countFluidElements++;
                        return 0;
                    }
                });
            }
            else
            {
                // Will never be used anyway
                takenWidth = inRect.width;
            }
            
            // Get the remaining width to be divvied out between each fluid element
            float remainingWidth = inRect.width - takenWidth;
            
            // We remove the width taken by the spaces between elements
            remainingWidth -= (children.Count - 1) * spaceBetween;
            
            // Split the remaining width between each fluid element
            float widthForFluid = remainingWidth / countFluidElements;

            // Set the starting X coord to 0 for a normal list direction or the container width for reverse
            float beginX = direction == ListDirection.NORMAL ? 0 : inRect.width;
            
            // Begin drawing each child element
            int i = 0;
            foreach (Displayable child in children)
            {
                float width = child.CalcWidth(height);
                if (width.Equals(FLUID))
                {
                    // Give the element a share of the remaining width
                    width = widthForFluid;
                }

                // If going from right to left, we first remove the width of the child
                if (direction == ListDirection.OPPOSITE)
                {
                    beginX -= width + spaceBetween;
                }

                // Container for the child element
                Rect childArea = new Rect(beginX, 0, width, height);
                
                // Draw the following as a group
                UnityEngine.GUI.BeginGroup(childArea);
                childArea.x = 0;

                // Check if we should draw an alternate background
                if (drawAlternateBackground && i % 2 == 1)
                {
                    UnityEngine.GUI.DrawTexture(childArea, alternateBackground);
                }

                // Draw the element
                child.Draw(childArea);

                // Close the group
                UnityEngine.GUI.EndGroup();

                // If going from left to right, we then add the width of the child
                if (direction == ListDirection.NORMAL)
                {
                    beginX += width + spaceBetween;
                }

                i++;
            }
        }

        /// <summary>
        /// Draws the list as a single column within the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void DrawColumn(Rect inRect)
        {
            float width = inRect.width;

            // We first calculate the remaining space that will be given to the fluid element
            float takenHeight;
            int countFluidElements = 0;
            if (IsFluidWidth())
            {
                // Get the sum of each child's height
                takenHeight = children.Sum((c) =>
                {
                    float childHeight = c.CalcHeight(width);
                    if (childHeight != FLUID)
                    {
                        // Child is not fluid, return its height
                        return childHeight;
                    }
                    else
                    {
                        // Child is fluid, add one to the fluid count and move on
                        countFluidElements++;
                        return 0;
                    }
                });
            }
            else
            {
                // Will never be used anyway
                takenHeight = inRect.height;
            }

            // Get the remaining height to be divvied out between fluid elements
            float remainingHeight = inRect.height - takenHeight;
            
            // We remove the height taken by the spaces between elements
            remainingHeight -= (children.Count - 1) * spaceBetween;
            
            // Split the remaining width between each fluid element
            float heightForFluid = remainingHeight / countFluidElements;

            // Set the starting Y coord to 0 for a normal list direction or the container height for reverse
            float beginY = direction == ListDirection.NORMAL ? 0 : inRect.height;

            // Begin drawing each child element
            int i = 0;
            foreach (Displayable child in children)
            {
                float height = child.CalcHeight(width);
                if (height == FLUID)
                {
                    // Give the element a share of the remaining height
                    height = heightForFluid;
                }

                // If going from bottom to top, we first remove the height of the child
                if (direction == ListDirection.OPPOSITE)
                {
                    beginY -= height + spaceBetween;
                }

                // Container for the child element
                Rect childArea = new Rect(0, beginY, width, height);
                
                // Draw the following as a group
                UnityEngine.GUI.BeginGroup(childArea);
                childArea.y = 0;

                // Check if we should draw an alternate background
                if (drawAlternateBackground && i % 2 == 1)
                {
                    UnityEngine.GUI.DrawTexture(childArea, alternateBackground);
                }

                // Draw the element
                child.Draw(childArea);

                // Close the group
                UnityEngine.GUI.EndGroup();

                // If going from top to bottom, we then add the height of the child
                if (direction == ListDirection.NORMAL)
                {
                    beginY += height + spaceBetween;
                }

                i++;
            }
        }

        /// <summary>
        /// Returns the number of elements with fluid height.
        /// </summary>
        /// <returns>Number of elements with fluid height</returns>
        private int CountFluidHeight()
        {
            return children.Count((c) => c.IsFluidHeight());
        }

        /// <summary>
        /// Returns the number of elements with fluid width.
        /// </summary>
        /// <returns>Number of elements with fluid width</returns>
        private int CountFluidWidth()
        {
            return children.Count((c) => c.IsFluidWidth());
        }

        /// <inheritdoc />
        public override bool IsFluidHeight()
        {
            if (flow == ListFlow.COLUMN)
            {
                // Return whether any of the elements have fluid height
                return children.Any((c) => c.IsFluidHeight());
            }
            else
            {
                // If the list is a row, it takes all height
                return true;
            }
        }

        /// <inheritdoc />
        public override bool IsFluidWidth()
        {
            if (flow == ListFlow.ROW)
            {
                // Return whether any of the elements have fluid width
                return children.Any((c) => c.IsFluidWidth());
            }
            else
            {
                // If the list is a column, it takes all width
                return true;
            }
        }
    }
}
