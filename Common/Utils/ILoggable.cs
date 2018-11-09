using System;

namespace Utils
{
    /// <summary>
    /// Interface for common logging functionality.
    /// Implementing classes may raise log entries through the <c>OnLogEntry</c> event.
    /// </summary>
    public interface ILoggable
    {
        /// <summary>
        /// Raised when a log entry is available.
        /// </summary>
        event EventHandler<LogEventArgs> OnLogEntry;
        
        /// <summary>
        /// Raises the <c>OnLogEntry</c> event with the given <c>LogEventArgs</c>.
        /// </summary>
        /// <param name="e">Event arguments</param>
        void RaiseLogEntry(LogEventArgs e);
    }
}