using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// Path to the log file.
        /// </summary>
        private string logPath;

        /// <summary>
        /// Creates a new <c>Logger</c> instance.
        /// </summary>
        /// <param name="logPath">Log file path including extension</param>
        /// <param name="displayVerbosity">Minimum verbosity level for a message to be displayed in console</param>
        public Logger(string logPath, Verbosity displayVerbosity)
        {
            this.logPath = logPath;
            this.DisplayVerbosity = displayVerbosity;
        }

        /// <summary>
        /// Appends a message to the console and log file with a timestamp and verbosity level.
        /// </summary>
        /// <param name="verbosity">Verbosity level</param>
        /// <param name="message">Message</param>
        public void Log(Verbosity verbosity, string message)
        {
            // Only write to the console if the verbosity meets the minimum
            if (verbosity >= DisplayVerbosity) Console.WriteLine("[{0:u}][{1}] {2}", DateTime.UtcNow, verbosity.ToString(), message);
        }
    }

    public enum Verbosity
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL
    }
}
