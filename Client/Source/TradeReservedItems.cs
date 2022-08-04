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
        public Dictionary<string, ExposableList<Thing>> Items => items;
        /// <inheritdoc cref="Items"/>
        private Dictionary<string, ExposableList<Thing>> items = new Dictionary<string, ExposableList<Thing>>();

        /// <inheritdoc cref="Dictionary{TKey,TValue}.this"/>
        public List<Thing> this[string index] => items[index];
        /// <inheritdoc cref="Dictionary{TKey,TValue}.ContainsKey(TKey)"/>
        public bool ContainsKey(string tradeId) => items.ContainsKey(tradeId);
        /// <inheritdoc cref="Dictionary{TKey,TValue}.Remove(TKey)"/>
        public bool Remove(string tradeId) => items.Remove(tradeId);

        /// <summary>
        /// Determines whether there are any items reserved.
        /// </summary>
        public bool Any() => items.Values.Any(list => list.Any(thing => thing.stackCount > 0));

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

            List<string> dictKeys = Items.Keys.ToList();
            List<ExposableList<Thing>> dictValues = Items.Values.ToList();
            Scribe_Collections.Look(ref items, "PhinixReservedItems", LookMode.Value, LookMode.Deep, ref dictKeys, ref dictValues);
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
            if (items.ContainsKey(tradeId))
            {
                items[tradeId].Add(thing);
            }
            else
            {
                ExposableList<Thing> list = new ExposableList<Thing>(){thing};
                items.Add(tradeId, list);
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
            if (items.ContainsKey(tradeId))
            {
                items[tradeId].AddRange(things);
            }
            else
            {
                ExposableList<Thing> list = new ExposableList<Thing>(things);
                items.Add(tradeId, list);
            }
        }
    }
}