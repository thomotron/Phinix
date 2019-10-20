// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

namespace PhinixClient.GUI
{
    internal class WidthContainer : Container
    {
        /// <inheritdoc />
        public override bool IsFluidWidth => false;
        
        public WidthContainer(Displayable child, float width): base(child, width, Displayable.FLUID)
        {

        }
    }
}
