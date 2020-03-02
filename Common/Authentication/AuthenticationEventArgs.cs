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
        public AuthFailureReason FailureReason;
        
        /// <summary>
        /// Failure message from the server.
        /// </summary>
        public string FailureMessage;

        /// <summary>
        /// Creates a new successful <see cref="AuthenticationEventArgs"/> instance.
        /// </summary>
        public AuthenticationEventArgs()
        {
            this.Success = true;
        }

        /// <summary>
        /// Creates a new failed <see cref="AuthenticationEventArgs"/> instance with the given reason and message.
        /// </summary>
        /// <param name="failureReason">Field that caused failure</param>
        /// <param name="failureMessage">Message provided by server</param>
        public AuthenticationEventArgs(AuthFailureReason failureReason, string failureMessage)
        {
            this.Success = false;
            this.FailureReason = failureReason;
            this.FailureMessage = failureMessage;
        }
    }
}