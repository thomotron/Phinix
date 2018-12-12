// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhiClient.UI
{
    class WidthContainer : Container
    {
        public WidthContainer(Displayable child, float width): base(child, width, Displayable.FLUID)
        {

        }

        public override bool IsFluidWidth()
        {
            return false;
        }
    }
}
