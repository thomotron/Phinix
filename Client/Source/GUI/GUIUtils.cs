using System;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Common utility methods for working with GUI elements.
    /// </summary>
    public static class GUIUtils
    {
        /// <summary>
        /// Previous state of <see cref="Text.Anchor"/>.
        /// </summary>
        private static TextAnchor textAnchor;
        /// <summary>
        /// Saved state of <see cref="Text.Font"/>.
        /// </summary>
        private static GameFont fontSize;

        /// <summary>
        /// Returns a new rect translated by the given X and Y offsets.
        /// </summary>
        /// <param name="rect">Source <see cref="Rect"/></param>
        /// <param name="xOffset">X axis offset</param>
        /// <param name="yOffset">Y axis offset</param>
        /// <returns>New rect translated by the given X and Y offsets</returns>
        public static Rect TranslatedBy(this Rect rect, float xOffset = 0f, float yOffset = 0f)
        {
            Vector2 newPosition = new Vector2(rect.x + xOffset, rect.y + yOffset);
            return new Rect(newPosition, rect.size);
        }

        /// <summary>
        /// Saves the current text alignment and font size.
        /// </summary>
        /// <seealso cref="RestoreTextFormat"/>
        public static void SaveTextFormat()
        {
            textAnchor = Text.Anchor;
            fontSize = Text.Font;
        }

        /// <summary>
        /// Restores the text alignment and font size previously saved by <see cref="SaveTextFormat"/>.
        /// </summary>
        public static void RestoreTextFormat()
        {
            Text.Anchor = textAnchor;
            Text.Font = fontSize;
        }

        /// <summary>
        /// Returns <paramref name="value"/> clamped to the inclusive range of <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        /// <param name="value">The value to be clamped</param>
        /// <param name="min">The lower bound of the result</param>
        /// <param name="max">The upper bound of the result</param>
        /// <returns><paramref name="value"/> clamped to the inclusive range of <paramref name="min"/> and <paramref name="max"/></returns>
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Returns a string representation of <paramref name="value"/> shortened to the nearest SI prefix.
        /// </summary>
        /// <param name="value">The value to be converted</param>
        /// <param name="precision">The number of decimal places to retain</param>
        /// <returns>String representation of <paramref name="value"/> shortened to the nearest SI prefix</returns>
        public static string ToStringSI(this int value, int precision = 2)
        {
            char[] prefixes = { 'k', 'M', 'G', 'T', 'P', 'E', 'Z' };

            // Numbers less than 1000 don't need a suffix added
            if (value < 1000) return value.ToString();

            // Progressively divide value down until it's less than 1000, then append the corresponding prefix letter
            double quotient = value;
            foreach (char prefix in prefixes)
            {
                quotient /= 1000d;
                if (quotient < 1000) return $"{Math.Round(quotient, precision)}{prefix}";
            }

            // Larger than the largest prefix available, just use that
            return $"{Math.Round(quotient, precision)}{prefixes[prefixes.Length]}";
        }
    }
}