using UnityEngine;

namespace PhinixClient.GUI
{
    public class BlankWidget : Displayable
    {
        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Don't draw anything, just return
            return;
        }
    }
}