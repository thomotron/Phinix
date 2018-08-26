using System;
using System.Collections.Generic;
using System.Reflection;
using Connections;

namespace PhinixServer
{
    /// <inheritdoc />
    public class VersionCommand : Command
    {
        public override string CommandName => "version";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("version", "Displays the version of the server and its modules")
        };

        public override bool Execute(List<string> args)
        {
            Console.WriteLine("Server: " + Server.Version);
            Console.WriteLine("Connections: " + NetCommon.Version);
            Console.WriteLine("UserManagement: " + UserManagement.UserManager.Version);

            return true;
        }
    }
}