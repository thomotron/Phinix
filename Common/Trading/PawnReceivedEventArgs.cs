using System;

namespace Trading
{
    public class PawnReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Incoming <see cref="Pawn"/>
        /// </summary>
        public ProtoPawn Pawn;

        /// <summary>
        /// Sender's UUID.
        /// </summary>
        public string SenderUuid;

        public PawnReceivedEventArgs(string senderUuid, ProtoPawn pawn)
        {
            this.SenderUuid = senderUuid;
            this.Pawn = pawn;
        }
    }
}