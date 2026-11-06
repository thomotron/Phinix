using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Emojis typed as :shortcodes: (":smile:", ":+1:") and drawn as small Twemoji pictures in chat and messages.
    /// </summary>
    /// <remarks>
    /// Nothing changes on the wire: the message is sent and stored as plain text, and each player's client swaps known
    /// shortcodes for pictures as it draws them, so it works on any server. Players without the mod just see the text.
    /// <para>
    /// The pictures are drawn on top of a label: each shortcode becomes a transparent stand-in (<see cref="PLACEHOLDER"/>)
    /// in the text so the label leaves a gap of the right size, and the picture goes over the gap afterwards.
    /// </para>
    /// </remarks>
    public static class Emoji
    {
        /// <summary>Marks where an emoji goes while a message is being put together, before it's drawn</summary>
        public const char MARK = '\u0001';

        /// <summary>What a mark turns into in the text that's measured and drawn: two characters, about an emoji's width</summary>
        private const string PLACEHOLDER = "WW";

        private static readonly Regex shortcodePattern = new Regex(@":([a-z0-9_+\-]+):", RegexOptions.Compiled);

        /// <summary>
        /// Unity's <c>GUIStyle.GetCursorPixelPosition</c>, or null where the game doesn't have it.
        /// </summary>
        /// <remarks>
        /// RimWorld 1.7's engine draws the GUI itself and its <c>GUIStyle</c> leaves this method out. Calling it directly
        /// would throw "Method not found" as soon as <see cref="Label"/> runs there, so it's looked up once by name, and
        /// <see cref="lineStarts"/> lays the text out instead when it's missing.
        /// </remarks>
        private static readonly Func<GUIStyle, Rect, GUIContent, int, Vector2> cursorPixelPosition = findCursorPixelPosition();

        /// <summary>
        /// Where each line starts in text <see cref="lineStarts"/> laid out recently, by text and width. Chat draws the
        /// same messages every frame, so this saves measuring them again each time.
        /// </summary>
        private static readonly Dictionary<(string, float), List<int>> lineStartsCache = new Dictionary<(string, float), List<int>>();

        private static Dictionary<string, string> mainNames;
        private static Dictionary<string, int> sheetIndices;
        private static Texture2D sheet;
        private static bool sheetLoaded;

        /// <summary>Shortcode (and alias) to main shortcode, built the first time it's needed</summary>
        private static Dictionary<string, string> MainNames
        {
            get
            {
                if (mainNames != null) return mainNames;

                mainNames = new Dictionary<string, string>();
                sheetIndices = new Dictionary<string, int>();
                for (int i = 0; i < EmojiIndex.All.Length; i++)
                {
                    (string name, string[] aliases, int _) = EmojiIndex.All[i];
                    mainNames[name] = name;
                    sheetIndices[name] = i;
                    foreach (string alias in aliases)
                    {
                        if (!mainNames.ContainsKey(alias)) mainNames[alias] = name;
                    }
                }
                return mainNames;
            }
        }

        /// <summary>The sheet all the emojis are packed into, or null if it's missing</summary>
        private static Texture2D Sheet
        {
            get
            {
                if (!sheetLoaded)
                {
                    sheet = ContentFinder<Texture2D>.Get("Phinix/Emoji", false);
                    sheetLoaded = true;
                }
                return sheet;
            }
        }

        /// <summary>
        /// Whether the server has emojis switched on. While it's off, shortcodes show as plain text.
        /// </summary>
        public static bool Enabled => Client.Instance.FeatureEnabled(UserManagement.Features.EMOJIS);

        /// <summary>
        /// Whether an emoji (by its main shortcode) can be drawn.
        /// </summary>
        public static bool Exists(string name) => Sheet != null && MainNames != null && sheetIndices.ContainsKey(name);

        /// <summary>
        /// Draws an emoji, by its main shortcode, filling <paramref name="rect"/>. Does nothing if it's missing.
        /// </summary>
        public static void Draw(Rect rect, string name)
        {
            if (!Exists(name)) return;

            // Its cell on the sheet, in texture coordinates (which start at the bottom left)
            int index = sheetIndices[name];
            float width = EmojiIndex.SheetColumns * EmojiIndex.CellSize;
            float height = EmojiIndex.SheetRows * EmojiIndex.CellSize;
            float padding = (EmojiIndex.CellSize - EmojiIndex.EmojiSize) / 2f;
            float left = index % EmojiIndex.SheetColumns * EmojiIndex.CellSize + padding;
            float top = index / EmojiIndex.SheetColumns * EmojiIndex.CellSize + padding;
            Rect texCoords = new Rect(left / width, 1f - (top + EmojiIndex.EmojiSize) / height, EmojiIndex.EmojiSize / width, EmojiIndex.EmojiSize / height);

            UnityEngine.GUI.DrawTextureWithTexCoords(rect, Sheet, texCoords);
        }

        /// <summary>
        /// Swaps each known shortcode in <paramref name="text"/> for a <see cref="MARK"/>, adding the emoji's name to
        /// <paramref name="names"/> in order. Unknown shortcodes are left as they are.
        /// </summary>
        /// <returns>The marked text</returns>
        public static string Mark(string text, List<string> names)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(':') < 0 || !Enabled) return text?.Replace(MARK.ToString(), "");

            return shortcodePattern.Replace(text.Replace(MARK.ToString(), ""), match =>
            {
                if (!MainNames.TryGetValue(match.Groups[1].Value, out string name) || !Exists(name)) return match.Value;
                names.Add(name);
                return MARK.ToString();
            });
        }

        /// <summary>
        /// Turns marked text into what's measured and drawn, with a gap where each emoji goes.
        /// </summary>
        /// <param name="text">Marked text</param>
        /// <param name="hidden">Whether the gaps are invisible (for drawing) or plain (for measuring)</param>
        public static string Placeholders(string text, bool hidden = true)
        {
            if (text.IndexOf(MARK) < 0) return text;
            return text.Replace(MARK.ToString(), hidden ? "<color=#00000000>" + PLACEHOLDER + "</color>" : PLACEHOLDER);
        }

        /// <summary>
        /// Draws marked text as a label, with the emojis drawn in their gaps.
        /// </summary>
        /// <param name="rect">Where to draw, as for <see cref="Widgets.Label(Rect, string)"/></param>
        /// <param name="text">Marked text, which may contain rich text</param>
        /// <param name="names">The emojis' names, in the order of the marks, from <see cref="Mark"/></param>
        public static void Label(Rect rect, string text, List<string> names)
        {
            Widgets.Label(rect, Placeholders(text));
            if (names == null || names.Count == 0) return;

            // Find where each gap ended up by measuring the same text without its formatting (the formatting left in a
            // chat message can't change sizes, so the words wrap at the same places)
            string plain = TextHelper.StripRichText(text);
            StringBuilder measured = new StringBuilder(plain.Length + names.Count * PLACEHOLDER.Length);
            List<int> gapIndices = new List<int>();
            foreach (char c in plain)
            {
                if (c == MARK)
                {
                    gapIndices.Add(measured.Length);
                    measured.Append(PLACEHOLDER);
                }
                else
                {
                    measured.Append(c);
                }
            }

            GUIStyle style = new GUIStyle(Text.CurFontStyle) { richText = false };
            GUIContent content = new GUIContent(measured.ToString());
            float lineHeight = style.lineHeight;

            for (int i = 0; i < gapIndices.Count && i < names.Count; i++)
            {
                Vector2 start = cursorPosition(style, rect, content, gapIndices[i]);
                Vector2 end = cursorPosition(style, rect, content, gapIndices[i] + PLACEHOLDER.Length);
                float gapWidth = end.y == start.y ? end.x - start.x : lineHeight;
                float size = Mathf.Min(lineHeight, gapWidth + 2f);

                Draw(new Rect(start.x + (gapWidth - size) / 2f, start.y + (lineHeight - size) / 2f, size, size), names[i]);
            }
        }

        /// <summary>
        /// Looks up <c>GUIStyle.GetCursorPixelPosition</c> by name, for <see cref="cursorPixelPosition"/>.
        /// </summary>
        /// <returns>A delegate calling it, or null if this game's <c>GUIStyle</c> doesn't have it</returns>
        private static Func<GUIStyle, Rect, GUIContent, int, Vector2> findCursorPixelPosition()
        {
            MethodInfo method = typeof(GUIStyle).GetMethod("GetCursorPixelPosition", new[] { typeof(Rect), typeof(GUIContent), typeof(int) });
            if (method == null || method.ReturnType != typeof(Vector2)) return null;

            try
            {
                return (Func<GUIStyle, Rect, GUIContent, int, Vector2>) Delegate.CreateDelegate(typeof(Func<GUIStyle, Rect, GUIContent, int, Vector2>), method);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the text cursor would be drawn before a character of a label: Unity's own answer where the game has it,
        /// otherwise worked out from <see cref="lineStarts"/>. Assumes the label is anchored to the upper left, like chat.
        /// </summary>
        /// <param name="style">Style the label is drawn with</param>
        /// <param name="rect">Where the label is drawn</param>
        /// <param name="content">The label's text, without formatting</param>
        /// <param name="index">Index of the character</param>
        /// <returns>The cursor's top-left corner, in the same space as <paramref name="rect"/></returns>
        private static Vector2 cursorPosition(GUIStyle style, Rect rect, GUIContent content, int index)
        {
            if (cursorPixelPosition != null) return cursorPixelPosition(style, rect, content, index);

            string text = content.text;
            List<int> starts = lineStarts(style, text, rect.width - style.padding.horizontal);
            int line = starts.Count - 1;
            while (line > 0 && starts[line] > index) line--;

            float x = textWidth(style, text.Substring(starts[line], index - starts[line]));
            return new Vector2(rect.x + style.padding.left + x, rect.y + style.padding.top + line * style.lineHeight);
        }

        /// <summary>
        /// Lays text out the way a word-wrapped label does, a word at a time, and returns where each line starts.
        /// </summary>
        /// <remarks>
        /// A line breaks at a newline, or after the last space that still lets the line fit in <paramref name="width"/>.
        /// A word too long for a line on its own is broken at the last character that fits. The space a line breaks at
        /// stays at the end of that line, as Unity does.
        /// </remarks>
        /// <param name="style">Style the text is drawn with</param>
        /// <param name="text">Text, without formatting</param>
        /// <param name="width">Width available for the text, inside the style's padding</param>
        /// <returns>Index of the first character of each line, starting with 0</returns>
        private static List<int> lineStarts(GUIStyle style, string text, float width)
        {
            if (lineStartsCache.TryGetValue((text, width), out List<int> cached)) return cached;

            List<int> starts = new List<int> { 0 };
            int lineStart = 0;
            int wordStart = 0;
            while (wordStart < text.Length)
            {
                // The next word, with the spaces after it
                int wordEnd = wordStart;
                while (wordEnd < text.Length && text[wordEnd] != ' ' && text[wordEnd] != '\n') wordEnd++;
                int spacesEnd = wordEnd;
                while (spacesEnd < text.Length && text[spacesEnd] == ' ') spacesEnd++;

                // Doesn't fit on this line: start a new one with this word, unless it's already the first word
                if (style.wordWrap && wordStart > lineStart && textWidth(style, text.Substring(lineStart, wordEnd - lineStart)) > width)
                {
                    lineStart = wordStart;
                    starts.Add(lineStart);
                }

                // Too long even for a line of its own: break it where it stops fitting
                while (style.wordWrap && wordEnd - lineStart > 1 && textWidth(style, text.Substring(lineStart, wordEnd - lineStart)) > width)
                {
                    int fits = lineStart + 1;
                    while (fits < wordEnd && textWidth(style, text.Substring(lineStart, fits + 1 - lineStart)) <= width) fits++;
                    lineStart = fits;
                    starts.Add(lineStart);
                }

                if (spacesEnd < text.Length && text[spacesEnd] == '\n')
                {
                    lineStart = spacesEnd + 1;
                    starts.Add(lineStart);
                    spacesEnd++;
                }
                wordStart = spacesEnd;
            }

            if (lineStartsCache.Count > 200) lineStartsCache.Clear();
            lineStartsCache[(text, width)] = starts;
            return starts;
        }

        /// <summary>
        /// Width of some text on one line in a style, without the style's padding.
        /// </summary>
        private static float textWidth(GUIStyle style, string text) => style.CalcSize(new GUIContent(text)).x - style.padding.horizontal;
    }
}
