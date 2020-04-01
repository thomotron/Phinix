using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class FittedTextureWidget : Displayable
    {
        /// <summary>
        /// The texture.
        /// </summary>
        private Texture2D texture;

        /// <summary>
        /// Scale of the texture.
        /// </summary>
        private float scale;

        public FittedTextureWidget(Texture2D texture, float scale = 1f)
        {
            this.texture = texture;
            this.scale = scale;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            Widgets.DrawTextureFitted(inRect, texture, scale);
        }
    }
}