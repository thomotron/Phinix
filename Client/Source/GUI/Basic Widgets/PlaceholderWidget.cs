using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class PlaceholderWidget : Displayable
    {
        /// <summary>
        /// Text displayed in the centre of the placeholder.
        /// </summary>
        private string text;
        
        /// <summary>
        /// Creates a <see cref="PlaceholderWidget"/> with the given text.
        /// </summary>
        /// <param name="text">Text to display</param>
        public PlaceholderWidget(string text = null)
        {
            this.text = text;
        }
        
        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Background
            Widgets.DrawMenuSection(inRect);
            
            // Text
            if (text != null)
            {
                Widgets.NoneLabelCenteredVertically(inRect, text);
            }
        }
    }
}