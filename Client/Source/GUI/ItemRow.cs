using System;
using System.Net.Mime;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ItemRow : IDrawable
    {
        /// <summary>
        /// The item this row will display.
        /// </summary>
        private Thing thing;
        
        /// <summary>
        /// Height of the row.
        /// </summary>
        private float height;

        /// <summary>
        /// Whether to draw an alternate background colour.
        /// </summary>
        private bool alternateBackground;

        public ItemRow(Thing thing, float height, bool alternateBackground = false)
        {
            this.thing = thing;
            this.height = height;
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
            Widgets.ThingIcon(iconRect.ContractedBy(iconRect.width * 0.1f), thing);
            
            // Set text alignment
            TextAnchor oldAnchorPos = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            // Item name
            Rect labelRect = new Rect(
                x: iconRect.xMax,
                y: container.yMin,
                width: Text.CalcSize(thing.def.label.CapitalizeFirst()).x,
                height: height
            );
            Widgets.Label(labelRect, thing.def.label.CapitalizeFirst());
            
            // Item count
            Rect countRect = new Rect(
                x: container.xMax - (Text.CalcSize(thing.stackCount.ToString()).x + 10f), // Move a further 10f for some nicer-looking padding
                y: container.yMin,
                width: Text.CalcSize(thing.stackCount.ToString()).x,
                height: height
            );
            Widgets.Label(countRect, thing.stackCount.ToString());
            
            // Reset text alignment
            Text.Anchor = oldAnchorPos;
        }

        /// <inheritdoc />
        public float GetHeight(float width)
        {
            return height;
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException"><c>ItemRow</c>s are always drawn at full width</exception>
        public float GetWidth(float height)
        {
            throw new NotSupportedException("ItemRows are always drawn at full width.");
        }
    }
}