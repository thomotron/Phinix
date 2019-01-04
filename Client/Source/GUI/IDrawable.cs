using UnityEngine;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Interface for drawing custom GUI widgets.
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// Draws the object within the given container.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        void Draw(Rect container);

        /// <summary>
        /// Calculates the height of the object when constrained within the given width.
        /// </summary>
        /// <param name="width">Maximum width</param>
        /// <returns>Calculated height</returns>
        float GetHeight(float width);
        
        /// <summary>
        /// Calculates the width of the object when constrained within the given height.
        /// </summary>
        /// <param name="height">Maximum height</param>
        /// <returns>Calculated width</returns>
        float GetWidth(float height);
    }
}