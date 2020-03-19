using System;
using UnityEngine;

namespace PhinixClient.GUI
{
    public class ConditionalContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => currentChild.IsFluidHeight;

        /// <inheritdoc />
        public override bool IsFluidWidth => currentChild.IsFluidWidth;

        /// <summary>
        /// The condition used to determine which child to display.
        /// </summary>
        private Func<bool> condition;

        /// <summary>
        /// The child that is active when <see cref="condition"/> is <code>true</code>.
        /// </summary>
        private Displayable childIfTrue;

        /// <summary>
        /// The child that is active when <see cref="condition"/> is <code>false</code>.
        /// </summary>
        private Displayable childIfFalse;

        /// <summary>
        /// The current active child.
        /// </summary>
        private Displayable currentChild => condition() ? childIfTrue : childIfFalse;

        public ConditionalContainer(Displayable childIfTrue, Displayable childIfFalse, Func<bool> condition)
        {
            this.childIfTrue = childIfTrue;
            this.childIfFalse = childIfFalse;
            this.condition = condition;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            currentChild.Draw(inRect);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return currentChild.CalcHeight(width);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return currentChild.CalcWidth(height);
        }
    }
}