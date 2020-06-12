// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using UnityEngine;

namespace PhinixClient.GUI
{
    public abstract class Displayable
    {
        public const float FLUID = -1;

        /// <summary>
        /// Whether the object will fill any horizontal space it's given.
        /// </summary>
        public virtual bool IsFluidWidth => true;

        /// <summary>
        /// Whether the object will fill any vertical space it's given.
        /// </summary>
        public virtual bool IsFluidHeight => true;

        /// <summary>
        /// Draws the object within the given container.
        /// </summary>
        /// <remarks>
        /// Only put logic pertinent to drawing in here; the element should be generated and ready to draw before this method is called.
        /// This is to reduce lag since elements are drawn on the main Unity thread.
        /// </remarks>
        /// <param name="inRect">Container to draw within</param>
        public abstract void Draw(Rect inRect);

        /// <summary>
        /// Refreshes the object's data.
        /// When called, it should also be called on any child <see cref="Displayable"/> elements.
        /// </summary>
        public virtual void Update() {}

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
