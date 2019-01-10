// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    internal class ThingIconWidget : Displayable
    {
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
    }
}
