using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class UserList : Displayable
    {
        private const float SCROLLBAR_WIDTH = 16f;
        private const float BLOCKED_SPACER_MARGIN_TOP = 10f;
        private const float BLOCKED_SPACER_MARGIN_BOTTOM = 3f;

        /// <summary>
        /// Search text callback used to filter the list.
        /// </summary>
        private Func<string> searchTextCallback;

        /// <summary>
        /// Container that holds a collection of <see cref="UserWidget"/> from <see cref="filteredUnblockedUserWidgets"/>
        /// </summary>
        private VerticalFlexContainer userListFlexContainer = new VerticalFlexContainer(0);

        /// <summary>
        /// List containing each online user.
        /// </summary>
        private List<UserWidget> userWidgets = new List<UserWidget>();
        /// <summary>
        /// Lock object to prevent multi-threaded access problems with <see cref="userWidgets"/>.
        /// </summary>
        private object userWidgetsLock = new object();

        /// <summary>
        /// A subset of <see cref="userWidgets"/> filtered by <see cref="searchTextCallback"/> that are not blocked.
        /// </summary>
        private List<UserWidget> filteredUnblockedUserWidgets = new List<UserWidget>();
        /// <summary>
        /// A subset of <see cref="userWidgets"/> filtered by <see cref="searchTextCallback"/> that are blocked.
        /// </summary>
        private List<UserWidget> filteredBlockedUserWidgets = new List<UserWidget>();
        /// <summary>
        /// Lock object to prevent multi-threaded access problems with <see cref="filteredUnblockedUserWidgets"/>.
        /// </summary>
        private object filteredUserWidgetsLock = new object();

        /// <summary>
        /// Whether <see cref="userListFlexContainer"/> needs to be refreshed to accommodate changes to
        /// <see cref="filteredUnblockedUserWidgets"/>.
        /// </summary>
        private bool filterChanged;

        /// <summary>
        /// List scroll position.
        /// </summary>
        Vector2 scrollPos;

        /// <summary>
        /// Creates a new <see cref="UserList"/> instance with the given search text callback.
        /// </summary>
        /// <param name="searchTextCallback">Search text callback</param>
        public UserList(Func<string> searchTextCallback)
        {
            // Refuse null callbacks
            if (searchTextCallback == null)
            {
                throw new ArgumentNullException(nameof(searchTextCallback), "Search text callback cannot be null.");
            }

            this.searchTextCallback = searchTextCallback;

            // Refresh and filter the user list to populate before the first draw
            refreshUserWidgetsList();

            // Bind to events
            // TODO: Handle user events individually rather than completely replacing the list
            //       This means settling on an ordering standard for users in the sidebar.
            Client.Instance.OnUserSync += (s, e) => refreshUserWidgetsList();
            Client.Instance.OnUserCreated += (s, e) => refreshUserWidgetsList();
            Client.Instance.OnUserLoggedIn += (s, e) => refreshUserWidgetsList();
            Client.Instance.OnUserLoggedOut += (s, e) => refreshUserWidgetsList();
            Client.Instance.OnUserDisplayNameChanged += (s, e) => refreshUserWidgetsList();
            Client.Instance.OnBlockedUsersChanged += (s, e) => refreshUserWidgetsList();
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Check if the filtered list has changed
            if (filterChanged)
            {
                // Try lock the filtered widget list, otherwise wait until the next cycle to refresh content
                if (Monitor.TryEnter(filteredUserWidgetsLock))
                {
                    // Replace the list content with the new list of filtered widgets
                    userListFlexContainer.Contents.Clear();
                    userListFlexContainer.Contents.AddRange(filteredUnblockedUserWidgets);
                    if (filteredBlockedUserWidgets.Any())
                    {
                        userListFlexContainer.Contents.Add(new SpacerWidget(height: BLOCKED_SPACER_MARGIN_TOP));
                        userListFlexContainer.Contents.Add(new TextWidget("Phinix_chat_blockedUsers".Translate(), anchor: TextAnchor.LowerCenter));
                        userListFlexContainer.Contents.Add(new SpacerWidget(height: BLOCKED_SPACER_MARGIN_BOTTOM));
                        userListFlexContainer.Contents.AddRange(filteredBlockedUserWidgets);
                    }

                    // Unset the filter changed flag
                    filterChanged = false;

                    Monitor.Exit(filteredUserWidgetsLock);
                }
            }

            // Set up the scrollable container
            Rect innerContainer = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width - SCROLLBAR_WIDTH,
                height: userListFlexContainer.CalcHeight(inRect.width - SCROLLBAR_WIDTH)
            );

            // Start scrolling
            Widgets.BeginScrollView(inRect, ref scrollPos, innerContainer);

            // Draw the flex container
            userListFlexContainer.Draw(innerContainer);

            // Stop scrolling
            Widgets.EndScrollView();
        }

        /// <inheritdoc />
        public override void Update()
        {
            // Re-filter the user widgets
            refreshFilteredUserWidgetsList();
        }

        /// <summary>
        /// Generates <see cref="UserWidget"/> widgets for each online user and
        /// places them in <see cref="userWidgets"/>.
        /// </summary>
        /// <remarks>
        /// This method calls <see cref="refreshFilteredUserWidgetsList"/> to rebuild
        /// the filtered widgets list based on the new content.
        /// </remarks>
        private void refreshUserWidgetsList()
        {
            lock (userWidgetsLock)
            {
                userWidgets.Clear();

                // Get each online and blocked user's UUID
                foreach (string uuid in Client.Instance.GetUserUuids(true).Union(Client.Instance.BlockedUsers).Distinct())
                {
                    // Try get the user's display name
                    if (!Client.Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";

                    // Try get the user's blocked state
                    bool isBlocked = Client.Instance.BlockedUsers.Contains(uuid);

                    // Create a new user widget and add it to the list
                    userWidgets.Add(new UserWidget(uuid, displayName, isBlocked));
                }
            }

            // Refresh the filtered widgets since the list just got purged
            refreshFilteredUserWidgetsList();
        }

        /// <summary>
        /// Refreshes <see cref="filteredUnblockedUserWidgets"/> and <see cref="filteredBlockedUserWidgets"/> with
        /// <see cref="UserWidget"/> that have display names containing the search text.
        /// </summary>
        private void refreshFilteredUserWidgetsList()
        {
            // Get the search text
            string searchText = searchTextCallback.Invoke();

            lock (filteredUserWidgetsLock)
            {
                filteredUnblockedUserWidgets.Clear();
                filteredBlockedUserWidgets.Clear();

                lock (userWidgetsLock)
                {
                    // Repopulate the lists with user widgets that have a display name containing cachedSearchText
                    filteredUnblockedUserWidgets.AddRange(userWidgets.Where(w => w.DisplayName.ToLower().Contains(searchText.ToLower()) && !w.Blocked));
                    filteredBlockedUserWidgets.AddRange(userWidgets.Where(w => w.DisplayName.ToLower().Contains(searchText.ToLower()) && w.Blocked));
                }
            }

            // Set the filter changed flag to tell the GUI thread that it needs to rebuild the list
            filterChanged = true;
        }
    }
}