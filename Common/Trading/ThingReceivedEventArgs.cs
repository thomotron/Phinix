using System;

namespace Trading
{
    public class ThingReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Incoming <c>Thing</c>
        /// </summary>
        public Thing Thing;
        public ThingReceivedEventArgs(Thing thing)
        {
            this.Thing = thing;
        }
    }
}