using System.Collections.Generic;

namespace PhinixServer
{
    /// <inheritdoc />
    /// <summary>
    /// Command handler for the help command. This is only implemented to be displayed by the help command, all processing for the help command occurs in the <see cref="CommandInterpreter"/> class.
    /// </summary>
    public class HelpCommand : Command
    {
        public override string CommandName => "help";

        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("help", "Lists all available commands"), 
            new HelpEntry("help", new string[]{"command"}, "Displays detailed help for a command"), 
            new HelpEntry("help", new string[]{"command", "arguments..."}, "Displays detailed help for a command using the given arguments")
        };

        public override bool Execute(List<string> args)
        {
            return false; // This should not be reachable since it will be handled within the CommandInterpreter class
        }
    }
}