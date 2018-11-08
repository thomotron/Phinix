using System;

namespace Utils
{
    /// <summary>
    /// Event arguments containing a log message and whether it is an error.
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        /// <summary>
        /// The log message.
        /// </summary>
        public string Message;
        
        /// <summary>
        /// The level of severity for the message.
        /// </summary>
        public LogLevel LogLevel;
        
        public LogEventArgs(string message, LogLevel logLevel = LogLevel.INFO)
        {
            this.Message = message;
            this.LogLevel = logLevel;
        }
    }
}