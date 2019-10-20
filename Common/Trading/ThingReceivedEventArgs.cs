using System;

namespace Trading
{
    public class ThingReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Incoming <see cref="Thing"/>
        /// </summary>
        public ProtoThing Thing;

        /// <summary>
        /// Sender's UUID.
        /// </summary>
        public string SenderUuid;

        public ThingReceivedEventArgs(string senderUuid, ProtoThing thing)
        {
            this.SenderUuid = senderUuid;
            this.Thing = thing;
        }
    }
}