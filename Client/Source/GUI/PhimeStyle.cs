using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Colours and small drawing helpers shared by the Phinix window's tabs: count badges, online dots and the gold
    /// accent. One accent colour (the same warm gold as the game's selection highlight) plus green for good news and
    /// grey for everything quiet, so nothing shouts. Buttons use the game's own untinted button texture.
    /// </summary>
    /// <remarks>
    /// Everything here is drawn with plain rectangles, text and one white circle texture (Textures/Phinix/Circle.png),
    /// tinted with <c>GUI.color</c>. Only call these while drawing a window, since textures load on the main thread.
    /// The textures still load the first time they're drawn. The attribute only tells the game's startup check, which
    /// warns about any class with a texture field that doesn't have it, that this class is safe.
    /// </remarks>
    [StaticConstructorOnStartup]
    public static class PhimeStyle
    {
        /// <summary>
        /// Silver as "$1,240", with commas so big values are easy to read. Like the game, cents only show under $10.
        /// </summary>
        public static string Money(this float value)
        {
            string number = value.ToString(value >= 10f || value == 0f ? "N0" : "N2", CultureInfo.InvariantCulture);
            return "$" + number;
        }

        public static readonly Color Gold = new Color(0.90f, 0.77f, 0.40f);
        public static readonly Color GoldHighlight = new Color(1f, 0.94f, 0.5f, 0.18f);
        public static readonly Color GoldCardFill = new Color(0.19f, 0.18f, 0.155f);
        public static readonly Color GoldCardBorder = new Color(0.42f, 0.37f, 0.24f);
        public static readonly Color Green = new Color(0.50f, 0.78f, 0.50f);
        public static readonly Color Muted = new Color(0.55f, 0.56f, 0.57f);
        public static readonly Color Dim = new Color(0.44f, 0.44f, 0.45f);
        public static readonly Color OfflineDot = new Color(0.36f, 0.37f, 0.38f);
        public static readonly Color BadgeRed = new Color(0.71f, 0.25f, 0.23f);
        public static readonly Color BadgeGrey = new Color(0.33f, 0.34f, 0.35f);
        public static readonly Color You = new Color(0.56f, 0.71f, 0.85f);
        public static readonly Color Them = new Color(0.88f, 0.66f, 0.43f);
        public static readonly Color Silver = new Color(0.81f, 0.83f, 0.84f);
        public static readonly Color Warning = new Color(0.79f, 0.48f, 0.45f);
        public static readonly Color Separator = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color CardFill = new Color(0.165f, 0.17f, 0.175f);
        public static readonly Color CardBorder = new Color(0.29f, 0.294f, 0.30f);
        public static readonly Color TileFill = new Color(0.11f, 0.114f, 0.118f);
        public static readonly Color TileBorder = new Color(0.235f, 0.24f, 0.25f);

        private static readonly Color InactiveButtonTint = new Color(0.45f, 0.45f, 0.45f);

        private const float BADGE_HEIGHT = 16f;

        private static Texture2D circle;
        private static Texture2D tradeIcon;

        /// <summary>
        /// A white filled circle, tinted for dots and the round ends of badges.
        /// </summary>
        public static Texture2D Circle => circle ?? (circle = ContentFinder<Texture2D>.Get("Phinix/Circle"));

        /// <summary>
        /// The two-arrows icon from the trade window, used wherever a trade is mentioned.
        /// </summary>
        public static Texture2D TradeIcon => tradeIcon ?? (tradeIcon = ContentFinder<Texture2D>.Get("tradeArrows"));

        /// <summary>
        /// Draws a label in the given colour, font and alignment, then puts all three back how they were.
        /// </summary>
        public static void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = UnityEngine.GUI.color;
            bool previousWrap = Text.WordWrap;

            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = false;
            UnityEngine.GUI.color = color;
            Widgets.Label(rect, text);

            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
            Text.WordWrap = previousWrap;
            UnityEngine.GUI.color = previousColor;
        }

        /// <summary>
        /// Width of a piece of text in the given font, on one line.
        /// </summary>
        public static float TextWidth(string text, GameFont font)
        {
            GameFont previousFont = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = previousFont;
            return width;
        }

        /// <summary>
        /// Shortens text with "..." to fit a width, in the given font.
        /// </summary>
        public static string Fit(string text, float width, GameFont font)
        {
            GameFont previousFont = Text.Font;
            Text.Font = font;
            string fitted = text.Truncate(width);
            Text.Font = previousFont;
            return fitted;
        }

        /// <summary>
        /// Small grey capitals used as a section heading, like "CONVERSATIONS".
        /// </summary>
        public static void Heading(Rect rect, string text) => Label(rect, text.ToUpperInvariant(), Muted, GameFont.Tiny, TextAnchor.MiddleLeft);

        /// <summary>
        /// A round dot, e.g. green for an online player.
        /// </summary>
        public static void Dot(Vector2 center, float size, Color color)
        {
            Color previous = UnityEngine.GUI.color;
            UnityEngine.GUI.color = color;
            UnityEngine.GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), Circle);
            UnityEngine.GUI.color = previous;
        }

        /// <summary>
        /// Text for a count badge: the number, or "99+" past 99.
        /// </summary>
        public static string CountText(int count) => count > 99 ? "99+" : count.ToString();

        /// <summary>
        /// How wide a badge with this text is.
        /// </summary>
        public static float BadgeWidth(string text) => Mathf.Max(BADGE_HEIGHT, TextWidth(text, GameFont.Tiny) + 10f);

        /// <summary>
        /// Draws a pill-shaped count badge with its left edge at <paramref name="x"/>, centred on <paramref name="centerY"/>.
        /// </summary>
        /// <returns>The badge's width</returns>
        public static float Badge(float x, float centerY, string text, Color color)
        {
            float width = BadgeWidth(text);
            Rect rect = new Rect(x, centerY - BADGE_HEIGHT / 2, width, BADGE_HEIGHT);

            // Two circles for the round ends with a block between them
            Color previous = UnityEngine.GUI.color;
            UnityEngine.GUI.color = color;
            UnityEngine.GUI.DrawTexture(new Rect(rect.x, rect.y, BADGE_HEIGHT, BADGE_HEIGHT), Circle);
            UnityEngine.GUI.DrawTexture(new Rect(rect.xMax - BADGE_HEIGHT, rect.y, BADGE_HEIGHT, BADGE_HEIGHT), Circle);
            UnityEngine.GUI.color = previous;
            if (width > BADGE_HEIGHT) Widgets.DrawBoxSolid(new Rect(rect.x + BADGE_HEIGHT / 2, rect.y, width - BADGE_HEIGHT, BADGE_HEIGHT), color);

            Label(rect, text, Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
            return width;
        }

        /// <summary>
        /// A thin gold bar down the left edge of a rect, marking the selected row or our own things.
        /// </summary>
        public static void AccentEdge(Rect rect) => Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), Gold);

        /// <summary>
        /// A filled box with a one-pixel border.
        /// </summary>
        public static void Card(Rect rect, Color fill, Color border) => Widgets.DrawBoxSolidWithOutline(rect, fill, border);

        /// <summary>
        /// A small icon followed by a label, both in the same colour, vertically centred in the rect.
        /// </summary>
        public static void IconLabel(Rect rect, Texture2D icon, string text, Color color, GameFont font = GameFont.Tiny)
        {
            float iconSize = font == GameFont.Tiny ? 12f : 16f;
            Color previous = UnityEngine.GUI.color;
            UnityEngine.GUI.color = color;
            UnityEngine.GUI.DrawTexture(new Rect(rect.x, rect.center.y - iconSize / 2, iconSize, iconSize), icon);
            UnityEngine.GUI.color = previous;
            Label(new Rect(rect.x + iconSize + 4f, rect.y, rect.width - iconSize - 4f, rect.height), text, color, font, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// A button drawn with the game's own untinted texture, greyed out properly when inactive, and with an optional count
        /// badge after the label.
        /// </summary>
        /// <returns>Whether it was clicked (never when inactive)</returns>
        public static bool Button(Rect rect, string label, bool active = true, int badgeCount = 0, bool urgentBadge = true)
        {
            Color previous = UnityEngine.GUI.color;
            UnityEngine.GUI.color = !active ? InactiveButtonTint : Color.white;
            Widgets.DrawButtonGraphic(rect);
            UnityEngine.GUI.color = previous;
            if (active) MouseoverSounds.DoRegion(rect);

            Color textColor = active ? Color.white : Dim;
            if (badgeCount <= 0)
            {
                Label(rect, label, textColor, GameFont.Small, TextAnchor.MiddleCenter);
            }
            else
            {
                // Centre the label and badge together
                string badgeText = CountText(badgeCount);
                float labelWidth = TextWidth(label, GameFont.Small);
                float total = labelWidth + 7f + BadgeWidth(badgeText);
                float x = rect.center.x - total / 2;
                Label(new Rect(x, rect.y, labelWidth + 2f, rect.height), label, textColor, GameFont.Small, TextAnchor.MiddleLeft);
                Badge(x + labelWidth + 7f, rect.center.y, badgeText, urgentBadge ? BadgeRed : BadgeGrey);
            }

            return active && Widgets.ButtonInvisible(rect, doMouseoverSound: false);
        }

        private static readonly Regex colorTag = new Regex("<color=#?([0-9a-fA-F]{6})([0-9a-fA-F]{2})?>", RegexOptions.Compiled);

        /// <summary>
        /// The colour a player picked for their display name (the first colour tag in it), or <paramref name="fallback"/>
        /// if they didn't pick one. Lets us show names in their colour without letting size or bold tags into our layout.
        /// </summary>
        public static Color NameColor(string displayName, Color fallback)
        {
            if (string.IsNullOrEmpty(displayName)) return fallback;

            Match match = colorTag.Match(displayName);
            if (!match.Success) return fallback;

            int rgb = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        /// <summary>
        /// A colour as "#rrggbb" for rich text colour tags.
        /// </summary>
        public static string Hex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}",
                Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255), Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255), Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255));
        }

        /// <summary>
        /// Wraps text in a rich text colour tag.
        /// </summary>
        public static string Colored(string text, Color color) => "<color=" + Hex(color) + ">" + text + "</color>";

        /// <summary>
        /// "opened 5 minutes ago", "opened yesterday" and so on, from a Unix time in seconds.
        /// </summary>
        public static string OpenedAgo(long unixSeconds)
        {
            TimeSpan age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            if (age.TotalMinutes < 1) return "Phinix_time_openedJustNow".Translate();
            if (age.TotalHours < 1) return plural("Phinix_time_openedMinute", "Phinix_time_openedMinutes", (int) age.TotalMinutes);
            if (age.TotalDays < 1) return plural("Phinix_time_openedHour", "Phinix_time_openedHours", (int) age.TotalHours);
            if (age.TotalDays < 2) return "Phinix_time_openedYesterday".Translate();
            return plural("Phinix_time_openedDay", "Phinix_time_openedDays", (int) age.TotalDays);
        }

        private static string plural(string oneKey, string manyKey, int count) => count == 1 ? oneKey.Translate().ToString() : manyKey.Translate(count).ToString();
    }
}
