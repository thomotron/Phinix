using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Remembers which deliveries (trade results, market and bounty deliveries, gifts) this colony has already
    /// received, so one the server sends again doesn't arrive twice.
    /// </summary>
    /// <remarks>
    /// The server keeps each delivery until our client confirms it arrived, and sends it again on the next login if the
    /// confirmation never reached it (say the connection dropped just after the items landed). Then it's only confirmed
    /// again, not dropped in a second time. Kept in the save file, and forgotten after <see cref="KEEP_DAYS"/>.
    /// </remarks>
    public class DeliveryLog : GameComponent
    {
        private const int KEEP_DAYS = 60;

        /// <summary>
        /// Delivery keys (e.g. "gift:" + its ID) and when each arrived, in Unix seconds.
        /// </summary>
        private Dictionary<string, long> received = new Dictionary<string, long>();

        // RimWorld needs this exact constructor to create the component
        public DeliveryLog(Game game)
        {
        }

        /// <summary>
        /// Whether this colony already received the delivery with this key. False when no game is loaded.
        /// </summary>
        public static bool AlreadyReceived(string key)
        {
            DeliveryLog log = Current.Game?.GetComponent<DeliveryLog>();
            return log != null && !string.IsNullOrEmpty(key) && log.received.ContainsKey(key);
        }

        /// <summary>
        /// Notes that the delivery with this key has arrived.
        /// </summary>
        public static void Remember(string key)
        {
            DeliveryLog log = Current.Game?.GetComponent<DeliveryLog>();
            if (log == null || string.IsNullOrEmpty(key)) return;
            log.received[key] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public override void ExposeData()
        {
            // Forget old ones before saving, so the list doesn't grow forever
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                long cutoff = DateTimeOffset.UtcNow.AddDays(-KEEP_DAYS).ToUnixTimeSeconds();
                received = received.Where(entry => entry.Value >= cutoff).ToDictionary(entry => entry.Key, entry => entry.Value);
            }

            Scribe_Collections.Look(ref received, "phinixReceivedDeliveries", LookMode.Value, LookMode.Value);

            // Loading a save from before this existed gives null, so start an empty one instead
            if (received == null) received = new Dictionary<string, long>();
        }
    }
}
