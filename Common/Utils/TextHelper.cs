// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Text.RegularExpressions;
using Pastel;

namespace Utils
{
	public static class TextHelper
	{
		/// <summary>
		/// Array containing all tags provided by Unity's Rich Text API.
		/// </summary>
		private static readonly string[] strippableTags = {"size", "b", "i", "color"};
		/// <summary>
		/// Array containing undesirable tags that should be filtered out.
		/// </summary>
		private static readonly string[] unsafeTags = {"size"};

		/// <summary>
		/// Strips the given set of tags from the input string.
		/// </summary>
		/// <param name="input">String to strip</param>
		/// <param name="strippedTags">Tags to strip from the input string</param>
		/// <returns>Stripped input</returns>
		private static string stripRichText(string input, params string[] strippedTags)
		{
			foreach (string tag in strippedTags) {
				// Maybe a better way than a Regex to parse RichText?
				// Unfortunately not, I'm afraid
				string pattern = @"<\/?" + tag + @"(=[\w#]+)?>";

				Regex regex = new Regex(pattern);
				input = regex.Replace (input, "");
			}

			return input;
		}

		/// <summary>
		/// Returns the given string with all rich text tags removed.
		/// </summary>
		/// <param name="input">String to strip</param>
		/// <returns>Stripped input</returns>
		public static string StripRichText(string input)
		{
			return stripRichText(input, strippableTags);
		}

		/// <summary>
		/// The most formatting tags a name can open and still be drawn with its formatting, as a guard against names
		/// padded out with thousands of tags. A colour per letter of a long name stays well under it.
		/// </summary>
		public const int MAX_DRAWN_TAGS = 200;

		private static readonly Regex anyTagPattern = new Regex(@"<(\/?)(size|b|i|color)(=[^<>]*)?>", RegexOptions.Compiled);

		/// <summary>
		/// Whether the game can draw the given text's formatting: each tag closed in the right order, and no more than
		/// <see cref="MAX_DRAWN_TAGS"/> of them. Unity shows the raw tags instead (or, in chat, nothing) when they're
		/// closed out of order. Text that fails should be repaired with <see cref="RepairRichText"/> or shown plain.
		/// </summary>
		/// <param name="input">Text to check</param>
		/// <returns>Whether its formatting can be drawn</returns>
		public static bool CanDrawRichText(string input)
		{
			if (string.IsNullOrEmpty(input) || input.IndexOf('<') < 0) return true;

			System.Collections.Generic.Stack<string> open = new System.Collections.Generic.Stack<string>();
			int opened = 0;
			foreach (Match match in anyTagPattern.Matches(input))
			{
				string tag = match.Groups[2].Value;
				if (match.Groups[1].Value.Length == 0)
				{
					if (++opened > MAX_DRAWN_TAGS) return false;
					open.Push(tag);
				}
				else if (open.Count == 0 || open.Pop() != tag)
				{
					return false;
				}
			}
			return open.Count == 0;
		}

		/// <summary>
		/// Fixes formatting tags closed in the wrong order, like <c>&lt;b&gt;&lt;i&gt;Name&lt;/b&gt;&lt;/i&gt;</c>,
		/// which some name generators write. A tag closed while others are open inside it closes those first and opens
		/// them again after. Closing tags with nothing to close are dropped, and tags left open are closed at the end.
		/// </summary>
		/// <param name="input">Text to repair</param>
		/// <returns>The text with its tags in an order the game can draw</returns>
		public static string RepairRichText(string input)
		{
			if (string.IsNullOrEmpty(input) || input.IndexOf('<') < 0) return input;

			// Each open tag as written (with its value, so a colour can be opened again) and its name
			System.Collections.Generic.List<(string Name, string Text)> open = new System.Collections.Generic.List<(string, string)>();
			System.Text.StringBuilder output = new System.Text.StringBuilder(input.Length + 32);
			int last = 0;
			foreach (Match match in anyTagPattern.Matches(input))
			{
				output.Append(input, last, match.Index - last);
				last = match.Index + match.Length;

				string tag = match.Groups[2].Value;
				if (match.Groups[1].Value.Length == 0)
				{
					open.Add((tag, match.Value));
					output.Append(match.Value);
					continue;
				}

				int index = open.FindLastIndex(entry => entry.Name == tag);
				if (index < 0) continue;

				// Close what's inside it, close it, then reopen what was inside
				for (int i = open.Count - 1; i > index; i--) output.Append("</").Append(open[i].Name).Append('>');
				output.Append("</").Append(tag).Append('>');
				for (int i = index + 1; i < open.Count; i++) output.Append(open[i].Text);
				open.RemoveAt(index);
			}
			output.Append(input, last, input.Length - last);
			for (int i = open.Count - 1; i >= 0; i--) output.Append("</").Append(open[i].Name).Append('>');

			return output.ToString();
		}

		/// <summary>
		/// Returns the given string with all undesirable tags removed.
		/// At the moment this only removes the size tag.
		/// </summary>
		/// <param name="input">String to sanitise</param>
		/// <returns>Sanitised input</returns>
		public static string SanitiseRichText(string input)
		{
			return stripRichText(input, unsafeTags);
		}

		/// <summary>
		/// Highlights a string with ANSI colour codes according to the given <see cref="HighlightType"/>.
		/// Intended for making the server log more readable.
		/// </summary>
		/// <param name="str"></param>
		/// <param name="highlightType"></param>
		/// <returns></returns>
		public static string Highlight(this string str, HighlightType highlightType)
		{
			switch (highlightType)
			{
				case HighlightType.ConnectionID:
					return str.Pastel("bf616a");
				case HighlightType.SessionID:
					return str.Pastel("d08770");
				case HighlightType.UUID:
					return str.Pastel("ebcb8b");
				case HighlightType.ChatMessageID:
					return str.Pastel("a3be8c");
				case HighlightType.TradeID:
					return str.Pastel("b48ead");
				case HighlightType.Username:
					return str.Pastel("88c0d0");
				default:
					return str;
			}
		}
	}

	public enum HighlightType
	{
		ConnectionID,
		SessionID,
		UUID,
		ChatMessageID,
		TradeID,
		Username
	}
}

