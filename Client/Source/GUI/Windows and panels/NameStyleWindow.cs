using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PhinixClient.GUI;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Lets the player colour their name without writing the formatting by hand: one colour or a gradient of up to
    /// four, bold and italic, with a preview. Applying it writes the name as rich text, the same as typing the tags.
    /// </summary>
    /// <remarks>
    /// A gradient gives each letter its own colour tag, so the raw name grows quickly. The server allows ten raw
    /// characters per visible one (see <c>ServerUserManager</c>), which is plenty, but very long names are warned about.
    /// </remarks>
    public class NameStyleWindow : Window
    {
        private const float SPACING = 10f;
        private const float ROW_HEIGHT = 30f;
        private const float SWATCH_SIZE = 30f;
        private const float WHEEL_SIZE = 170f;
        private const float BUTTON_WIDTH = 120f;
        private const float PREVIEW_HEIGHT = 40f;
        private const int MAX_STOPS = 4;

        /// <summary>
        /// The most colour tags a gradient uses, so a long name's formatting stays well inside the server's limit.
        /// </summary>
        private const int GRADIENT_STEPS = 24;

        /// <summary>
        /// The most raw characters a name may have, matching the server's check with its default name length.
        /// </summary>
        private const int MAX_RAW_LENGTH = 1000;

        public override Vector2 InitialSize => new Vector2(520f, 460f);

        private static readonly Regex colourTagPattern = new Regex("<color=(#[0-9a-fA-F]{6,8})>", RegexOptions.Compiled);

        private readonly Action<string> onApply;

        private string name;
        private bool bold;
        private bool italic;
        private bool gradient;
        private readonly List<Color> stops = new List<Color>();
        private int selectedStop;
        private string hexBuffer;
        private bool draggingWheel;

        /// <param name="currentName">The name as it is now, which may already have formatting</param>
        /// <param name="onApply">Called with the new, formatted name when the player applies it</param>
        public NameStyleWindow(string currentName, Action<string> onApply)
        {
            this.onApply = onApply;

            // Start from what the name already has: its text, bold and italic, and its first and last colours
            name = TextHelper.StripRichText(currentName ?? "");
            bold = currentName != null && currentName.Contains("<b>");
            italic = currentName != null && currentName.Contains("<i>");
            List<Color> found = colourTagPattern.Matches(currentName ?? "").Cast<Match>()
                .Select(match => ColorUtility.TryParseHtmlString(match.Groups[1].Value, out Color colour) ? colour : Color.white)
                .ToList();
            if (found.Count == 0)
            {
                stops.Add(new Color(0.95f, 0.75f, 0.30f));
                stops.Add(new Color(0.85f, 0.30f, 0.30f));
            }
            else
            {
                stops.Add(found.First());
                stops.Add(found.Count > 1 ? found.Last() : found.First());
                gradient = found.Count > 1 && found.First() != found.Last();
            }
            hexBuffer = toHex(stops[0]);

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width - 30f, 35f), "Phinix_nameStyle_title".Translate());
            Text.Font = GameFont.Small;
            y += 35f + SPACING;

            // Left: the name, styles and colour stops. Right: the colour wheel and brightness for the chosen stop
            float leftWidth = inRect.width - WHEEL_SIZE - SPACING;
            float leftX = inRect.x;
            float rightX = inRect.x + leftWidth + SPACING;
            float columnTop = y;

            // Name
            name = Widgets.TextField(new Rect(leftX, y, leftWidth, ROW_HEIGHT), name);
            y += ROW_HEIGHT + SPACING;

            // Bold and italic
            float halfWidth = (leftWidth - SPACING) / 2f;
            Widgets.CheckboxLabeled(new Rect(leftX, y, halfWidth, ROW_HEIGHT), "Phinix_nameStyle_bold".Translate(), ref bold);
            Widgets.CheckboxLabeled(new Rect(leftX + halfWidth + SPACING, y, halfWidth, ROW_HEIGHT), "Phinix_nameStyle_italic".Translate(), ref italic);
            y += ROW_HEIGHT + SPACING;

            // One colour or a gradient, the chosen one underlined in gold like the tabs
            Rect solidRect = new Rect(leftX, y, halfWidth, ROW_HEIGHT);
            Rect gradientRect = new Rect(leftX + halfWidth + SPACING, y, halfWidth, ROW_HEIGHT);
            if (PhimeStyle.Button(solidRect, "Phinix_nameStyle_solid".Translate()))
            {
                gradient = false;
                selectStop(0);
            }
            if (PhimeStyle.Button(gradientRect, "Phinix_nameStyle_gradient".Translate())) gradient = true;
            Rect chosenRect = gradient ? gradientRect : solidRect;
            Widgets.DrawBoxSolid(new Rect(chosenRect.x + 12f, chosenRect.yMax - 4f, chosenRect.width - 24f, 2f), PhimeStyle.Gold);
            y += ROW_HEIGHT + SPACING;

            // Colour stops: click one to edit it. A gradient can have up to four, added and removed at the end
            int shownStops = gradient ? stops.Count : 1;
            float x = leftX;
            for (int i = 0; i < shownStops; i++)
            {
                Rect swatchRect = new Rect(x, y, SWATCH_SIZE, SWATCH_SIZE);
                Widgets.DrawBoxSolid(swatchRect, stops[i]);
                if (i == selectedStop) Widgets.DrawBox(swatchRect.ExpandedBy(2f), 2);
                else if (Mouse.IsOver(swatchRect)) Widgets.DrawBox(swatchRect);
                if (Widgets.ButtonInvisible(swatchRect)) selectStop(i);
                x += SWATCH_SIZE + 8f;
            }
            if (gradient && stops.Count < MAX_STOPS)
            {
                Rect addRect = new Rect(x, y, SWATCH_SIZE, SWATCH_SIZE);
                TooltipHandler.TipRegion(addRect, "Phinix_nameStyle_addColour".Translate());
                if (PhimeStyle.Button(addRect, "+"))
                {
                    stops.Add(stops.Last());
                    selectStop(stops.Count - 1);
                }
                x += SWATCH_SIZE + 8f;
            }
            if (gradient && stops.Count > 2)
            {
                Rect removeRect = new Rect(x, y, SWATCH_SIZE, SWATCH_SIZE);
                TooltipHandler.TipRegion(removeRect, "Phinix_nameStyle_removeColour".Translate());
                if (PhimeStyle.Button(removeRect, "-"))
                {
                    stops.RemoveAt(stops.Count - 1);
                    selectStop(Mathf.Min(selectedStop, stops.Count - 1));
                }
            }
            y += SWATCH_SIZE + SPACING;

            // Hex code for the chosen stop, so colours can be pasted in
            Widgets.Label(new Rect(leftX, y, 40f, ROW_HEIGHT), "Phinix_nameStyle_hex".Translate());
            string newHex = Widgets.TextField(new Rect(leftX + 40f, y, 100f, ROW_HEIGHT), hexBuffer);
            if (newHex != hexBuffer)
            {
                hexBuffer = newHex;
                string candidate = hexBuffer.StartsWith("#") ? hexBuffer : "#" + hexBuffer;
                if (candidate.Length == 7 && ColorUtility.TryParseHtmlString(candidate, out Color parsed)) stops[selectedStop] = parsed;
            }
            y += ROW_HEIGHT;

            // Colour wheel and brightness. The wheel picks hue and saturation; the slider picks how bright
            Color.RGBToHSV(stops[selectedStop], out float hue, out float saturation, out float brightness);
            Color picked = stops[selectedStop];
            Widgets.HSVColorWheel(new Rect(rightX, columnTop, WHEEL_SIZE, WHEEL_SIZE), ref picked, ref draggingWheel, 1f);
            Color.RGBToHSV(picked, out hue, out saturation, out _);
            float newBrightness = brightness;
            Widgets.HorizontalSlider(new Rect(rightX, columnTop + WHEEL_SIZE + SPACING, WHEEL_SIZE, ROW_HEIGHT), ref newBrightness, new FloatRange(0.15f, 1f), "Phinix_nameStyle_brightness".Translate());
            Color result = Color.HSVToRGB(hue, saturation, newBrightness);
            if (toHex(result) != toHex(stops[selectedStop]))
            {
                stops[selectedStop] = result;
                hexBuffer = toHex(result);
            }

            // Preview, on a dark box like the chat
            string styled = Build();
            y = Mathf.Max(y, columnTop + WHEEL_SIZE + SPACING + ROW_HEIGHT) + SPACING;
            Rect previewRect = new Rect(inRect.x, y, inRect.width, PREVIEW_HEIGHT);
            Widgets.DrawMenuSection(previewRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(previewRect, styled);
            Text.Anchor = TextAnchor.UpperLeft;
            y += PREVIEW_HEIGHT + 4f;

            // Too long for the server once each letter has its colour
            bool tooLong = styled.Length > MAX_RAW_LENGTH;
            if (tooLong) PhimeStyle.Label(new Rect(inRect.x, y, inRect.width, 22f), "Phinix_nameStyle_tooLong".Translate(), PhimeStyle.Warning);

            // Apply and cancel
            Rect applyRect = new Rect(inRect.xMax - BUTTON_WIDTH, inRect.yMax - ROW_HEIGHT, BUTTON_WIDTH, ROW_HEIGHT);
            Rect cancelRect = new Rect(applyRect.x - SPACING - BUTTON_WIDTH, applyRect.y, BUTTON_WIDTH, ROW_HEIGHT);
            if (PhimeStyle.Button(cancelRect, "Phinix_nameStyle_cancel".Translate())) Close();
            bool canApply = !tooLong && !string.IsNullOrWhiteSpace(name);
            if (PhimeStyle.Button(applyRect, "Phinix_nameStyle_apply".Translate(), active: canApply) && canApply)
            {
                onApply(styled);
                Close();
            }
        }

        /// <summary>
        /// Writes the name with its styles as rich text: one colour tag for a solid colour, or a tag per run of letters
        /// sharing a colour for a gradient. Spaces take no colour of their own.
        /// </summary>
        private string Build()
        {
            string text = name.Replace("<", "").Replace(">", "");
            StringBuilder builder = new StringBuilder();

            if (!gradient || stops.Count < 2 || text.Length < 2)
            {
                builder.Append("<color=").Append(toHex(stops[0])).Append('>').Append(text).Append("</color>");
            }
            else
            {
                // Spread the stops evenly over the letters (spaces are skipped so they don't stretch the gradient).
                // Long names get the gradient in steps of a few letters each, at most GRADIENT_STEPS of them, which
                // keeps the name's formatting short
                int letters = text.Count(c => !char.IsWhiteSpace(c));
                int steps = Mathf.Min(letters, GRADIENT_STEPS);
                int letter = 0;
                string runHex = null;
                foreach (char c in text)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        builder.Append(c);
                        continue;
                    }

                    int step = letter * steps / letters;
                    string hex = toHex(sample(steps <= 1 ? 0f : step / (float) (steps - 1)));
                    if (hex != runHex)
                    {
                        if (runHex != null) builder.Append("</color>");
                        builder.Append("<color=").Append(hex).Append('>');
                        runHex = hex;
                    }
                    builder.Append(c);
                    letter++;
                }
                if (runHex != null) builder.Append("</color>");
            }

            string styled = builder.ToString();
            if (italic) styled = "<i>" + styled + "</i>";
            if (bold) styled = "<b>" + styled + "</b>";
            return styled;
        }

        /// <summary>
        /// The gradient's colour at <paramref name="t"/>, from 0 (first stop) to 1 (last).
        /// </summary>
        private Color sample(float t)
        {
            float scaled = t * (stops.Count - 1);
            int index = Mathf.Min(Mathf.FloorToInt(scaled), stops.Count - 2);
            return Color.Lerp(stops[index], stops[index + 1], scaled - index);
        }

        private void selectStop(int index)
        {
            selectedStop = index;
            hexBuffer = toHex(stops[index]);
        }

        private static string toHex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);
    }
}
