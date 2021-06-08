using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using HugsLib.Settings;
using HugsLib.Source.Settings;
using Utils;

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
            // HugsLib doesn't support deserialising XmlArrays so we have to do this ourselves
            XmlSerializer s = new XmlSerializer(List.GetType());
            object result = null;
            using (StringReader sr = new StringReader(settingValue))
            {
                try
                {
                    result = s.Deserialize(sr);
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs("Failed to deserialise StringListSetting: " + e.Message, LogLevel.ERROR));
                }
            }
            
            if (result is List<string> loadedList)
            {
                List.Clear();
                List.AddRange(loadedList);
            }
            else
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Failed to deserialise StringListSetting: Deserialised type {0} does not match {1}", result?.GetType(), List.GetType()), LogLevel.ERROR));
            }
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