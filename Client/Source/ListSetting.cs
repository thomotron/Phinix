using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using HugsLib.Settings;
using HugsLib.Source.Settings;
using Utils;

namespace PhinixClient
{
    /// <summary>
    /// Wrapper for <see cref="System.Collections.Generic.List{T}"/> that HugsLib can de/serialise in a <see cref="SettingHandle{T}"/>.
    /// </summary>
    public class ListSetting<T> : SettingHandleConvertible
    {
        public override bool ShouldBeSaved => true;
        
        /// <summary>
        /// Inner wrapped list.
        /// </summary>
        [XmlArray]
        public readonly List<T> List;

        /// <summary>
        /// Creates a new <see cref="ListSetting{T}"/> with an empty list.
        /// </summary>
        public ListSetting() : this(new List<T>()) {}

        /// <summary>
        /// Creates a new <see cref="ListSetting{T}"/> to wrap the given list.
        /// </summary>
        /// <param name="list">List to wrap</param>
        public ListSetting(List<T> list)
        {
            this.List = list;
        }

        /// <inheritdoc cref="SettingHandleConvertible.FromString"/>
        public override void FromString(string settingValue)
        {
            // HugsLib doesn't support deserialising XmlArrays so we have to do this ourselves
            // Try deserialising what we've been given
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
                    Client.Instance.Log(new LogEventArgs("Failed to deserialise ListSetting: " + e.Message, LogLevel.ERROR));
                }
            }
            
            // Make sure it's a list and if so, update it
            if (result is List<T> loadedList)
            {
                List.Clear();
                List.AddRange(loadedList);
            }
            else
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Failed to deserialise ListSetting: Deserialised type {0} does not match {1}", result?.GetType(), List.GetType()), LogLevel.ERROR));
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
            return obj is ListSetting<T> other && List.Equals(other.List);
        }

        /// <inheritdoc cref="object.GetHashCode"/>
        public override int GetHashCode()
        {
            return (List != null ? List.GetHashCode() : 0);
        }
    }
}