using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Keeps our own animals and colonists after they leave (on a trade offer or in a market), so the very same pawn
    /// comes back if they're returned, instead of a copy rebuilt from the trade data.
    /// </summary>
    /// <remarks>
    /// The trade data can't carry everything about a pawn: relationships, memories, work priorities, policies, records
    /// and more are left behind (see ColonistConverter). That's unavoidable when a pawn moves to someone else's world,
    /// but when a trade is cancelled or market stock is taken back, it's our own pawn coming home. So instead of being
    /// thrown away when the server takes them, they're parked here (in the save file), and each one sent carries an
    /// <see cref="Trading.ProtoThing.OriginId"/> that names it. If that ID comes back to this world, the parked pawn
    /// returns. Pawns nobody sent back are let go after <see cref="KEEP_DAYS"/>.
    /// </remarks>
    public class ParkedPawns : GameComponent
    {
        private const int KEEP_DAYS = 30;

        private class Parked : IExposable
        {
            public Pawn Pawn;
            public string OriginId;
            public long ParkedAt;

            public void ExposeData()
            {
                // "Deep" saves the whole pawn, since it isn't on any map or in the world, so nothing else would save it
                Scribe_Deep.Look(ref Pawn, "pawn");
                Scribe_Values.Look(ref OriginId, "originId");
                Scribe_Values.Look(ref ParkedAt, "parkedAt");
            }
        }

        private List<Parked> parked = new List<Parked>();

        // RimWorld needs this exact constructor to create the component
        public ParkedPawns(Game game)
        {
        }

        /// <summary>
        /// The ID a pawn of ours is sent with: this world's random value and the pawn's ID, so it only ever matches here.
        /// </summary>
        public static string OriginIdOf(Pawn pawn)
        {
            if (pawn == null || Find.World?.info == null) return "";
            return Find.World.info.persistentRandomValue + ":" + pawn.ThingID;
        }

        /// <summary>
        /// Keeps a pawn that has left, instead of throwing it away. Must run on the main thread.
        /// </summary>
        public static void Park(Pawn pawn)
        {
            ParkedPawns component = Current.Game?.GetComponent<ParkedPawns>();
            if (component == null || pawn == null || pawn.Destroyed || pawn.Discarded) return;
            if (pawn.Spawned) pawn.DeSpawn();

            string originId = OriginIdOf(pawn);
            component.parked.RemoveAll(entry => entry.OriginId == originId);
            component.parked.Add(new Parked { Pawn = pawn, OriginId = originId, ParkedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            component.letGoOfOld();
        }

        /// <summary>
        /// Takes back the parked pawn a returning thing names, if it's one of ours. Must run on the main thread.
        /// </summary>
        /// <param name="originId">The returning thing's <see cref="Trading.ProtoThing.OriginId"/></param>
        /// <param name="pawn">The original pawn, ready to drop in</param>
        /// <returns>Whether it was one of ours</returns>
        public static bool TryTake(string originId, out Pawn pawn)
        {
            pawn = null;
            ParkedPawns component = Current.Game?.GetComponent<ParkedPawns>();
            if (component == null || string.IsNullOrEmpty(originId)) return false;

            Parked entry = component.parked.FirstOrDefault(p => p.OriginId == originId && p.Pawn != null && !p.Pawn.Destroyed && !p.Pawn.Discarded);
            if (entry == null) return false;

            component.parked.Remove(entry);
            pawn = entry.Pawn;
            return true;
        }

        /// <summary>
        /// Lets go of pawns parked longer than <see cref="KEEP_DAYS"/>: by then they've surely gone for good.
        /// </summary>
        private void letGoOfOld()
        {
            long cutoff = DateTimeOffset.UtcNow.AddDays(-KEEP_DAYS).ToUnixTimeSeconds();
            foreach (Parked entry in parked.Where(p => p.ParkedAt < cutoff).ToList())
            {
                parked.Remove(entry);
                if (entry.Pawn != null && !entry.Pawn.Destroyed && !entry.Pawn.Discarded)
                {
                    Find.WorldPawns.PassToWorld(entry.Pawn, PawnDiscardDecideMode.Discard);
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref parked, "phinixParkedPawns", LookMode.Deep);

            // Loading a save from before this existed gives null, so start an empty list instead
            if (parked == null) parked = new List<Parked>();
        }
    }
}
