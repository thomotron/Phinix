// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
		private static string stripRichText(string input, string[] strippedTags)
		{
			foreach (string tag in strippedTags) {
				// Maybe a better way than a Regex to parse RichText?
				// Unfortunately not, I'm afraid
				string pattern = @"<\/?" + tag + @"(=[\w#]+)?>";

				Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
				input = regex.Replace(input, "");
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

		public static string Strip(string input, string[] strippedTags)
		{
			string text = "<this><is><a=closed>tag</a></is></this>";



			Regex colourHexRegex = new Regex("#([a-f0-9]{8}|[a-f0-9]{6}|[a-f0-9]{3})");
			Stack<TagMatch> tagStack = new Stack<string>();

			// Iterate over the string
			for (int i = 0; i < text.Length; i++)
			{
				// Try match the string from our current position against the opening tag filter
				Match openMatch = openingTagRegex.Match(text.Substring(i));
				if (openMatch.Success)
				{
					string tag = openMatch.Groups[1].Value.ToLower();
					string arg = openMatch.Groups[3].Value.ToLower();

					// Check if the tag matches any of the strippable tags
					if (strippedTags.Contains(tag))
					{
						// Push the opening tag onto the stack
						tagStack.Push(openMatch.Groups[0].Value);
						i += openMatch.Length;
					}


				}
				else
				{

				}
			}
		}

		private static bool tryFindOpeningTag(string text, out TagMatch tagMatch)
		{
			tagMatch = new TagMatch();

			Regex regex = new Regex("<(\\w+)(?:=(\"?)([^>=\"]*)\\2)?>");
			Match match = regex.Match(text);

			if (match.Success)
			{
				tagMatch = new TagMatch(
					tag: match.Groups[1].Value,
					arg: match.Groups[3].Value,
					closing: false,
					startPos: match.Index,
					length: match.Length
				);

				return true;
			}

			return false;
		}

		private static bool tryFindClosingTag(string text, out TagMatch tagMatch)
		{
			tagMatch = new TagMatch();

			Regex regex = new Regex("<\\/(\\w+)>");
			Match match = regex.Match(text);

			if (match.Success)
			{
				tagMatch = new TagMatch(
					tag: match.Groups[1].Value,
					arg: match.Groups[3].Value,
					closing: true,
					startPos: match.Index,
					length: match.Length
				);

				return true;
			}

			return false;
		}

		private struct TagMatch
		{
			public string Tag { get; }
			public string Arg { get; }
			public bool Closing { get; }
			public int StartPos { get; }
			public int Length { get; }

			public TagMatch(string tag, string arg, bool closing, int startPos, int length)
			{
				this.Tag = tag;
				this.Arg = arg;
				this.Closing = closing;
				this.StartPos = startPos;
				this.Length = length;
			}
		}

		enum CaptureStage
		{
			Tag,
			Arg,
			Result,
		}

		public static string Doot(string text)
		{
			StringBuilder resultBuffer = new StringBuilder();
			StringBuilder tagBuffer = new StringBuilder();
			StringBuilder argBuffer = new StringBuilder();
			CaptureStage capStage;

			// Iterate over the string, char by char
			for (int i = 0; i < text.Length; i++)
			{
				// Get the char at the current position
				char firstChar = text[i];

				// Is this a tag? (starts with `<` and has a `>` further along)
				if (firstChar == '<' && text.IndexOf('>', i) > i)
				{
					bool isClosingTag = false;
					tagBuffer.Length = 0;
					argBuffer.Length = 0;

					// State that we're within a tag
					capStage = CaptureStage.Tag;

					// Mark where the tag opened
					int tagStartIndex = i;

					// Move forward one
					i++;

					// Iterate along the string until we get to the end or the tag closes, whichever comes first
					bool finishedReadingTag = false;
					for (; i < text.Length && !finishedReadingTag; i++)
					{
						char secondChar = text[i];
						switch (secondChar)
						{
							case '/':
								// Found a `/`, this tag should be closing soon
								isClosingTag = true;
								break;
							case '>':
								// Found a `>`, this tag is closed
								capStage = CaptureStage.Result;
								if (isClosingTag)
								{
									// Get a copy of the tags and content then append it to the result buffer
									string str = text.Substring(tagStartIndex, i - tagStartIndex + 1);
									resultBuffer.Append(str);

									// Mark that we're done reading the tag
									finishedReadingTag = true;
									break;
								}
								else
									// This was the end of an opening tag, don't do anything special
									break;
						}

						// Skip further processing if we're done with this tag
						if (finishedReadingTag) break;

						if (capStage == CaptureStage.Arg)
						{
							// We're reading an argument, append it to the arg buffer
							argBuffer.Append(secondChar);
						}

						if (!isClosingTag && secondChar == '=')
						{
							// We're in an opening tag and we have an argument coming
							capStage = CaptureStage.Arg;
						}

						if (capStage == CaptureStage.Tag)
						{
							// We're reading a tag, append it to the tag buffer
							tagBuffer.Append(secondChar);
						}
					}

					if (!isClosingTag)
					{
						resultBuffer.Append(firstChar);
						i = tagStartIndex + 1;
					}
				}
				else
				{
					resultBuffer.Append(firstChar);
				}
			}

			return resultBuffer.ToString();
		}
	}
}

