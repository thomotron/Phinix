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

        public ThingIconWidget(Thing thing)
        {
            this.thing = thing;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            Widgets.ThingIcon(inRect, thing);
        }
    }
}
