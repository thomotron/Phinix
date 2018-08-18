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
        /// A list of help entries to be displayed when the help command is invoked.
        /// Should contain an entry for each argument combination.
        /// </summary>
        public abstract HelpEntry[] HelpEntries { get; }

        /// <summary>
        /// Run when the command is executed.
        /// </summary>
        /// <param name="args">Arguments entered by the user</param>
        /// <returns>Completed successfully</returns>
        public abstract bool Execute(List<string> args);

        /// <summary>
        /// Constructs a help entry when the help command is invoked targetting this command specifically.
        /// Gives the command free reign over writing to the console to allow for more advanced formatting capability.
        /// </summary>
        /// <param name="args">Arguments entered by the user</param>
        public virtual void GetSpecificHelp(List<string> args)
        {
            Console.WriteLine("No specific help is available for '{0}'", CommandName);
        }
    }
}