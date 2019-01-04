// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class CheckboxLabeledWidget : Displayable
    {
        private const float CHECKBOX_HEIGHT = 40f;

        /// <summary>
        /// Label text displayed next to the checkbox.
        /// </summary>
        private string label;
        
        /// <summary>
        /// Whether the checkbox is checked.
        /// </summary>
        private bool isChecked;

        /// <summary>
        /// Callback invoked when the checkbox state changes. 
        /// </summary>
        private Action<bool> onChange;

        public CheckboxLabeledWidget(string label, bool isChecked, Action<bool> onChange)
        {
            this.label = label;
            this.isChecked = isChecked;
            this.onChange = onChange;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Get a copy of the old checked state to compare with later
            bool oldValue = isChecked;
            
            // Draw the checkbox
            Widgets.CheckboxLabeled(inRect, label, ref isChecked);

            // Check if the checked state has changed
            if (oldValue != isChecked)
            {
                // Invoke the callback with the checked state
                onChange(isChecked);
            }
        }

        /// <inheritdoc />
        public override bool IsFluidHeight()
        {
            return false;
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return CHECKBOX_HEIGHT;
        }
    }
}
