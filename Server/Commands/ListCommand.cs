using System;
using System.Collections.Generic;
using System.Text;
using Utils;

namespace PhinixServer
{
    internal class ListCommand : Command
    {
        public override string CommandName => "list";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("list", "Lists all currently connected users")
        };

        public override bool Execute(List<string> args)
        {
            var cols = 6;
            var rows = new List<string[]>
            {
                new[] { "User", "UUID", "Username", "Session", "Connection", "IP Address" }
            };

            // Fetch details for each connection
            foreach (string sessionId in Server.Authenticator.GetSessions())
            {
                if (!Server.Authenticator.TryGetConnectionId(sessionId, out string connectionId)) connectionId = "Unknown";
                if (!Server.Connections.TryGetEndpoint(connectionId, out string endpoint)) endpoint = "Unknown";
                if (!Server.Authenticator.TryGetUsername(connectionId, sessionId, out string username)) username = "Unknown";
                if (!Server.UserManager.TryGetUserUuid(username, out string uuid)) uuid = "Unknown";
                if (!Server.UserManager.TryGetDisplayName(uuid, out string displayName)) displayName = "Unknown";

                rows.Add(new[] { TextHelper.StripRichText(displayName), uuid, username, sessionId, connectionId, endpoint });
            }

            // Calculate column widths
            int[] columnWidths = GetColumnWidths(rows, cols);

            // Print everything
            var sb = new StringBuilder();
            for (int row = 0; row < rows.Count; row++)
            {
                for (int col = 0; col < cols - 1; col++)
                {
                    sb.Append(rows[row][col].PadRight(columnWidths[col])).Append(' ');
                }
                sb.AppendLine(rows[row][cols - 1]);

                Console.WriteLine(sb.ToString());
                sb.Clear();
            }

            return true;
        }

        /// <summary>
        /// Calculates the column widths required to fit the contents of each row.
        /// </summary>
        /// <param name="rows">Rows to calculate widths from.</param>
        /// <param name="columns">Number of columns.</param>
        /// <returns>Array containing the width of each column.</returns>
        private static int[] GetColumnWidths(IList<string[]> rows, int columns)
        {
            var columnWidths = new int[columns];
            for (int row = 0; row < rows.Count; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    columnWidths[col] = Math.Max(columnWidths[col], rows[row][col].Length);
                }
            }

            return columnWidths;
        }
    }
}
