using System;
using System.Collections.Generic;
using System.Linq;
using Utils;

namespace PhinixServer
{
    public class UnbanCommand : Command
    {
        public override string CommandName => "unban";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("unban", new string[] {"username or UUID"}, "Unbans a user by their username or UUID")
        };

        public override bool Execute(List<string> args)
        {
            // Fail if we don't get enough arguments
            if (args.Count < 1) return false;

            string name = args.ElementAt(0);
            if (Server.Authenticator.TryGetSessionId(name, out string _)) // Get the session for this user, proving they exist
            {
                // Unban by username
                Server.Authenticator.Unban(name);
                Console.WriteLine("Unbanned user " + name.Highlight(HighlightType.Username) + " by username");
            }
            else if (Server.UserManager.TryGetLoggedIn(name, out bool _)) // Get the user's logged-in state, proving they exist
            {
                // Unban by UUID
                //Server.UserManager.Unban(name);
                Console.WriteLine("Unbanned user " + name.Highlight(HighlightType.UUID) + " by UUID");
            }
            else
            {
                // Unknown ban type, fail here
                Console.WriteLine("Unknown user " + name);
                return false;
            }

            return true;
        }
    }
}