using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhinixServer
{
    /// <summary>
    /// Appends messages with timestamps and severity levels to the console and disk.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// The minimum severity level for a message to be displayed in the console.
        /// </summary>
        public Severity MinimumDisplaySeverity;

        /// <summary>
        /// Path to the log file.
        /// </summary>
        private string logPath;

        /// <summary>
        /// Creates a new <c>Logger</c> instance.
        /// </summary>
        /// <param name="logPath">Log file path including extension</param>
        /// <param name="minimumDisplaySeverity">Minimum severity level for a message to be displayed in console</param>
        public Logger(string logPath, Severity minimumDisplaySeverity)
        {
            this.logPath = logPath;
            this.MinimumDisplaySeverity = minimumDisplaySeverity;
        }

        /// <summary>
        /// Appends a message to the console and log file with a timestamp and severity level.
        /// </summary>
        /// <param name="severity">Severity level</param>
        /// <param name="message">Message</param>
        public void Log(Severity severity, string message)
        {
            // Only write to the console if the severity meets the minimum
            if (severity >= MinimumDisplaySeverity) Console.WriteLine("[{0:u}][{1}] {2}", DateTime.UtcNow, severity.ToString(), message);
        }
    }

    public enum Severity
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL
    }
}
