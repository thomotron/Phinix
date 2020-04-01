using System;
using System.IO;
using System.Runtime.Serialization;

namespace PhinixServer
{
    /// <summary>
    /// Appends messages with timestamps and verbosity levels to the console and disk.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// The minimum verbosity level for a message to be displayed in the console.
        /// </summary>
        public Verbosity DisplayVerbosity;

        /// <summary>
        /// The minimum verbosity level for a message to be recorded in the log file.
        /// </summary>
        public Verbosity LogVerbosity;

        /// <summary>
        /// Path to the log file.
        /// </summary>
        private string logPath;

        /// <summary>
        /// Creates a new <see cref="Logger"/> instance.
        /// </summary>
        /// <param name="logPath">Log file path including extension</param>
        /// <param name="displayVerbosity">Minimum verbosity level for a message to be displayed in console</param>
        /// <param name="logVerbosity">Minimum verbosity for a message to be displayed in the log</param>
        public Logger(string logPath, Verbosity displayVerbosity, Verbosity logVerbosity = Verbosity.INFO)
        {
            // Make sure that the log path isn't empty
            if (string.IsNullOrEmpty(logPath)) throw new ArgumentException("Must specify a non-empty log path!", nameof(logPath));

            this.logPath = logPath;
            this.DisplayVerbosity = displayVerbosity;
            this.LogVerbosity = logVerbosity;
        }

        /// <summary>
        /// Appends a message to the console and log file with a timestamp and verbosity level. Optionally prepends a
        /// module name before the message.
        /// </summary>
        /// <param name="verbosity">Verbosity level</param>
        /// <param name="message">Message</param>
        /// <param name="module">Module prefix</param>
        public void Log(Verbosity verbosity, string message, string module = null)
        {
            string formattedEntry = string.Format("[{0:u}][{1}]{2} {3}{4}",
                DateTime.UtcNow,
                verbosity.ToString(),
                module != null ? "[" + module + "]" : "",
                message,
                Environment.NewLine
            );

            // Only write to the console if the verbosity meets the minimum
            if (verbosity >= DisplayVerbosity) Console.Write(formattedEntry);

            // Only write to disk if the verbosity meets the minimum
            if (verbosity >= LogVerbosity) File.AppendAllText(logPath, formattedEntry);
        }
    }

    [DataContract]
    public enum Verbosity
    {
        [EnumMember]
        DEBUG,
        [EnumMember]
        INFO,
        [EnumMember]
        WARN,
        [EnumMember]
        ERROR,
        [EnumMember]
        FATAL
    }
}
