using System;
using System.Globalization;
using System.IO;
using System.Text;
using Chat;
using Hermes;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Keeps the messages a player reports, in a file on their own computer.
    /// </summary>
    /// <remarks>
    /// For now a report is only saved, one per line, in <c>PhimeReports.jsonl</c> in RimWorld's Config folder (next to
    /// Phinix's own settings), so it doesn't depend on which colony is loaded. Each line is a small JSON object, the
    /// same shape a "report" packet to the server admin could take later.
    /// </remarks>
    public static class MessageReports
    {
        /// <summary>
        /// One reported message and everything needed to find it again.
        /// </summary>
        public class Report
        {
            /// <summary>"chat" for the main chat, "inbox" for a private message</summary>
            public string Where;
            /// <summary>The server the message came through, as "address:port"</summary>
            public string Server;
            public string MessageId;
            /// <summary>When the message was sent, in UTC</summary>
            public DateTime SentAt;
            public string SenderUuid;
            public string SenderName;
            /// <summary>Who it was to. Empty for the main chat, where it went to everyone</summary>
            public string RecipientUuid;
            public string RecipientName;
            public string Text;
            /// <summary>Why the player reported it, if they said</summary>
            public string Reason = "";
        }

        /// <summary>
        /// Where reports are saved.
        /// </summary>
        public static string FilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "PhimeReports.jsonl");

        /// <summary>
        /// Most characters kept from the reason box.
        /// </summary>
        public const int MAX_REASON_LENGTH = 500;

        /// <summary>
        /// Makes a report for a message in the main chat.
        /// </summary>
        /// <param name="message">Message being reported</param>
        public static Report ForChatMessage(ClientChatMessage message)
        {
            Settings settings = Client.Instance.Settings;
            return new Report
            {
                Where = "chat",
                Server = settings.ServerAddress + ":" + settings.ServerPort,
                MessageId = message.MessageId,
                SentAt = message.Timestamp.ToUniversalTime(),
                SenderUuid = message.SenderUuid,
                SenderName = nameOf(message.SenderUuid),
                RecipientUuid = "",
                RecipientName = "",
                Text = message.Message
            };
        }

        /// <summary>
        /// Makes a report for a private message in the Inbox.
        /// </summary>
        /// <param name="message">Message being reported</param>
        public static Report ForDirectMessage(DirectMessage message)
        {
            // Private messages go through the companion when the main server can't carry them
            Settings settings = Client.Instance.Settings;
            string server = Client.Instance.DirectMessagesViaCompanion
                ? settings.CompanionAddress + ":" + settings.CompanionPort
                : settings.ServerAddress + ":" + settings.ServerPort;

            return new Report
            {
                Where = "inbox",
                Server = server,
                MessageId = message.MessageId,
                SentAt = DateTimeOffset.FromUnixTimeSeconds(message.SentTime).UtcDateTime,
                SenderUuid = message.SenderUuid,
                SenderName = nameOf(message.SenderUuid),
                RecipientUuid = message.RecipientUuid,
                RecipientName = nameOf(message.RecipientUuid),
                Text = message.Text
            };
        }

        /// <summary>
        /// Someone's display name without formatting, or "???" if we don't know them.
        /// </summary>
        private static string nameOf(string uuid)
        {
            return Client.Instance.TryGetDisplayName(uuid, out string name) ? TextHelper.StripRichText(name) : "???";
        }

        /// <summary>
        /// Adds a report to the end of the file, along with who reported it and when.
        /// </summary>
        /// <param name="report">Report to save</param>
        /// <returns>Whether it was saved</returns>
        public static bool Save(Report report)
        {
            // Built up piece by piece, like a list of "key": value strings joined with commas in Python
            StringBuilder json = new StringBuilder("{");
            field(json, "reportedAt", DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture));
            field(json, "reporterUuid", Client.Instance.Uuid ?? "");
            field(json, "reporterName", TextHelper.StripRichText(Client.Instance.Settings.DisplayName ?? ""));
            field(json, "where", report.Where);
            field(json, "server", report.Server);
            field(json, "messageId", report.MessageId);
            field(json, "sentAt", report.SentAt.ToString("o", CultureInfo.InvariantCulture));
            field(json, "senderUuid", report.SenderUuid);
            field(json, "senderName", report.SenderName);
            field(json, "recipientUuid", report.RecipientUuid);
            field(json, "recipientName", report.RecipientName);
            field(json, "text", report.Text);
            field(json, "reason", report.Reason, last: true);
            json.Append("}");

            try
            {
                File.AppendAllText(FilePath, json + Environment.NewLine, Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs("Couldn't save a message report to " + FilePath + ": " + e.Message, LogLevel.ERROR));
                return false;
            }
        }

        /// <summary>
        /// Adds <c>"name": "value",</c> to a JSON object being built.
        /// </summary>
        private static void field(StringBuilder json, string name, string value, bool last = false)
        {
            json.Append('"').Append(name).Append("\": \"");
            foreach (char c in value ?? "")
            {
                // Quotes, backslashes and invisible characters (like new lines) need escaping to stay valid JSON
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < ' ') json.Append("\\u").Append(((int) c).ToString("x4"));
                        else json.Append(c);
                        break;
                }
            }
            json.Append(last ? "\"" : "\", ");
        }
    }
}
