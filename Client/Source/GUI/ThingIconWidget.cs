// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace PhiClient.UI
{
    class ThingIconWidget : Displayable
    {
        public Thing thing;

        public ThingIconWidget(Thing thing)
        {
            this.thing = thing;
        }

        public override void Draw(Rect inRect)
        {
            Widgets.ThingIcon(inRect, thing);
        }
    }
}
