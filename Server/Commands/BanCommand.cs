using System;
using System.Collections.Generic;
using System.Linq;
using Utils;

namespace PhinixServer
{
    public class BanCommand : Command
    {
        public override string CommandName => "ban";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("ban", new string[] {"username or UUID"}, "Bans a user by their username or UUID")
        };

        public override bool Execute(List<string> args)
        {
            // Fail if we don't get enough arguments
            if (args.Count < 1) return false;

            string name = args.ElementAt(0);
            if (Server.Authenticator.TryGetSessionId(name, out string _)) // Get the session for this user, proving they exist
            {
                // Ban by username
                Server.Authenticator.Ban(name);
                Console.WriteLine("Banned user " + name.Highlight(HighlightType.Username) + " by username");
            }
            else if (Server.UserManager.TryGetLoggedIn(name, out bool _)) // Get the user's logged-in state, proving they exist
            {
                // Ban by UUID
                //Server.UserManager.Ban(name);
                Console.WriteLine("Banned user " + name.Highlight(HighlightType.UUID) + " by UUID");
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