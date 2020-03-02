// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class TextFieldWidget : Displayable
    {
        /// <summary>
        /// The field's text content.
        /// </summary>
        private string text;
        
        /// <summary>
        /// Callback invoked when the field's text changes
        /// </summary>
        private Action<string> onChange;

        public TextFieldWidget(string text, Action<string> onChange)
        {
            this.text = text;
            this.onChange = onChange;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Draw the text field
            string newText = Widgets.TextField(inRect, text);
            
            // Check if the content has changed
            if (newText != text)
            {
                // Invoke the callback
                onChange(newText);
            }
        }
    }
}
