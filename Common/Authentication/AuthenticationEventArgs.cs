using System;

namespace Authentication
{
    public class AuthenticationEventArgs : EventArgs
    {
        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        public bool Success;
        
        /// <summary>
        /// Which input caused the authentication failure.
        /// </summary>
        public FailureReason FailureReason;
        
        /// <summary>
        /// Failure message from the server.
        /// </summary>
        public string FailureMessage;

        /// <summary>
        /// Creates a new successful <c>AuthenticationEventArgs</c> instance.
        /// </summary>
        public AuthenticationEventArgs()
        {
            this.Success = true;
        }

        /// <summary>
        /// Creates a new failed <c>AuthenticationEventArgs</c> instance with the given reason and message.
        /// </summary>
        /// <param name="failureReason">Field that caused failure</param>
        /// <param name="failureMessage">Message provided by server</param>
        public AuthenticationEventArgs(FailureReason failureReason, string failureMessage)
        {
            this.Success = false;
            this.FailureReason = failureReason;
            this.FailureMessage = failureMessage;
        }
    }
}