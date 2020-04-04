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

