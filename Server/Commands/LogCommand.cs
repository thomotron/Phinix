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
            new HelpEntry("log console", new string[]{"verbosity"}, "Sets the verbosity for printing to the console. Valid levels are 0 (DEBUG) to 4 (FATAL)."),
            new HelpEntry("log file",    new string[]{"verbosity"}, "Sets the verbosity for writing to the log. Valid levels are 0 (DEBUG) to 4 (FATAL).")
        };

        public override bool Execute(List<string> args)
        {
            if (args.Count < 2) return false;

            if (!int.TryParse(args.ElementAt(1), out int verbosityInt)) return false;

            if (!Enum.IsDefined(typeof(Verbosity), verbosityInt)) return false;

            if (args.ElementAt(0) == "console") // Set the console verbosity
            {
                Server.Config.DisplayVerbosity = (Verbosity) verbosityInt;
                Server.Logger.DisplayVerbosity = (Verbosity) verbosityInt;
                Console.WriteLine("Set console verbosity to {0} ({1})", verbosityInt, (Verbosity) verbosityInt);
            }
            else if (args.ElementAt(0) == "file") // Set the log file verbosity
            {
                Server.Config.LogVerbosity = (Verbosity) verbosityInt;
                Server.Logger.LogVerbosity = (Verbosity) verbosityInt;
                Console.WriteLine("Set log verbosity to {0} ({1})", verbosityInt, (Verbosity) verbosityInt);
            }
            else
            {
                // We don't know which verbosity to set so fail here
                return false;
            }

            return true;
        }

        public override void GetSpecificHelp(List<string> args)
        {
            string printDestination = "either the console or the log file";
            if (args.Count > 0)
            {
                switch (args.First())
                {
                    case "console":
                        printDestination = "the console";
                        break;
                    case "file":
                        printDestination = "the log file";
                        break;
                    default:
                        Console.WriteLine("Unknown argument '{0}'", args.First());
                        return;
                }
            }

            Console.WriteLine("Sets the minimum verbosity required before a log message is printed to " + printDestination + ".\n" +
                              "Valid levels are:\n" +
                              " 0: DEBUG (Useful for developers, highly-verbose output)\n" +
                              " 1: INFO  (Default, events worth noting in normal use)\n" +
                              " 2: WARN  (Warnings of unusual activity or minor problems that do not need immediate attention)\n" +
                              " 3: ERROR (Problems that require immediate attention or could lead to instability)\n" +
                              " 4: FATAL (Major errors that will cause the server to break)");
        }
    }
}
