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
            new HelpEntry("ban username", new string[] {"username"}, "Bans a user by their username")
        };

        public override bool Execute(List<string> args)
        {
            // Fail if we don't get enough arguments
            if (args.Count < 2) return false;

            if (args.ElementAt(0) == "username")
            {
                try
                {
                    // Ban the user
                    Server.Authenticator.Ban(args.ElementAt(1));
                }
                catch (ArgumentException)
                {
                    // Failed to ban the user, they probably don't exist
                    Console.WriteLine("User " + args.ElementAt(1).Highlight(HighlightType.Username) + " does not exist");
                    return false;
                }

                Console.WriteLine("Banned user " + args.ElementAt(1).Highlight(HighlightType.Username));
            }
            else
            {
                // Unknown ban type, fail here
                return false;
            }

            return true;
        }
    }
}