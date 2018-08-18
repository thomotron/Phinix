using System;
using System.Collections.Generic;
using System.Linq;

namespace PhinixServer
{
    /// <inheritdoc />
    public class LogCommand : Command
    {
        public override string CommandName => "log";
        
        public override HelpEntry[] HelpEntries => new HelpEntry[]
        {
            new HelpEntry("log", new string[]{"verbosity"}, "Sets the log verbosity for printing to the console. Valid levels are 0 (DEBUG) to 4 (FATAL).")
        };

        public override bool Execute(List<string> args)
        {
            if (args.Count < 1) return false;

            if (int.TryParse(args.ElementAt(0), out int severityInt)) // Parse the first argument
            {
                if (Enum.IsDefined(typeof(Severity), severityInt)) // Ensure that it is a valid severity level
                {
                    Server.Logger.MinimumDisplaySeverity = (Severity) severityInt;
                    Console.WriteLine("Set log verbosity to {0} ({1})", severityInt, (Severity) severityInt);
                    return true;
                }
            }

            return false;
        }

        public override void GetSpecificHelp(List<string> args)
        {
            Console.WriteLine("Sets the minimum verbosity required before a log message is printed to the console.\n" +
                              "Valid levels are:\n" +
                              " 0: DEBUG (Useful for developers, highly-verbose output)\n" +
                              " 1: INFO  (Default, events worth noting in normal use)\n" +
                              " 2: WARN  (Warnings of unusual activity or minor problems that do not need immediate attention)\n" +
                              " 3: ERROR (Problems that require immediate attention or could lead to instability)\n" +
                              " 4: FATAL (Major errors that will cause the server to break)\n" +
                              "\n" +
                              "All messages are still recorded in the log file regardless of this setting.");
        }
    }
}
