using System.Collections.Generic;

namespace PhinixServer
{
    public class ExitCommand : Command
    {
        public override string CommandName => "exit";

        public override bool Execute(List<string> args)
        {
            return false; // This should not be reachable since it will be handled within the Server class
        }

        public override string GetHelp()
        {
            return "Shuts down the server";
        }
    }
}