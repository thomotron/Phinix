using System.Collections.Generic;
using System.Linq;
using Utils;

namespace PhinixServer
{
    internal class ListCommand : Command
    {
        public override string CommandName => "list";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("list", "Lists all currently connected users"),
            new HelpEntry("list users", "Lists all currently connected users"),
            new HelpEntry("list all", "Lists all users"),
            new HelpEntry("list bans", "Lists banned users")
        };

        public override bool Execute(List<string> args)
        {
            switch (args?.FirstOrDefault())
            {
                case null:
                case "users":
                    return ExecuteListUsers(true);
                case "all":
                    return ExecuteListUsers(false);
                case "bans":
                    return ExecuteListBans();
                default:
                    return false;
            }
        }

        private bool ExecuteListUsers(bool onlineOnly)
        {
            var rows = new List<string[]>
            {
                new[] { "User", "UUID", "Username", "Session", "Connection", "IP Address" }
            };

            // Fetch details for each connection
            foreach (string sessionId in Server.Authenticator.GetSessions())
            {
                if (!Server.Authenticator.TryGetConnectionId(sessionId, out string connectionId)) connectionId = "Unknown";

                if (!Server.Connections.TryGetEndpoint(connectionId, out string endpoint))
                {
                    if (onlineOnly)
                    {
                        continue;
                    }
                    else
                    {
                        endpoint = "Unknown";
                    }
                }

                if (!Server.Authenticator.TryGetUsername(connectionId, sessionId, out string username)) username = "Unknown";
                if (!Server.UserManager.TryGetUserUuid(username, out string uuid)) uuid = "Unknown";
                if (!Server.UserManager.TryGetDisplayName(uuid, out string displayName)) displayName = "Unknown";

                rows.Add(new[] { TextHelper.StripRichText(displayName), uuid, username, sessionId, connectionId, endpoint });
            }

            WriteTabulated(rows);

            return true;
        }

        private bool ExecuteListBans()
        {
            var rows = new List<string[]>
            {
                new[] { "User", "UUID", "Username" }
            };

            // Fetch details for each banned user
            foreach (string uuid in Server.UserManager.GetBanned())
            {
                if (!Server.UserManager.TryGetDisplayName(uuid, out string displayName)) displayName = "Unknown";
                if (!Server.UserManager.TryGetUsername(uuid, out string username)) username = "Unknown";

                rows.Add(new[] { TextHelper.StripRichText(displayName), uuid, username });
            }

            WriteTabulated(rows);

            return true;
        }
    }
}
