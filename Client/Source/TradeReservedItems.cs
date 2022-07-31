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
        public Dictionary<string, ExposableList<Thing>> ReservedItems => reservedItems;
        /// <inheritdoc cref="ReservedItems"/>
        private Dictionary<string, ExposableList<Thing>> reservedItems = new Dictionary<string, ExposableList<Thing>>();

        /// <inheritdoc cref="Dictionary{TKey,TValue}.this"/>
        public List<Thing> this[string index] => reservedItems[index];
        /// <inheritdoc cref="Dictionary{TKey,TValue}.ContainsKey(TKey)"/>
        public bool ContainsKey(string tradeId) => reservedItems.ContainsKey(tradeId);

        /// <inheritdoc cref="IExposable.ExposeData"/>
        public override void ExposeData()
        {
            // Don't save anything if there aren't any items reserved
            if (Scribe.mode == LoadSaveMode.Saving && !reservedItems.Any())
            {
                return;
            }

            List<string> dictKeys = ReservedItems.Keys.ToList();
            List<ExposableList<Thing>> dictValues = ReservedItems.Values.ToList();
            Scribe_Collections.Look(ref reservedItems, "PhinixReservedItems", LookMode.Value, LookMode.Deep, ref dictKeys, ref dictValues);
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
        /// <inheritdoc cref="Add(string,IEnumerable{Thing})"/>
        /// </summary>
        /// <param name="tradeId">ID of the trade the items belong to</param>
        /// <param name="thing"><see cref="Thing"/> to add</param>
        public void Add(string tradeId, Thing thing)
        {
            Add(tradeId, new Thing[]{thing});
        }

        /// <summary>
        /// Adds a collection of items to the reserved items dictionary.
        /// </summary>
        /// <param name="tradeId">ID of the trade the items belong to</param>
        /// <param name="things">Collection of <see cref="Thing"/>s to add</param>
        public void Add(string tradeId, IEnumerable<Thing> things)
        {
            ExposableList<Thing> list = new ExposableList<Thing>("Things", LookMode.Deep, things);
            reservedItems.Add(tradeId, list);
        }
    }
}