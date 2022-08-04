using System.Collections.Generic;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Wrapper around <see cref="List{T}"/> which implements <see cref="IExposable"/>.
    /// </summary>
    /// <typeparam name="T">List type</typeparam>
    /// <seealso cref="List{T}"/>
    public class ExposableList<T> : List<T>, IExposable
    {
        /// <inheritdoc cref="List{T}()"/>
        public ExposableList()
        {
        }

        /// <inheritdoc cref="List{T}(int)"/>
        public ExposableList(int capacity) : base(capacity)
        {
        }

        /// <inheritdoc cref="List{T}(IEnumerable{T})"/>
        public ExposableList(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <inheritdoc cref="IExposable.ExposeData"/>
        public void ExposeData()
        {
            List<T> items = this;
            Scribe_Collections.Look(ref items, "ol", LookMode.Deep);

            // Purge the inner list and repopulate from the one we've been provided
            if (items != this)
            {
                Clear();
                if (items != null) AddRange(items);
            }
        }
    }
}