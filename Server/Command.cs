using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        /// <summary>
        /// Writes a 2-dimensional collection of strings to the console with automatic column sizing.
        /// </summary>
        /// <param name="rows">2-dimentional collection of values to write to the console</param>
        protected static void WriteTabulated(IEnumerable<IEnumerable<string>> rows)
        {
            int[] columnWidths = GetColumnWidths(rows);

            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                var cols = row.ToArray();

                for (int col = 0; col < cols.Length - 1; col++)
                {
                    sb.Append(cols[col].PadRight(columnWidths[col])).Append(' ');
                }
                sb.Append(cols[cols.Length - 1]);

                Console.WriteLine(sb.ToString());
                sb.Clear();
            }
        }

        /// <summary>
        /// Calculates the column widths required to fit the contents of each row.
        /// </summary>
        /// <param name="rows">Rows to calculate widths from.</param>
        /// <returns>Array containing the width of each column.</returns>
        protected static int[] GetColumnWidths(IEnumerable<IEnumerable<string>> rows)
        {
            var columnWidths = new List<int>();
            foreach (var row in rows)
            {
                var cols = row.ToArray();

                // Add additional columns as needed
                while (columnWidths.Count < cols.Length) columnWidths.Add(0);

                for (int col = 0; col < cols.Length; col++)
                {
                    columnWidths[col] = Math.Max(columnWidths[col], cols[col].Length);
                }
            }

            return columnWidths.ToArray();
        }
    }
}