using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PhinixClient
{
    public class TradeReservedItems : GameComponent
    {
        /// <summary>
        /// Items that have been put up for offer and removed from the game, organised by trade ID.
        /// </summary>
        public Dictionary<string, ExposableList<Thing>> Things => things;
        /// <inheritdoc cref="Things"/>
        private Dictionary<string, ExposableList<Thing>> things = new Dictionary<string, ExposableList<Thing>>();

        /// <inheritdoc cref="Dictionary{TKey,TValue}.this"/>
        public List<Thing> this[string index] => things[index];
        /// <inheritdoc cref="Dictionary{TKey,TValue}.ContainsKey(TKey)"/>
        public bool ContainsKey(string tradeId) => things.ContainsKey(tradeId);
        /// <inheritdoc cref="Dictionary{TKey,TValue}.Remove(TKey)"/>
        public bool Remove(string tradeId) => things.Remove(tradeId);

        /// <summary>
        /// Determines whether there are any items reserved.
        /// </summary>
        public bool Any() => things.Values.Any(list => list.Any(thing => thing.stackCount > 0));

        // Required for RimWorld startup
        public TradeReservedItems(Game game = null) {}

        /// <inheritdoc cref="IExposable.ExposeData"/>
        public override void ExposeData()
        {
            // Don't save anything if there aren't any items reserved
            if (Scribe.mode == LoadSaveMode.Saving && !Any())
            {
                return;
            }

            List<string> dictKeys = Things.Keys.ToList();
            List<ExposableList<Thing>> dictValues = Things.Values.ToList();
            Scribe_Collections.Look(ref things, "PhinixReservedItems", LookMode.Value, LookMode.Deep, ref dictKeys, ref dictValues);
        }

        /// <summary>
        /// <inheritdoc cref="Add(string,IEnumerable{Thing})"/>
        /// </summary>
        /// <param name="tradeId">ID of the trade the items belong to</param>
        /// <param name="stackedThings">Stacked collection of <see cref="Thing"/>s to add</param>
        public void Add(string tradeId, StackedThings stackedThings)
        {
            Add(tradeId, stackedThings.Things);
        }

        /// <summary>
        /// Adds an item to the reserved items dictionary.
        /// </summary>
        /// <param name="tradeId">ID of the trade the item belongs to</param>
        /// <param name="thing"><see cref="Thing"/> to add</param>
        public void Add(string tradeId, Thing thing)
        {
            // Add to an existing list or create a new one
            if (things.ContainsKey(tradeId))
            {
                things[tradeId].Add(thing);
            }
            else
            {
                ExposableList<Thing> list = new ExposableList<Thing>(){thing};
                things.Add(tradeId, list);
            }
        }

        /// <summary>
        /// Adds a collection of items to the reserved items dictionary.
        /// </summary>
        /// <param name="tradeId">ID of the trade the items belong to</param>
        /// <param name="things">Collection of <see cref="Thing"/>s to add</param>
        public void Add(string tradeId, IEnumerable<Thing> things)
        {
            // Add to an existing list or create a new one
            if (this.things.ContainsKey(tradeId))
            {
                this.things[tradeId].AddRange(things);
            }
            else
            {
                ExposableList<Thing> list = new ExposableList<Thing>(things);
                this.things.Add(tradeId, list);
            }
        }
    }
}