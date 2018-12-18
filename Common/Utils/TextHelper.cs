﻿// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Text.RegularExpressions;

namespace Utils
{
	public static class TextHelper
	{
		public const string SIZE = "size";
		public const string B = "b";
		public const string I = "i";
		public const string COLOR = "color";

		private static string stripRichText(string input, params string[] strippedTags)
		{
			foreach (string tag in strippedTags) {
				// Maybe a better way than a Regex to parse RichText ?
				string pattern = @"<\/?" + tag + @"(=[\w#]+)?>";

				Regex regex = new Regex(pattern);
				input = regex.Replace (input, "");
			}

			return input;
		}

		public static string StripRichText(string input)
		{
			return StripRichText(input, SIZE, B, I, COLOR);
		}

        public static string Clamp(string input, int min, int max, char filler = '-')
        {
            int strippedLength = StripRichText(input).Length;

            if (strippedLength < min)
            {
                input += new string(filler, min);
            }
            else if (strippedLength > max)
            {
                input = input.Substring(0, max);
            }

            return input;
        }
	}
}

