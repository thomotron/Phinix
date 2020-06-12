// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Text validator to apply to the value as it is being changed.
        /// </summary>
        private Regex validator;

        /// <summary>
        /// Creates a new <see cref="TextFieldWidget"/> with the given initial text and change handler.
        /// A default validator equivalent to the regular expression <c>[\s\S]*</c> will be applied.
        /// </summary>
        /// <param name="initialText">Initial text to display</param>
        /// <param name="onChange">Callback invoked with the new value when it is changed</param>
        public TextFieldWidget(string initialText, Action<string> onChange) : this(initialText, onChange, new Regex("[\\s\\S]*"))
        {
        }

        /// <summary>
        /// Creates a new <see cref="TextFieldWidget"/> with the given initial text, change handler, and validator.
        /// </summary>
        /// <param name="initialText">Initial text to display</param>
        /// <param name="onChange">Callback invoked with the new value when it is changed</param>
        /// <param name="validator">Text validator to apply to the value as it is being changed</param>
        public TextFieldWidget(string initialText, Action<string> onChange, Regex validator)
        {
            this.text = initialText;
            this.onChange = onChange;
            this.validator = validator;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Draw the text field
            string newText = Widgets.TextField(inRect, text, int.MaxValue, validator);

            // Check if the content has changed
            if (newText != text)
            {
                // Invoke the callback
                onChange?.Invoke(newText);
            }

            // Update the text field
            text = newText;
        }
    }
}
