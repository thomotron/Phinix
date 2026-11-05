using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Drops things that arrived (from a trade, market, bounty, contract or gift) into the colony by drop pod, or keeps
    /// them until there's a colony to drop them into.
    /// </summary>
    /// <remarks>
    /// The server lets go of things once it has sent them, so they must never be thrown away here. A player can have
    /// no home map (only caravans), or things can arrive while another save is loading. Those wait in the save game
    /// (see <see cref="PendingDropsGameComponent"/>), or in memory if no game is loaded at all, and come down as soon
    /// as there's a home map.
    /// </remarks>
    public static class PendingDrops
    {
        /// <summary>
        /// Things waiting while no game was loaded at all. Moved into the save as soon as one is.
        /// </summary>
        private static readonly List<Thing> waitingWithoutGame = new List<Thing>();

        /// <summary>
        /// Drops things in now if there's a map, otherwise keeps them for later. Must run on the main thread.
        /// </summary>
        /// <param name="things">Things not on any map</param>
        /// <returns>Where they landed, or nothing if they're waiting</returns>
        public static LookTargets DropOrKeep(IEnumerable<Thing> things)
        {
            List<Thing> list = splitOversizedStacks(mergeStacks(things));
            if (list.Count == 0) return null;

            Map map = targetMap();
            if (map != null) return drop(list, map);

            keep(list);
            Messages.Message("Phinix_pendingDrops_waiting".Translate(), MessageTypeDefOf.CautionInput, historical: true);
            return null;
        }

        /// <summary>
        /// Drops in anything that has been waiting, once there's somewhere to drop it. Called every frame from
        /// <see cref="Client.Update"/>, so it does almost nothing when there's nothing waiting.
        /// </summary>
        public static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null) return;

            PendingDropsGameComponent component = Current.Game.GetComponent<PendingDropsGameComponent>();
            if (component == null) return;
            if (waitingWithoutGame.Count > 0)
            {
                component.Things.AddRange(waitingWithoutGame);
                waitingWithoutGame.Clear();
            }
            if (component.Things.Count == 0) return;

            Map map = targetMap();
            if (map == null) return;

            List<Thing> things = component.Things.Where(thing => thing != null && !thing.Destroyed).ToList();
            component.Things.Clear();
            if (things.Count == 0) return;

            LookTargets where = drop(things, map);
            Messages.Message("Phinix_pendingDrops_arrived".Translate(things.Count), where, MessageTypeDefOf.PositiveEvent, historical: true);
        }

        /// <summary>
        /// The map deliveries go to, or null if there isn't one right now.
        /// </summary>
        private static Map targetMap()
        {
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null) return null;
            Map map = Client.Instance.Settings.DropCurrentMap ? Find.CurrentMap : Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            return map != null && Find.Maps.Contains(map) ? map : null;
        }

        /// <summary>
        /// Where trades land on a map: the player's trade drop spot if they've placed one, otherwise the same
        /// place the game drops goods bought from orbital traders.
        /// </summary>
        public static IntVec3 DropSpot(Map map)
        {
            ThingDef spotDef = DefDatabase<ThingDef>.GetNamedSilentFail("PhinixDropSpot");
            if (spotDef != null)
            {
                foreach (Thing spot in map.listerThings.ThingsOfDef(spotDef))
                {
                    if (spot.Spawned && spot.Faction == Faction.OfPlayer) return spot.Position;
                }
            }
            return DropCellFinder.TradeDropSpot(map);
        }

        private static LookTargets drop(List<Thing> things, Map map)
        {
            IntVec3 dropSpot = DropSpot(map);

            // Note the pods already falling, so the new ones can be told apart
            HashSet<Thing> alreadyFalling = new HashSet<Thing>(map.listerThings.ThingsOfDef(ThingDefOf.DropPodIncoming));

            // Not forbidden, like things bought from an orbital trader, so they get hauled in without allowing each one.
            // Things keep whatever forbidden state they had in the sender's game, so clear that too
            foreach (Thing thing in things)
            {
                if (!(thing is Pawn)) thing.SetForbidden(false, warnOnFail: false);
            }
            DropPodUtility.DropThingsNear(dropSpot, map, things, canRoofPunch: false, forbid: false);

            // Point at every spot a pod is coming down, so the letter shows where it all landed, not just the middle
            List<GlobalTargetInfo> landings = new List<GlobalTargetInfo> { new GlobalTargetInfo(dropSpot, map) };
            foreach (Thing pod in map.listerThings.ThingsOfDef(ThingDefOf.DropPodIncoming))
            {
                if (!alreadyFalling.Contains(pod) && pod.Position != dropSpot) landings.Add(new GlobalTargetInfo(pod.Position, map));
            }
            return new LookTargets(landings);
        }

        private static void keep(List<Thing> things)
        {
            PendingDropsGameComponent component = Current.Game?.GetComponent<PendingDropsGameComponent>();
            if (component != null) component.Things.AddRange(things);
            else waitingWithoutGame.AddRange(things);
        }

        /// <summary>
        /// Merges part stacks that the game would stack together anyway (same item, and the game's own rules for
        /// things like meals' ingredients), so ten single meals come in as one stack and one pod, not ten.
        /// Pawns and things that don't stack are passed through untouched.
        /// </summary>
        private static List<Thing> mergeStacks(IEnumerable<Thing> things)
        {
            List<Thing> result = new List<Thing>();
            foreach (Thing thing in things)
            {
                if (thing == null) continue;
                if (!(thing is Pawn) && thing.def.stackLimit > 1)
                {
                    // Pour it into any earlier part stack with room; whatever doesn't fit stays as its own stack
                    foreach (Thing earlier in result)
                    {
                        if (earlier is Pawn || earlier.stackCount >= earlier.def.stackLimit || !earlier.CanStackWith(thing)) continue;
                        if (earlier.TryAbsorbStack(thing, respectStackLimit: true)) break;
                    }
                    if (thing.Destroyed) continue;
                }
                result.Add(thing);
            }
            return result;
        }

        /// <summary>
        /// Splits any stack bigger than its item's stack limit into stacks that fit, e.g. 2,000 silver from a market's
        /// till into four stacks of 500. Things that already fit are passed through untouched.
        /// </summary>
        private static List<Thing> splitOversizedStacks(IEnumerable<Thing> things)
        {
            List<Thing> result = new List<Thing>();
            foreach (Thing thing in things)
            {
                // SplitOff makes a new thing with the same def, stuff, quality and hit points and takes the count off this one
                int limit = System.Math.Max(1, thing.def.stackLimit);
                while (thing.stackCount > limit) result.Add(thing.SplitOff(limit));
                result.Add(thing);
            }
            return result;
        }
    }

    /// <summary>
    /// Keeps things waiting for a home map (see <see cref="PendingDrops"/>) inside the save file.
    /// </summary>
    /// <remarks>
    /// RimWorld creates one of every GameComponent subclass for each game automatically, so defining this class is
    /// enough to hook it in.
    /// </remarks>
    public class PendingDropsGameComponent : GameComponent
    {
        public List<Thing> Things = new List<Thing>();

        // RimWorld needs this exact constructor to create the component
        public PendingDropsGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            // "Deep" saves the whole thing, since it isn't on any map, so nothing else would save it
            Scribe_Collections.Look(ref Things, "phinixPendingDrops", LookMode.Deep);

            // Loading a save from before this existed gives null, so start an empty list instead
            if (Things == null) Things = new List<Thing>();
        }
    }
}
