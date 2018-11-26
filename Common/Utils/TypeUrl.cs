using System;
using System.Text.RegularExpressions;

namespace Utils
{
    public class TypeUrl
    {
        public string Prefix;
        public string Namespace;
        public string Type;
        
        /// <summary>
        /// Creates a <c>TypeUrl</c> object from a valid TypeUrl string.
        /// For example, given 'Phinix/Namespace.SubNamespace.ClassName' it would yield:
        ///   1. Phinix - The prefix
        ///   2. Namespace.SubNamespace - The namespace
        ///   3. ClassName - The class name
        /// </summary>
        /// <param name="typeUrl">TypeUrl string</param>
        /// <exception cref="ArgumentException">Given string is not a properly-formatted TypeUrl string</exception>
        public TypeUrl(string typeUrl)
        {
            Regex pattern = new Regex("([\\w.]+)\\/([\\w.]+?)\\.(\\w+)");
            GroupCollection groups = pattern.Match(typeUrl).Groups;

            if (groups.Count == 4) // Group 0 is always present and contains the whole match
            {
                this.Prefix = groups[1].Value;
                this.Namespace = groups[2].Value;
                this.Type = groups[3].Value;
            }
            else
            {
                throw new ArgumentException("Invalid TypeUrl string", nameof(typeUrl));
            }
        }

        public TypeUrl(string prefix, string namespace_, string type)
        {
            this.Prefix = prefix;
            this.Namespace = namespace_;
            this.Type = type;
        }
    }
}