using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ItemStackRow : IDrawable
    {
        private const float BUTTON_WIDTH = 30f;
        private const float COUNT_FIELD_WIDTH = 70f;
        
        /// <summary>
        /// The item stack this row will display.
        /// </summary>
        private StackedThings itemStack;

        /// <summary>
        /// Height of the row.
        /// </summary>
        private float height;

        /// <summary>
        /// Whether to draw count-modifying buttons.
        /// </summary>
        private bool interactive;

        /// <summary>
        /// Whether to draw an alternate background colour.
        /// </summary>
        private bool alternateBackground;

        /// <summary>
        /// Creates an <c>ItemRow</c> for the given stack of items.
        /// </summary>
        /// <param name="itemStack">Item stack the row is for</param>
        /// <param name="height">Height to draw</param>
        /// <param name="interactive">Whether to display buttons for altering the item stack's selected count</param>
        /// <param name="alternateBackground">Whether to alternate the background of each second row</param>
        public ItemStackRow(StackedThings itemStack, float height, bool interactive = false, bool alternateBackground = false)
        {
            this.itemStack = itemStack;
            this.height = height;
            this.interactive = interactive;
            this.alternateBackground = alternateBackground;
        }
        
        /// <inheritdoc />
        public void Draw(Rect container)
        {
            // Background
            if (alternateBackground) Widgets.DrawHighlight(container);
            
            // Icon
            Rect iconRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.height,
                height: height
            );
            Widgets.ThingIcon(iconRect.ContractedBy(iconRect.width * 0.1f), itemStack.Things.First());
            
            // Set text alignment
            TextAnchor oldAnchorPos = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Item name
            Rect labelRect = new Rect(
                x: iconRect.xMax,
                y: container.yMin,
                width: Text.CalcSize(itemStack.Things.First().def.label.CapitalizeFirst()).x,
                height: height
            );
            Widgets.Label(labelRect, itemStack.Things.First().def.label.CapitalizeFirst());

            if (interactive)
            {
                int oldSelectedCount = itemStack.Selected;
                
                // Add all button
                Rect addAllRect = new Rect(
                    x: container.xMax - (BUTTON_WIDTH + 10f), // Move a further 10f for some nicer-looking padding
                    y: container.yMin,
                    width: BUTTON_WIDTH,
                    height: height
                );
                if (Widgets.ButtonText(addAllRect, "++"))
                {
                    itemStack.Selected = itemStack.Count;
                    
                    Log.Message("Maxing = " + itemStack.Selected);
                }
                
                // Add one button
                Rect addOneRect = new Rect(
                    x: addAllRect.xMin - BUTTON_WIDTH,
                    y: container.yMin,
                    width: BUTTON_WIDTH,
                    height: height
                );
                if (Widgets.ButtonText(addOneRect, "+"))
                {
                    if (itemStack.Selected + 1 <= itemStack.Count) itemStack.Selected++;
                    
                    Log.Message("Adding = " + itemStack.Selected);
                }
                
                // Count text field
                Rect countTextFieldRect = new Rect(
                    x: addOneRect.xMin - COUNT_FIELD_WIDTH,
                    y: container.yMin,
                    width: COUNT_FIELD_WIDTH,
                    height: height
                );
                
                // Remove one button
                Rect removeOneRect = new Rect(
                    x: countTextFieldRect.xMin - BUTTON_WIDTH,
                    y: container.yMin,
                    width: BUTTON_WIDTH,
                    height: height
                );
                if (Widgets.ButtonText(removeOneRect, "-"))
                {
                    if (itemStack.Selected - 1 >= 0) itemStack.Selected--;
                    
                    Log.Message("Taking = " + itemStack.Selected);
                }
                
                // Remove all button
                Rect removeAllRect = new Rect(
                    x: removeOneRect.xMin - BUTTON_WIDTH,
                    y: container.yMin,
                    width: BUTTON_WIDTH,
                    height: height
                );
                if (Widgets.ButtonText(removeAllRect, "--"))
                {
                    itemStack.Selected = 0;
                    
                    Log.Message("Minning = " + itemStack.Selected);
                }
                
                // Logic for the count text field
                string countText = itemStack.Selected.ToString();
                countText = Widgets.TextField(countTextFieldRect, countText, int.MaxValue, new Regex("(^\\d$)")); // Match only digits
                itemStack.Selected = int.Parse(countText);
            }
            else
            {
                // Item count
                Rect countRect = new Rect(
                    x: container.xMax - (Text.CalcSize(itemStack.Selected.ToString()).x + 10f), // Move a further 10f for some nicer-looking padding
                    y: container.yMin,
                    width: Text.CalcSize(itemStack.Count.ToString()).x,
                    height: height
                );
                Widgets.Label(countRect, itemStack.Count.ToString());
            }
            
            // Reset text alignment
            Text.Anchor = oldAnchorPos;
        }

        /// <inheritdoc />
        public float GetHeight(float width)
        {
            return height;
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException"><c>ItemStackRow</c>s are always drawn at full width</exception>
        public float GetWidth(float height)
        {
            throw new NotSupportedException("ItemStackRows are always drawn at full width.");
        }
    }
}