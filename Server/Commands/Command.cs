using System;
using System.Collections.Generic;

namespace PhinixServer
{
    /// <summary>
    /// A console command entered by the user and its associated handler methods.
    /// </summary>
    public abstract class Command
    {
        /// <summary>
        /// The command entered in the console.
        /// Should be all lowercase.
        /// </summary>
        public abstract string CommandName { get; }

        /// <summary>
        /// Run when the command is executed.
        /// </summary>
        /// <param name="args">Arguments entered by the user</param>
        /// <returns>Completed successfully</returns>
        public abstract bool Execute(List<string> args);

        /// <summary>
        /// Constructs a help entry when the help command is invoked.
        /// Should be kept to a single line.
        /// </summary>
        /// <returns>Constructed help entry</returns>
        public abstract string GetHelp();

        /// <summary>
        /// Constructs a help entry when the help command is invoked targetting this command specifically.
        /// Gives the command free reign over writing to the console.
        /// </summary>
        /// <param name="args">Arguments entered by the user</param>
        public virtual void GetSpecificHelp(List<string> args)
        {
            Console.WriteLine(CommandName + " " + GetHelp());
        }
    }
}