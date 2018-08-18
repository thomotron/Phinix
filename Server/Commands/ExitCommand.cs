using System.Collections.Generic;

namespace PhinixServer
{
    /// <inheritdoc />
    /// <summary>
    /// Command handler for the exit command. This is only implemented to be displayed by the help command, all processing for the exit command occurs in the <c>Server</c> class.
    /// </summary>
    public class ExitCommand : Command
    {
        public override string CommandName => "exit";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("exit", "Shuts down the server")
        };

        public override bool Execute(List<string> args)
        {
            return false; // This should not be reachable since it will be handled within the Server class
        }
    }
}