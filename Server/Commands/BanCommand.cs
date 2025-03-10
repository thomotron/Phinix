using System;
using System.Collections.Generic;
using System.Linq;

namespace PhinixServer
{
    internal class BanCommand : Command
    {
        public override string CommandName => "ban";

        public override HelpEntry[] HelpEntries => new[]
        {
            new HelpEntry("ban", new[]{"UUID"}, "Bans a user by their UUID")
        };

        public override bool Execute(List<string> args)
        {
            if (args.Count < 1) return false;

            if (!Server.UserManager.TryBan(args.First()))
            {
                Console.WriteLine("User does not exist.");
            }
            else
            {
                Console.WriteLine("Banned user.");
            }

            return true;
        }
    }
}
