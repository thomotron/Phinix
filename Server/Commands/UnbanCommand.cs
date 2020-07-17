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
            new HelpEntry("unban username", new string[] {"username"}, "Unbans a user by their username")
        };

        public override bool Execute(List<string> args)
        {
            // Fail if we don't get enough arguments
            if (args.Count < 2) return false;

            if (args.ElementAt(0) == "username")
            {
                try
                {
                    // Ban the user by their username
                    Server.Authenticator.Unban(args.ElementAt(1));
                }
                catch (ArgumentException)
                {
                    // Failed to ban the user, they probably don't exist
                    Console.WriteLine("User " + args.ElementAt(1).Highlight(HighlightType.Username) + " does not exist");
                    return false;
                }

                Console.WriteLine("Unbanned user " + args.ElementAt(1).Highlight(HighlightType.Username));
            }
            else
            {
                // Unknown unban type, fail here
                return false;
            }

            return true;
        }
    }
}