using System;

namespace UserManagement
{
    public class LoginEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the login attempt was successful.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Field that caused login failure.
        /// </summary>
        public LoginFailureReason FailureReason;

        /// <summary>
        /// Message from the server.
        /// </summary>
        public string FailureMessage;

        /// <summary>
        /// Creates a new successful <c>LoginEventArgs</c>.
        /// </summary>
        public LoginEventArgs()
        {
            this.Success = true;
        }

        /// <summary>
        /// Creates a new failed <c>LoginEventArgs</c> with the given failure reason and message.
        /// </summary>
        /// <param name="failureReason">Field that caused login failure</param>
        /// <param name="failureMessage">Message from the server</param>
        public LoginEventArgs(LoginFailureReason failureReason, string failureMessage)
        {
            this.FailureReason = failureReason;
            this.FailureMessage = failureMessage;
        }
    }
}