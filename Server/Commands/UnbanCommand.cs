using System;
using System.Collections.Generic;
using System.Linq;

namespace PhinixServer
{
    internal class UnbanCommand : Command
    {
        public override string CommandName => "unban";

        public override HelpEntry[] HelpEntries => new[]
        {
            new HelpEntry("unban", new[]{"UUID"}, "Unbans a user by their UUID")
        };

        public override bool Execute(List<string> args)
        {
            if (args.Count < 1) return false;

            if (!Server.UserManager.TryUnban(args.First()))
            {
                Console.WriteLine("User is not banned.");
            }
            else
            {
                Console.WriteLine("Unbanned user.");
            }

            return true;
        }
    }
}