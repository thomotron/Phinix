using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Turns the colony's items and pawns into rows for a pick list like the game's caravan and trade windows, and
    /// takes the picked items off the map.
    /// </summary>
    /// <remarks>
    /// The game calls one row of those lists a <see cref="TransferableOneWay"/>: every thing that counts as "the same"
    /// (all the stacks of steel, or all the cloth dusters of one quality and about the same wear), plus how many of
    /// them the player has picked (<c>CountToTransfer</c>). Making our rows the same way means we can use the game's
    /// own list drawing and its &lt;&lt; &lt; [n] &gt; &gt;&gt; buttons.
    /// </remarks>
    public static class ColonyTransferables
    {
        /// <summary>
        /// One row per kind of tradeable item in the colony, grouped the way the caravan window groups them.
        /// </summary>
        /// <param name="include">Optional extra rule for which items to list (e.g. no silver in markets)</param>
        /// <returns>Rows, nothing picked yet</returns>
        public static List<TransferableOneWay> Items(Func<Thing, bool> include = null)
        {
            List<TransferableOneWay> rows = new List<TransferableOneWay>();
            foreach (Thing thing in ColonyItems.TradeableItems().Distinct())
            {
                if (include == null || include(thing)) addToRow(thing, rows);
            }
            return rows;
        }

        /// <summary>
        /// One row per tradeable colonist, and one per group of animals that are alike (same kind, sex and age, unnamed),
        /// the same as the caravan window.
        /// </summary>
        /// <returns>Rows, nothing picked yet</returns>
        public static List<TransferableOneWay> Pawns()
        {
            List<TransferableOneWay> rows = new List<TransferableOneWay>();
            foreach (Pawn pawn in ColonyItems.TradeablePawns()) addToRow(pawn, rows);
            return rows;
        }

        /// <summary>
        /// Puts a thing in the row it belongs to, starting a new row if it's the first of its kind.
        /// </summary>
        private static void addToRow(Thing thing, List<TransferableOneWay> rows)
        {
            // The game's own "do these two count as the same?" check, with the rules the caravan window uses
            TransferableOneWay row = TransferableUtility.TransferableMatching(thing, rows, TransferAsOneMode.PodsOrCaravanPacking);
            if (row == null)
            {
                row = new TransferableOneWay();
                rows.Add(row);
            }
            row.things.Add(thing);
        }

        /// <summary>
        /// How many separate items picked rows become when sent: things that stack, like steel, go as one item per row
        /// however many stacks they're taken from; things that don't, like dusters, go one each.
        /// </summary>
        /// <param name="rows">Rows to count</param>
        /// <returns>Number of items</returns>
        public static int ItemCount(IEnumerable<TransferableOneWay> rows)
        {
            return rows.Where(row => row.CountToTransfer > 0)
                .Sum(row => stacks(row) ? 1 : row.CountToTransfer);
        }

        /// <summary>
        /// What the picked rows are worth to a trader, in silver.
        /// </summary>
        /// <param name="rows">Rows to add up</param>
        /// <returns>Total market value</returns>
        public static float PickedValue(IEnumerable<TransferableOneWay> rows)
        {
            return rows.Where(row => row.CountToTransfer > 0 && row.HasAnyThing)
                .Sum(row => row.AnyThing.MarketValue * row.CountToTransfer);
        }

        /// <summary>
        /// Takes the picked amount of each row off the map and holds it in <see cref="ItemEscrow"/> under one new token.
        /// Takes nothing at all if any row no longer has enough (a colonist may have eaten or moved some since the
        /// window opened).
        /// </summary>
        /// <param name="rows">Item rows (no pawns), each with how many to take</param>
        /// <param name="module">Which module the items are going to (see <see cref="ItemEscrow.TryTake"/>)</param>
        /// <param name="token">Token the items are held under, to send to the server</param>
        /// <param name="itemsPerRow">The taken items, converted for sending, in the same order as <paramref name="rows"/></param>
        /// <returns>Whether the items were taken</returns>
        public static bool TryTakeItems(IList<TransferableOneWay> rows, string module, out string token, out List<List<ProtoThing>> itemsPerRow)
        {
            token = null;
            itemsPerRow = null;

            // Check every row first, so we never take half and then find the rest are gone
            if (!rows.All(row => row.things.Where(stillHere).Sum(thing => thing.stackCount) >= row.CountToTransfer)) return false;

            List<Thing> pieces = new List<Thing>();
            List<Map> maps = new List<Map>();
            List<IntVec3> positions = new List<IntVec3>();
            List<List<Thing>> piecesPerRow = new List<List<Thing>>();
            foreach (TransferableOneWay row in rows)
            {
                List<Thing> rowPieces = new List<Thing>();
                int stillNeeded = row.CountToTransfer;

                // ToList() takes a snapshot, since SplitOff below changes the map while we loop
                foreach (Thing thing in row.things.Where(stillHere).ToList())
                {
                    if (stillNeeded <= 0) break;

                    // Remember where it was before it leaves the map. A despawned thing forgets its map, and a piece
                    // split off a stack never had a position of its own
                    positions.Add(thing.Position);
                    maps.Add(thing.Map);

                    // SplitOff(n) takes n off the stack as their own thing, or hands back the whole thing (already off
                    // the map) when n is the whole stack
                    Thing piece = thing.SplitOff(Math.Min(stillNeeded, thing.stackCount));
                    stillNeeded -= piece.stackCount;
                    pieces.Add(piece);
                    rowPieces.Add(piece);
                }
                piecesPerRow.Add(rowPieces);
            }

            token = ItemEscrow.Hold(pieces, maps, module, positions);
            itemsPerRow = new List<List<ProtoThing>>();
            for (int i = 0; i < rows.Count; i++)
            {
                List<ProtoThing> items = piecesPerRow[i].Select(TradingThingConverter.ConvertThingForSending).ToList();
                itemsPerRow.Add(stacks(rows[i]) ? join(items) : items);
            }
            return true;
        }

        /// <summary>
        /// Whether a listed thing can still be taken: on the map, not used up, and not biocoded since.
        /// </summary>
        private static bool stillHere(Thing thing)
        {
            return thing.Spawned && !thing.Destroyed && ColonyItems.CanLeaveOnItsOwn(thing);
        }

        /// <summary>
        /// Whether a row's things stack (steel does, dusters don't).
        /// </summary>
        private static bool stacks(TransferableOneWay row)
        {
            return row.HasAnyThing && row.ThingDef.stackLimit > 1;
        }

        /// <summary>
        /// Joins pieces of the same stackable item into one, keeping the most worn hit points so nobody gets less than
        /// they expected. The receiving game splits it into normal-sized stacks again when it arrives.
        /// </summary>
        private static List<ProtoThing> join(List<ProtoThing> pieces)
        {
            if (pieces.Count < 2) return pieces;

            ProtoThing joined = pieces[0].Clone();
            joined.StackCount = pieces.Sum(piece => piece.StackCount);
            joined.HitPoints = pieces.Min(piece => piece.HitPoints);
            return new List<ProtoThing> { joined };
        }
    }
}
