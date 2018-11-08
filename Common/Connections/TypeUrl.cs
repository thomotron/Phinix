using System;
using System.CodeDom;
using System.Linq;

namespace Connections
{
    public class TypeUrl
    {
        public string Prefix;
        public string Namespace;
        public string Type;

        public TypeUrl(string typeUrl)
        {
            string[] typeUrlComponents = typeUrl.Split('/', '.');
            
            this.Prefix = typeUrlComponents[0];
            string[] suffix = typeUrlComponents[1].Split('.');
            
            this.Namespace = string.Join(".", suffix.Take(suffix.Length - 1).ToArray());
            this.Type = suffix.Last();
        }

        public TypeUrl(string prefix, string namespace_, string type)
        {
            this.Prefix = prefix;
            this.Namespace = namespace_;
            this.Type = type;
        }
    }
}