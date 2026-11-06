using System;
using System.Collections.Generic;

namespace Utils
{
    /// <summary>
    /// Limits how often something can happen per key (e.g. per user UUID) using a sliding time window.
    /// For example, a limit of 5 actions per 10 seconds lets a user act 5 times, then refuses them until
    /// their oldest action is more than 10 seconds old.
    /// </summary>
    /// <remarks>
    /// Safe to call from multiple threads at once, since packet handlers may run concurrently.
    /// </remarks>
    public class RateLimiter
    {
        /// <summary>
        /// Maximum number of actions allowed per key inside the window.
        /// </summary>
        private readonly int maxActions;

        /// <summary>
        /// Length of the sliding window.
        /// </summary>
        private readonly TimeSpan window;

        /// <summary>
        /// Times of each key's recent actions, oldest first.
        /// </summary>
        private readonly Dictionary<string, Queue<DateTime>> recentActions = new Dictionary<string, Queue<DateTime>>();

        /// <summary>
        /// Lock object to prevent race conditions when accessing <see cref="recentActions"/>.
        /// </summary>
        private readonly object recentActionsLock = new object();

        /// <summary>
        /// Initialises a new <see cref="RateLimiter"/> instance.
        /// </summary>
        /// <param name="maxActions">Maximum number of actions allowed per key inside the window</param>
        /// <param name="window">Length of the sliding window</param>
        public RateLimiter(int maxActions, TimeSpan window)
        {
            if (maxActions < 1) throw new ArgumentOutOfRangeException(nameof(maxActions), "Must allow at least one action");
            if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Must be longer than zero");

            this.maxActions = maxActions;
            this.window = window;
        }

        /// <summary>
        /// Records an action for the given key if it is under its limit.
        /// </summary>
        /// <param name="key">Who or what is acting, e.g. a user's UUID</param>
        /// <returns>Whether the action is allowed</returns>
        public bool TryAct(string key)
        {
            DateTime now = DateTime.UtcNow;

            lock (recentActionsLock)
            {
                // Get this key's recent actions, creating an empty queue the first time we see it
                if (!recentActions.TryGetValue(key, out Queue<DateTime> actions))
                {
                    actions = new Queue<DateTime>();
                    recentActions.Add(key, actions);
                }

                // Forget actions that have slid out of the window
                while (actions.Count > 0 && now - actions.Peek() >= window)
                {
                    actions.Dequeue();
                }

                // Refuse if the key has used up its allowance (refused actions don't count towards it)
                if (actions.Count >= maxActions) return false;

                // Otherwise record this action and allow it
                actions.Enqueue(now);
                return true;
            }
        }
    }
}
