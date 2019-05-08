using System;
using System.Linq;
using System.Text.RegularExpressions;
using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ItemStackRow : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        private const float DEFAULT_SPACING = 10f;
        private const float RIGHT_PADDING = 5f;

        private const float ICON_WIDTH = 30f;
        private const float BUTTON_WIDTH = 40f;
        private const float COUNT_FIELD_WIDTH = 70f;
        private const float AVAILABLE_COUNT_WIDTH = 50f;
        
        /// <summary>
        /// The item stack this row will display.
        /// </summary>
        private StackedThings itemStack;

        /// <summary>
        /// Whether to draw count-modifying buttons.
        /// </summary>
        private bool interactive;

        /// <summary>
        /// Whether to draw an alternate background colour.
        /// </summary>
        private bool alternateBackground;

        /// <summary>
        /// Callback invoked when the number of selected items has changed.
        /// </summary>
        private Action<int> onSelectedChanged;

        /// <summary>
        /// Creates an <c>ItemRow</c> for the given stack of items.
        /// </summary>
        /// <param name="itemStack">Item stack the row is for</param>
        /// <param name="interactive">Whether to display buttons for altering the item stack's selected count</param>
        /// <param name="alternateBackground">Whether to alternate the background of each second row</param>
        /// <param name="onSelectedChanged">Callback invoked when the number of selected items has changed</param>
        public ItemStackRow(StackedThings itemStack, bool interactive = false, bool alternateBackground = false, Action<int> onSelectedChanged = null)
        {
            this.itemStack = itemStack;
            this.interactive = interactive;
            this.alternateBackground = alternateBackground;
            this.onSelectedChanged = onSelectedChanged;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect container)
        {
            // Background
            if (alternateBackground) Widgets.DrawHighlight(container);
            
            // Create a row to hold everything
            HorizontalFlexContainer row = new HorizontalFlexContainer(DEFAULT_SPACING);
            
            // Icon
            row.Add(
                new VerticalPaddedContainer(
                    new ThingIconWidget(
                        thing: itemStack.Things.First(),
                        scale: 0.9f
                    ),
                    ICON_WIDTH
                )
            );
            
            // Item name
            row.Add(
                new TextWidget(
                    text: itemStack.Things.First().LabelCapNoCount,
                    anchor: TextAnchor.MiddleLeft
                )
            );

            if (interactive)
            {
                // Add some padding to right-align the buttons
                row.Add(new SpacerWidget());
                
                // -100 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "-100",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected -= 100) < itemStack.Count)
                                {
                                    itemStack.Selected = 0;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
                
                // -10 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "-10",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected -= 10) < itemStack.Count)
                                {
                                    itemStack.Selected = 0;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
                
                // -1 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "-1",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected -= 1) < itemStack.Count)
                                {
                                    itemStack.Selected = 0;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
                
                // Count text field
                row.Add(
                    new Container(
                        new TextFieldWidget(
                            text: itemStack.Selected.ToString(),
                            onChange: (countText) =>
                            {
                                if (int.TryParse(countText, out int result))
                                {
                                    if (result < 0)
                                    {
                                        itemStack.Selected = 0;
                                    }
                                    else if (result > itemStack.Count)
                                    {
                                        itemStack.Selected = itemStack.Count;
                                    }
                                    else
                                    {
                                        itemStack.Selected = result;
                                    }
                                }
                                else
                                {
                                    itemStack.Selected = 0;
                                }
                            }
                        ),
                        width: COUNT_FIELD_WIDTH
                    )
                );
                
                // Available count
                row.Add(
                    new Container(
                        new TextWidget(
                            text: "/ " + itemStack.Count,
                            anchor: TextAnchor.MiddleLeft
                        ),
                        width: AVAILABLE_COUNT_WIDTH
                    )
                );
                
                // +1 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "+1",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected += 1) > itemStack.Count)
                                {
                                    itemStack.Selected = itemStack.Count;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
                
                // +10 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "+10",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected += 10) > itemStack.Count)
                                {
                                    itemStack.Selected = itemStack.Count;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
                
                // +100 button
                row.Add(
                    new Container(
                        new ButtonWidget(
                            label: "+100",
                            clickAction: () =>
                            {
                                if ((itemStack.Selected += 100) > itemStack.Count)
                                {
                                    itemStack.Selected = itemStack.Count;
                                }
                            }
                        ),
                        width: BUTTON_WIDTH
                    )
                );
            }
            else
            {
                // Item count
                row.Add(
                    new Container(
                        new TextWidget(
                            text: itemStack.Count.ToString(),
                            anchor: TextAnchor.MiddleRight
                        ),
                        width: COUNT_FIELD_WIDTH
                    )
                );
            }
            
            // Add some padding to keep off the edge
            row.Add(new SpacerWidget(RIGHT_PADDING));

            // Get a copy of the number of selected items
            int oldSelectedCount = itemStack.Selected;
            
            // Draw the row
            row.Draw(container);

            // Check if the number of selected items has changed
            if (itemStack.Selected != oldSelectedCount)
            {
                // Invoke the selected items changed callback
                onSelectedChanged?.Invoke(itemStack.Selected);
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            // Get the text we will be calculating the height of
            string text = itemStack.Things.First().Label.CapitalizeFirst();
            
            // Check if we should factor in the interactive-only buttons
            if (interactive)
            {
                // Get the remaining width after subtracting all fixed-width elements and spacing
                float remainingWidth = width - (ICON_WIDTH + (BUTTON_WIDTH * 6) + COUNT_FIELD_WIDTH + RIGHT_PADDING + (DEFAULT_SPACING * 9));
                
                // Return the height required by the text
                return Text.CalcHeight(text, remainingWidth);
            }
            else
            {
                // Get the remaining width after subtracting all fixed-width elements and spacing
                float remainingWidth = width - (ICON_WIDTH + COUNT_FIELD_WIDTH + (DEFAULT_SPACING * 2));
                
                // Return the height required by the text
                return Text.CalcHeight(text, remainingWidth);
            }
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return FLUID;
        }
    }
}