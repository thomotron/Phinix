using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HugsLib.Settings;
using HugsLib.Source.Settings;

namespace PhinixClient
{
    public class StringListSetting : SettingHandleConvertible
    {
        public override bool ShouldBeSaved => true;

        [XmlArray]
        public readonly List<string> List;

        public StringListSetting() : this(new List<string>()) {}

        public StringListSetting(List<string> list)
        {
            this.List = list;
        }

        /// <inheritdoc cref="SettingHandleConvertible.FromString"/>
        public override void FromString(string settingValue)
        {
            SettingHandleConvertibleUtility.DeserializeValuesFromString(settingValue, List);
        }

        /// <inheritdoc cref="SettingHandleConvertible.ToString"/>
        public override string ToString()
        {
            return SettingHandleConvertibleUtility.SerializeValuesToString(List);
        }

        /// <inheritdoc cref="object.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is StringListSetting other && List.Equals(other.List);
        }

        /// <inheritdoc cref="object.GetHashCode"/>
        public override int GetHashCode()
        {
            return (List != null ? List.GetHashCode() : 0);
        }
    }
}