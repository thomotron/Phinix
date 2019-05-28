using System;

namespace Utils
{
    /// <summary>
    /// Interface for common logging functionality.
    /// Implementing classes may raise log entries through the <see cref="OnLogEntry"/> event.
    /// </summary>
    public interface ILoggable
    {
        /// <summary>
        /// Raised when a log entry is available.
        /// </summary>
        event EventHandler<LogEventArgs> OnLogEntry;
        
        /// <summary>
        /// Raises the <see cref="OnLogEntry"/> event with the given <see cref="LogEventArgs"/>.
        /// </summary>
        /// <param name="e">Event arguments</param>
        void RaiseLogEntry(LogEventArgs e);
    }
}