// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class ThingIconWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;
        /// <inheritdoc />
        public override bool IsFluidWidth => false;

        /// <summary>
        /// Thing the icon is for.
        /// </summary>
        public Thing thing;

        /// <summary>
        /// Scale of the icon.
        /// </summary>
        private float scale;

        public ThingIconWidget(Thing thing, float scale = 1f)
        {
            this.thing = thing;
            this.scale = scale;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            Widgets.ThingIcon(inRect.ScaledBy(scale), thing);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return width;
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return height;
        }
    }
}
