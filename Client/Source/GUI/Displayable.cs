// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PhiClient.UI
{
    public abstract class Displayable
    {
        public const float FLUID = -1;

        /// <summary>
        /// Draws the object within the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public abstract void Draw(Rect inRect);

        /// <summary>
        /// Returns whether the object will fill any horizontal space it's given.
        /// </summary>
        /// <returns>Whether the object will fill any horizontal space it's given</returns>
        public virtual bool IsFluidWidth()
        {
            return true;
        }

        /// <summary>
        /// Returns whether the object will fill any vertical space it's given.
        /// </summary>
        /// <returns>Whether the object will fill any vertical space it's given</returns>
        public virtual bool IsFluidHeight()
        {
            return true;
        }

        /// <summary>
        /// Calculates the object's width when constrained within the given height.
        /// </summary>
        /// <param name="height">Height to constrain within</param>
        /// <returns>Object's width</returns>
        public virtual float CalcWidth(float height)
        {
            return Displayable.FLUID;
        }

        /// <summary>
        /// Calculates the object's height when constrained within the given width.
        /// </summary>
        /// <param name="width">Width to constrain within</param>
        /// <returns>Object's height</returns>
        public virtual float CalcHeight(float width)
        {
            return Displayable.FLUID;
        }
    }
}
