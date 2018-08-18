using System;
using System.Collections.Generic;
using System.Linq;
using PhinixServer;

namespace PhinixServer
{
    public class CommandInterpreter
    {
        /// <summary>
        /// The command dictionary. All valid commands and their handlers are contained in here.
        /// </summary>
        private static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>
        {
            { "help", new HelpCommand() },
            { "exit", new ExitCommand() }
        };

        /// <summary>
        /// Attempts to run a command with the given arguments.
        /// </summary>
        /// <param name="command">Command to run</param>
        /// <param name="args">Command arguments</param>
        public void Run(string command, List<string> args = null)
        {
            if (command == "help") // Help needs some special access to the command list so we run it here
            {
                PrintHelp(args);
            }
            else if (commands.ContainsKey(command)) // Is this a valid command?
            {
                commands[command].Execute(args);
            }
            else // Spit out an unknown command warning
            {
                Console.WriteLine($"Unknown command '{command}'");
            }
        }

        /// <summary>
        /// Prints out a list of commands or specific help text if a command is specified.
        /// </summary>
        /// <param name="args">Command </param>
        private void PrintHelp(List<string> args)
        {
            if (args.Count > 0) // Specific command and argument(s)
            {
                string subcommand = args.First();
                List<string> subargs = args.Count > 1 ? args.GetRange(1, args.Count - 2) : null; // Include additional arguments if provided
                if (commands.ContainsKey(subcommand))
                {
                    commands[subcommand].GetSpecificHelp(subargs);
                }
                else
                {
                    Console.WriteLine($"Unable to find help for '{subcommand}'");
                }
            }
            else // List all commands
            {
                foreach (Command command in commands.Values)
                {
                    // Iterate through all the help entries
                }
            }
        }
    }
}
