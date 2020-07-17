using System;
using System.Collections.Generic;
using System.Linq;

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
            { "version", new VersionCommand()},
            { "exit", new ExitCommand() },
            { "log", new LogCommand() },
            { "ban", new BanCommand() },
            { "unban", new UnbanCommand() }
        };

        /// <summary>
        /// Attempts to run a command with the given arguments.
        /// </summary>
        /// <param name="command">Command to run</param>
        /// <param name="args">Command arguments</param>
        public void Run(string command, List<string> args)
        {
            if (command == "help") // Help needs some special access to the command list so we run it here
            {
                PrintHelp(args);
            }
            else if (commands.ContainsKey(command)) // Is this a valid command?
            {
                if (!commands[command].Execute(args)) // Try to execute the command and report if it failed
                {
                    Console.WriteLine("Failed to run command '{0}' with arguments '{1}'", command, string.Join(" ", args.ToArray()));
                }
            }
            else // Spit out an unknown command warning
            {
                Console.WriteLine($"Unknown command '{command}'");
            }
        }

        /// <summary>
        /// Prints out a list of commands or specific help text if a command is specified.
        /// </summary>
        /// <param name="args">Command arguments</param>
        private void PrintHelp(List<string> args)
        {
            if (args.Count > 0) // Specific command and argument(s)
            {
                string subcommand = args.First();
                List<string> subargs = args.Count > 1 ? args.GetRange(1, args.Count - 1) : new List<string>(); // Include additional arguments if provided
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
                // Determine how wide the command column should be by finding the longest command string
                int columnWidth = commands.Values.Max(command => command.HelpEntries.Max(entry => entry.ConstructCommand().Length));
                foreach (Command command in commands.Values)
                {
                    foreach (HelpEntry entry in command.HelpEntries)
                    {
                        // Print out the command and arguments padded to the column width, then the description
                        // The padding size does not need to be compile-time constant, so using columnWidth will work nicely
                        // A negative column width is also used to align everything to the left rather than the right
                        Console.WriteLine("{0,-" + columnWidth + "} {1}", entry.ConstructCommand(), entry.Text);
                    }
                }
            }
        }
    }
}
