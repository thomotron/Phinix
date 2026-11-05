using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Finds the items a colony can put into a trade, a listing or a bounty.
    /// </summary>
    /// <remarks>
    /// "static" means you never make one of these with <c>new</c>, you just call <c>ColonyItems.Something()</c>.
    /// It's the C# way of writing a Python module full of plain functions.
    /// </remarks>
    public static class ColonyItems
    {
        /// <summary>
        /// Every item in the colony that's allowed to leave it, looking in the same places as the trade window: just
        /// stockpiles and shelves, or the whole map if the player turned on "all items tradable" in Phinix's settings.
        /// </summary>
        /// <returns>Tradeable items</returns>
        public static IEnumerable<Thing> TradeableItems()
        {
            IEnumerable<Map> homeMaps = Find.Maps.Where(map => map.IsPlayerHome);
            IEnumerable<Thing> things;
            if (Client.Instance.Settings.AllItemsTradable)
            {
                // Everything except what's still hidden under fog, which the colony hasn't found yet
                things = homeMaps.SelectMany(map => map.listerThings.AllThings).Where(thing => !thing.Position.Fogged(thing.Map));
            }
            else
            {
                things = homeMaps
                    .SelectMany(map => map.haulDestinationManager.AllGroups)
                    .SelectMany(group => group.HeldThings);
            }

            // Only loose items: no pawns, corpses or packed-up furniture. Spawned means it's actually on the map
            // (things being carried or already in a trade don't count)
            return things.Where(thing => thing.Spawned &&
                                         thing.def.category == ThingCategory.Item &&
                                         !thing.def.IsCorpse &&
                                         thing.def.minifiedDef == null &&
                                         !(thing is MinifiedThing) &&
                                         CanLeaveOnItsOwn(thing));
        }

        /// <summary>
        /// Whether an item may be traded, sold or given away on its own. Biocoded items can't: they're locked to one of
        /// our colonists, who wouldn't be going with them. They can still leave on that colonist (see
        /// <see cref="ColonistConverter.AddGearCopies"/>).
        /// </summary>
        /// <param name="thing">Item to check</param>
        /// <returns>Whether it can be offered</returns>
        public static bool CanLeaveOnItsOwn(Thing thing)
        {
            return !CompBiocodable.IsBiocoded(thing);
        }

        /// <summary>
        /// Every animal and colonist that can leave the colony: tame animals and free colonists on our home maps, the
        /// same ones the trade window offers (see <see cref="ColonistConverter.CanTrade"/> for who counts).
        /// </summary>
        /// <returns>Tradeable pawns</returns>
        public static IEnumerable<Pawn> TradeablePawns()
        {
            // None at all while the server has colonist and animal trading switched off
            if (!Client.Instance.FeatureEnabled(UserManagement.Features.PAWN_TRADING)) return Enumerable.Empty<Pawn>();

            List<Map> homeMaps = Find.Maps.Where(map => map.IsPlayerHome).ToList();
            IEnumerable<Pawn> animals = homeMaps.SelectMany(map => map.mapPawns.SpawnedColonyAnimals).Where(animal => !animal.Dead);
            IEnumerable<Pawn> colonists = homeMaps.SelectMany(map => map.mapPawns.FreeColonistsSpawned).Where(ColonistConverter.CanTrade);
            return animals.Concat(colonists);
        }

        /// <summary>
        /// Whether an item meets a minimum quality. Items without a quality always do.
        /// </summary>
        /// <param name="thing">Item to check</param>
        /// <param name="minimum">Lowest acceptable quality</param>
        /// <returns>Whether it's good enough</returns>
        public static bool MeetsQuality(Thing thing, Quality minimum)
        {
            // TryGetQuality gives back false for things that don't have a quality at all, like steel
            if (!thing.TryGetQuality(out QualityCategory quality)) return true;

            // Reuse the server's rule so the client and server can never disagree.
            // QualityCategory and our Quality enum use the same numbers (Awful = 0 ... Legendary = 6), so a cast works
            return Agora.ServerAgora.QualityIsEnough((Quality) quality, minimum);
        }

        /// <summary>
        /// How many of an item the colony has that are at least a given quality.
        /// </summary>
        /// <param name="def">Item to count</param>
        /// <param name="minimum">Lowest acceptable quality</param>
        /// <returns>Total stack count</returns>
        public static int Count(ThingDef def, Quality minimum)
        {
            return TradeableItems()
                .Where(thing => thing.def == def && MeetsQuality(thing, minimum))
                .Sum(thing => thing.stackCount);
        }
    }
}
