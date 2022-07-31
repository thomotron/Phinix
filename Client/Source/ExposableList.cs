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
        private readonly string label;
        private readonly LookMode lookMode;

        /// <inheritdoc cref="List{T}()"/>
        /// <param name="label">XML tag used in save format</param>
        /// <param name="lookMode">Type of de/serialisation to use for the list when saving or loading</param>
        /// <seealso cref="List{T}()"/>
        public ExposableList(string label, LookMode lookMode)
        {
            this.label = label;
            this.lookMode = lookMode;
        }

        /// <inheritdoc cref="List{T}(IEnumerable{T})"/>
        /// <param name="label">XML tag used in save format</param>
        /// <param name="lookMode">Type of de/serialisation to use for the list when saving or loading</param>
        /// <seealso cref="List{T}(IEnumerable{T})"/>
        public ExposableList(string label, LookMode lookMode, IEnumerable<T> collection) : base(collection)
        {
            this.label = label;
            this.lookMode = lookMode;
        }

        /// <inheritdoc cref="List{T}(int)"/>
        /// <param name="label">XML tag used in save format</param>
        /// <param name="lookMode">Type of de/serialisation to use for the list when saving or loading</param>
        /// <seealso cref="List{T}(int)"/>
        public ExposableList(string label, LookMode lookMode, int capacity) : base(capacity)
        {
            this.label = label;
            this.lookMode = lookMode;
        }

        /// <inheritdoc cref="IExposable.ExposeData"/>
        public void ExposeData()
        {
            List<T> items = this;
            Scribe_Collections.Look(ref items, label, lookMode);
            
            // Purge the inner list and repopulate from the one we've been provided
            if (items != this)
            {
                Clear();
                AddRange(items);
            }
        }
    }
}