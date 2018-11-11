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
    }
}