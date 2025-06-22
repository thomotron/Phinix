using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Xml.Serialization;
using Utils;

namespace PhinixClient
{
    /// <summary>
    /// Legacy settings saved by HugsLib.
    /// </summary>
    public class LegacySettings
    {
        #region Properties

        public string ServerAddress { get; set; }

        public int? ServerPort { get; set; }

        public string DisplayName { get; set; }

        public bool? AcceptingTrades { get; set; }

        public bool? ShowNameFormatting { get; set; }

        public bool? ShowChatFormatting { get; set; }

        public bool? PlayNoiseOnMessageReceived { get; set; }

        public bool? ShowUnreadMessageCount { get; set; }

        public bool? ShowBlockedUnreadMessageCount { get; set; }

        public int? ChatMessageLimit { get; set; }

        public bool? ForceMessageFieldFocus { get; set; }

        public bool? AllItemsTradable { get; set; }

        public bool? ShowBlockedTrades { get; set; }

        public HashSet<string> BlockedUsers { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Loads legacy settings from the HugsLib mod settings file.
        /// </summary>
        /// <param name="path">Path to the HugsLib settings file.</param>
        /// <returns>Loaded settings object if present, otherwise <see langword="null"/>.</returns>
        public static LegacySettings FromHugsLibSettings(string path)
        {
            var settings = new LegacySettings();

            if (!File.Exists(path))
            {
                return null;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch (Exception ex)
            {
                Client.Instance.Log(new LogEventArgs($"Failed to load settings XML: {ex.Message}", LogLevel.ERROR));
                return null;
            }

            var phinixElement = doc.Root?.Element("Phinix");
            if (phinixElement == null)
            {
                return null;
            }

            var displayName = phinixElement.Element("displayName")?.Value;
            if (!string.IsNullOrEmpty(displayName))
            {
                settings.DisplayName = displayName;
            }

            var serverAddress = phinixElement.Element("serverAddress")?.Value;
            if (!string.IsNullOrEmpty(serverAddress))
            {
                settings.ServerAddress = serverAddress;
            }

            if (int.TryParse(phinixElement.Element("serverPort")?.Value, out int serverPort))
            {
                settings.ServerPort = serverPort;
            }

            if (int.TryParse(phinixElement.Element("chatMessageLimit")?.Value, out int chatMessageLimit))
            {
                settings.ChatMessageLimit = chatMessageLimit;
            }

            if (bool.TryParse(phinixElement.Element("allItemsTradable")?.Value, out bool allItemsTradable))
            {
                settings.AllItemsTradable = allItemsTradable;
            }

            if (bool.TryParse(phinixElement.Element("acceptingTrades")?.Value, out bool acceptingTrades))
            {
                settings.AcceptingTrades = acceptingTrades;
            }

            if (bool.TryParse(phinixElement.Element("showNameFormatting")?.Value, out bool showNameFormatting))
            {
                settings.ShowNameFormatting = showNameFormatting;
            }

            if (bool.TryParse(phinixElement.Element("showChatFormatting")?.Value, out bool showChatFormatting))
            {
                settings.ShowChatFormatting = showChatFormatting;
            }

            if (bool.TryParse(phinixElement.Element("playNoiseOnMessageReceived")?.Value, out bool playNoiseOnMessageReceived))
            {
                settings.PlayNoiseOnMessageReceived = playNoiseOnMessageReceived;
            }

            if (bool.TryParse(phinixElement.Element("showUnreadMessageCount")?.Value, out bool showUnreadMessageCount))
            {
                settings.ShowUnreadMessageCount = showUnreadMessageCount;
            }

            if (bool.TryParse(phinixElement.Element("showBlockedUnreadMessageCount")?.Value, out bool showBlockedUnreadMessageCount))
            {
                settings.ShowBlockedUnreadMessageCount = showBlockedUnreadMessageCount;
            }

            if (bool.TryParse(phinixElement.Element("forceMessageFieldFocus")?.Value, out bool forceMessageFieldFocus))
            {
                settings.ForceMessageFieldFocus = forceMessageFieldFocus;
            }

            if (bool.TryParse(phinixElement.Element("showBlockedTrades")?.Value, out bool showBlockedTrades))
            {
                settings.ShowBlockedTrades = showBlockedTrades;
            }

            var blockedUsersRaw = phinixElement.Element("blockedUsers")?.Value;
            if (!string.IsNullOrEmpty(blockedUsersRaw) && TryParseXmlArray(blockedUsersRaw, out List<string> blockedUsers))
            {
                settings.BlockedUsers = new HashSet<string>(blockedUsers);
            }
            else
            {
                settings.BlockedUsers = new HashSet<string>();
            }

            return settings;
        }

        /// <summary>
        /// Attempts to parse a string value as a list of strings encoded as an XML array.
        /// </summary>
        /// <param name="value">String to parse.</param>
        /// <param name="output">Parsed result as a list of strings.</param>
        /// <returns>Whether the operation completed successfully.</returns>
        public static bool TryParseXmlArray(string value, out List<string> output)
        {
            output = null;

            object result = null;
            using (StringReader sr = new StringReader(value))
            {
                XmlSerializer s = new XmlSerializer(typeof(List<string>));
                try
                {
                    result = s.Deserialize(sr);
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs($"Failed to deserialise ListSetting: {e.Message}", LogLevel.ERROR));
                    return false;
                }
            }

            if (result is List<string> resultList)
            {
                output = resultList;
                return true;
            }
            else
            {
                Client.Instance.Log(new LogEventArgs($"Failed to deserialise ListSetting: Deserialised type {result?.GetType()} does not match {typeof(List<string>)}", LogLevel.ERROR));
                return false;
            }
        }

        #endregion
    }
}
