using System;
using System.Collections.Generic;
using System.Reflection;

namespace PhinixServer
{
    public class VersionCommand : Command
    {
        public override string CommandName => "version";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("version", "Displays the version of the server and its modules")
        };

        public override bool Execute(List<string> args)
        {
            Console.WriteLine("Server: " + Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Connections: " + Assembly.GetAssembly(typeof(Connections.NetServer)).GetName().Version);

            return true;
        }
    }
}