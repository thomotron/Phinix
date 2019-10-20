using System.Text;

namespace PhinixServer
{
    /// <summary>
    /// A help entry read by the <see cref="CommandInterpreter"/> to help with consistent formatting.
    /// </summary>
    public class HelpEntry
    {
        /// <summary>
        /// The base command entered by the user.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Optional arguments suffixed to the base command.
        /// </summary>
        public string[] Arguments { get; }

        /// <summary>
        /// A description of what the command and argument combination will do when entered by the user.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Constructs a new <see cref="HelpEntry"/> without any arguments.
        /// </summary>
        /// <param name="commandName">Name of the command as typed by the user</param>
        /// <param name="text">Description of what the command does</param>
        public HelpEntry(string commandName, string text)
        {
            this.CommandName = commandName;
            this.Arguments = new string[0];
            this.Text = text;
        }

        /// <summary>
        /// Constructs a new <see cref="HelpEntry"/> without any arguments.
        /// </summary>
        /// <param name="commandName">Name of the command as typed by the user</param>
        /// <param name="arguments">List of arguments required to run this combination</param>
        /// <param name="text">Description of what the command does</param>
        public HelpEntry(string commandName, string[] arguments, string text)
        {
            this.CommandName = commandName;
            this.Arguments = arguments;
            this.Text = text;
        }

        public string ConstructCommand()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(CommandName);
            foreach (string argument in Arguments)
            {
                sb.Append($" <{argument}>");
            }

            return sb.ToString();
        }
    }
}