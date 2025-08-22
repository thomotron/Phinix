using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class UserList
    {
        private const float SCROLLBAR_WIDTH = 16f;

        private readonly float BLOCKED_SPACER_HEIGHT = Text.CalcHeight("", 0) + BLOCKED_SPACER_PADDING_TOP + BLOCKED_SPACER_PADDING_BOTTOM;
        private const float BLOCKED_SPACER_PADDING_TOP = 7f;
        private const float BLOCKED_SPACER_PADDING_BOTTOM = 3f;

        private readonly float USER_BUTTON_HEIGHT = Text.CalcHeight("", 0) + USER_BUTTON_PADDING_VERTICAL * 2;
        private const float USER_BUTTON_PADDING_HORIZONTAL = 3f;
        private const float USER_BUTTON_PADDING_VERTICAL = 5f;

        private readonly Texture2D blockedSpacerCollapseIcon = ContentFinder<Texture2D>.Get("collapse");

        /// <summary>
        /// Background colour for blocked users.
        /// </summary>
        private readonly Color blockedBackgroundColour = new Color(0f, 0f, 0f, 0.35f);
        /// <summary>
        /// Text colour for blocked users.
        /// </summary>
        private readonly Color blockedNameColour = new Color(0.6f, 0.6f, 0.6f);

        /// <summary>
        /// List of currently-online users.
        /// </summary>
        private readonly List<ImmutableUser> onlineUsers = new List<ImmutableUser>();
        /// <summary>
        /// List of blocked users.
        /// </summary>
        private readonly List<ImmutableUser> blockedUsers = new List<ImmutableUser>();
        /// <summary>
        /// Subset of <see cref="onlineUsers"/> filtered by <see cref="searchText"/> who are also not in
        /// <see cref="blockedUsers"/>.
        /// </summary>
        /// <remarks>
        /// This list is primarily used by the UI thread. It is populated from <see cref="onlineUsers"/> by the UI
        /// thread when <see cref="Draw"/> is called and <see cref="onlineUsersChanged"/> is true. It should not be
        /// updated directly by any other thread.
        /// </remarks>
        private readonly List<ImmutableUser> filteredOnlineUsers = new List<ImmutableUser>();
        /// <summary>
        /// Subset of <see cref="blockedUsers"/> filtered by <see cref="searchText"/>.
        /// </summary>
        /// <remarks>
        /// This list is primarily used by the UI thread. It is populated from <see cref="blockedUsers"/> by the UI
        /// thread when <see cref="Draw"/> is called and <see cref="blockedUsersChanged"/> is true. It should not be
        /// updated directly by any other thread.
        /// </remarks>
        private readonly List<ImmutableUser> filteredBlockedUsers = new List<ImmutableUser>();
        /// <summary>
        /// Whether <see cref="filteredOnlineUsers"/> should be repopulated from <see cref="onlineUsers"/>.
        /// </summary>
        private bool onlineUsersChanged = false;
        /// <summary>
        /// Whether <see cref="filteredBlockedUsers"/> should be repopulated from <see cref="blockedUsers"/>.
        /// </summary>
        private bool blockedUsersChanged = false;
        /// <summary>
        /// Lock object protecting <see cref="filteredOnlineUsers"/> and <see cref="filteredBlockedUsers"/>.
        /// </summary>
        private readonly object userListsLock = new object();

        /// <summary>
        /// Collection of pre-calculated heights for each user. Used to wrap long usernames with a cached result for
        /// better performance.
        /// </summary>
        private readonly Dictionary<ImmutableUser, (float Normal, float Scrollbar)> userRectHeights = new Dictionary<ImmutableUser, (float Normal, float Scrollbar)>();
        /// <summary>
        /// Total height of all users. Derived from <see cref="userRectHeights"/>.
        /// </summary>
        private (float Normal, float Scrollbar) userRectHeightsSum = (0f, 0f);

        /// <summary>
        /// Collection of pre-calculated heights for each blocked user. Used to wrap long usernames with a cached
        /// result for better performance.
        /// </summary>
        private readonly Dictionary<ImmutableUser, (float Normal, float Scrollbar)> blockedUserRectHeights = new Dictionary<ImmutableUser, (float Normal, float Scrollbar)>();
        /// <summary>
        /// Total height of all blocked users. Derived from <see cref="blockedUserRectHeights"/>.
        /// </summary>
        private (float Normal, float Scrollbar) blockedUserRectHeightsSum = (0f, 0f);

        /// <summary>
        /// Search text to filter <see cref="filteredOnlineUsers"/> and <see cref="filteredBlockedUsers"/> by.
        /// </summary>
        private string searchText = "";

        /// <summary>
        /// List scroll position.
        /// </summary>
        Vector2 scrollPos;

        /// <summary>
        /// Creates a new <see cref="UserList"/> instance.
        /// </summary>
        public UserList()
        {
            // Bind to events
            Client.Instance.OnUserSync += (s, e) => refreshUserLists();
            Client.Instance.OnUserCreated += (s, e) => refreshOnlineUserList();
            Client.Instance.OnUserLoggedIn += (s, e) => refreshOnlineUserList();
            Client.Instance.OnUserLoggedOut += (s, e) => refreshOnlineUserList();
            Client.Instance.OnUserDisplayNameChanged += (s, e) => refreshOnlineUserList();
            Client.Instance.OnBlockedUsersChanged += (s, e) => refreshUserLists();

            // Populate the user lists before the first draw
            refreshUserLists();
        }

        /// <summary>
        /// Draws the user list within the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public void Draw(Rect inRect)
        {
            if (onlineUsersChanged || blockedUsersChanged)
            {
                // Update the online users list if it's been changed
                if (onlineUsersChanged)
                {
                    // Try lock the unfiltered lists, otherwise wait until the next frame to refresh content
                    if (Monitor.TryEnter(userListsLock))
                    {
                        // Repopulate the list content with users matching the search text
                        filteredOnlineUsers.Clear();
                        filteredOnlineUsers.AddRange(onlineUsers.Where(u => u.DisplayName.StripTags().IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) > -1));

                        // Unset the changed flag and release the lock
                        onlineUsersChanged = false;
                        Monitor.Exit(userListsLock);
                    }
                }

                // Update the blocked users list if it's been changed
                if (blockedUsersChanged)
                {
                    // Try lock the unfiltered lists, otherwise wait until the next frame to refresh content
                    if (Monitor.TryEnter(userListsLock))
                    {
                        // Repopulate the list content with users matching the search text
                        filteredBlockedUsers.Clear();
                        filteredBlockedUsers.AddRange(blockedUsers.Where(u => u.DisplayName.StripTags().IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) > -1));

                        // Unset the changed flag and release the lock
                        blockedUsersChanged = false;
                        Monitor.Exit(userListsLock);
                    }
                }

                // Recalculate user heights
                userRectHeights.Clear();
                userRectHeightsSum = (0f, 0f);
                foreach (ImmutableUser user in filteredOnlineUsers)
                {
                    float normalHeight = Text.CalcHeight(formatDisplayName(user.DisplayName, false), inRect.width) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    float heightWithScrollbar = Text.CalcHeight(formatDisplayName(user.DisplayName, false), inRect.width - SCROLLBAR_WIDTH) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    userRectHeights.Add(user, (normalHeight, heightWithScrollbar));
                    userRectHeightsSum.Normal += normalHeight;
                    userRectHeightsSum.Scrollbar += heightWithScrollbar;
                }
                blockedUserRectHeights.Clear();
                blockedUserRectHeightsSum = (0f, 0f);
                foreach (ImmutableUser user in filteredBlockedUsers)
                {
                    float normalHeight = Text.CalcHeight(formatDisplayName(user.DisplayName, true), inRect.width) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    float heightWithScrollbar = Text.CalcHeight(formatDisplayName(user.DisplayName, true), inRect.width - SCROLLBAR_WIDTH) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    blockedUserRectHeights.Add(user, (normalHeight, heightWithScrollbar));
                    blockedUserRectHeightsSum.Normal += normalHeight;
                    blockedUserRectHeightsSum.Scrollbar += heightWithScrollbar;
                }
            }

            // Set up the scrollable container
            float totalHeight = userRectHeightsSum.Normal;
            if (filteredBlockedUsers.Any())
            {
                totalHeight += BLOCKED_SPACER_HEIGHT;
                if (!Client.Instance.Settings.CollapseBlockedUsers) totalHeight += blockedUserRectHeightsSum.Normal;
            }

            Rect contentRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: totalHeight
            );
            if (contentRect.height > inRect.height)
            {
                totalHeight = userRectHeightsSum.Scrollbar;
                if (filteredBlockedUsers.Any())
                {
                    totalHeight += BLOCKED_SPACER_HEIGHT;
                    if (!Client.Instance.Settings.CollapseBlockedUsers) totalHeight += blockedUserRectHeightsSum.Scrollbar;
                }

                contentRect.width = inRect.width - SCROLLBAR_WIDTH;
                contentRect.height = totalHeight;
            }

            // Start scrolling
            Widgets.BeginScrollView(inRect, ref scrollPos, contentRect);

            // Keep track of how far down the list we are
            float currentY = contentRect.yMin;

            // Normal users
            foreach (ImmutableUser user in filteredOnlineUsers)
            {
                float height = contentRect.height > inRect.height ? userRectHeights[user].Scrollbar : userRectHeights[user].Normal;
                drawUser(new Rect(contentRect.xMin, currentY, contentRect.width, height), user, false);
                currentY += height;
            }

            // Blocked users
            if (filteredBlockedUsers.Any())
            {
                // Draw the blocked users spacer
                Rect paddedRect = new Rect(
                    x: contentRect.xMin,
                    y: currentY + BLOCKED_SPACER_PADDING_TOP,
                    width: contentRect.width,
                    height: BLOCKED_SPACER_HEIGHT - BLOCKED_SPACER_PADDING_TOP - BLOCKED_SPACER_PADDING_BOTTOM
                );
                TextAnchor oldTextAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(paddedRect, "Phinix_chat_blockedUsers".Translate());
                Text.Anchor = oldTextAnchor;

                // Handle clicks on it
                if (Widgets.ButtonInvisible(paddedRect, false))
                {
                    Client.Instance.Settings.CollapseBlockedUsers = !Client.Instance.Settings.CollapseBlockedUsers;
                    Client.Instance.Settings.AcceptChanges();
                }

                // Draw the collapse arrow
                Rect collapseIconRect = new Rect(
                    x: paddedRect.xMin + USER_BUTTON_PADDING_HORIZONTAL,
                    y: paddedRect.yMin - 1f,
                    width: paddedRect.height,
                    height: paddedRect.height
                );
                Widgets.DrawTextureFitted(
                    outerRect: collapseIconRect,
                    tex: blockedSpacerCollapseIcon,
                    scale: 0.4f,
                    texProportions: new Vector2(blockedSpacerCollapseIcon.width, blockedSpacerCollapseIcon.height),
                    texCoords: new Rect(0f, 0f, 1f, 1f),
                    angle: Client.Instance.Settings.CollapseBlockedUsers ? 0 : 90
                );

                currentY += BLOCKED_SPACER_HEIGHT;

                // ...then the blocked users
                if (!Client.Instance.Settings.CollapseBlockedUsers)
                {
                    foreach (ImmutableUser user in filteredBlockedUsers)
                    {
                        float height = contentRect.height > inRect.height ? blockedUserRectHeights[user].Scrollbar : blockedUserRectHeights[user].Normal;
                        drawUser(new Rect(contentRect.xMin, currentY, contentRect.width, height), user, true);
                        currentY += height;
                    }
                }
            }

            // Stop scrolling
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Updates the user list.
        /// </summary>
        public void Filter(string searchText)
        {
            // Apply search text and flag filtered lists to be updated next frame
            this.searchText = searchText;
            onlineUsersChanged = true;
            blockedUsersChanged = true;
        }

        /// <summary>
        /// Repopulates both <see cref="onlineUsers"/> and <see cref="blockedUsers"/>.
        /// </summary>
        private void refreshUserLists()
        {
            refreshBlockedUserList();
            refreshOnlineUserList();
        }

        /// <summary>
        /// Repopulates <see cref="onlineUsers"/> with each online user.
        /// </summary>
        private void refreshOnlineUserList()
        {
            lock (userListsLock)
            {
                // Repopulate the online list
                onlineUsers.Clear();
                onlineUsers.AddRange(Client.Instance.GetUsers(true).Where(u => !blockedUsers.Contains(u)));
            }

            // Flag the filtered list to be repopulated
            onlineUsersChanged = true;
        }

        /// <summary>
        /// Repopulates <see cref="blockedUsers"/> with each blocked user.
        /// </summary>
        private void refreshBlockedUserList()
        {
            lock (userListsLock)
            {
                // Repopulate the online list
                blockedUsers.Clear();
                foreach (string uuid in Client.Instance.Settings.BlockedUsers)
                {
                    if (Client.Instance.TryGetUser(uuid, out ImmutableUser user))
                    {
                        blockedUsers.Add(user);
                    }
                }
            }

            // Flag the filtered list to be repopulated
            blockedUsersChanged = true;
        }

        /// <summary>
        /// Draws a user in the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        /// <param name="user">User to draw</param>
        /// <param name="blocked">Whether the user is blocked</param>
        private void drawUser(Rect inRect, ImmutableUser user, bool blocked = false)
        {
            string formattedDisplayName = formatDisplayName(user.DisplayName, blocked);

            if (blocked)
            {
                // Draw a highlighted background
                Widgets.DrawRectFast(inRect, blockedBackgroundColour);
            }

            // Get a padded area to draw the text in
            Rect paddedRect = inRect.ContractedBy(USER_BUTTON_PADDING_HORIZONTAL, USER_BUTTON_PADDING_VERTICAL);

            // Draw the text
            Widgets.Label(paddedRect, Mouse.IsOver(inRect) ? formattedDisplayName.Colorize(Widgets.MouseoverOptionColor) : formattedDisplayName);

            // Draw the button and optionally the context menu if clicked
            if (Widgets.ButtonInvisible(inRect, false))
            {
                drawContextMenu(user);
            }
        }

        /// <summary>
        /// Draws a context menu with user-specific actions.
        /// </summary>
        private void drawContextMenu(ImmutableUser user)
        {
            // Do nothing if this is our UUID
            if (user.Uuid == Client.Instance.Uuid) return;

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(
                new FloatMenuOption(
                    label: "Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(user.DisplayName)),
                    action: () => Client.Instance.CreateTrade(user.Uuid)
                )
            );
            items.Add(
                new FloatMenuOption(
                    label: (Client.Instance.Settings.BlockedUsers.Contains(user.Uuid) ? "Phinix_chat_contextMenu_unblockUser" : "Phinix_chat_contextMenu_blockUser").Translate(),
                    action: () =>
                    {
                        if (Client.Instance.Settings.BlockedUsers.Contains(user.Uuid)) Client.Instance.UnBlockUser(user.Uuid);
                        else Client.Instance.BlockUser(user.Uuid);
                    }
                )
            );

            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// Formats <paramref name="displayName"/> according to the user's settings.
        /// </summary>
        /// <param name="displayName">Name to be formatted</param>
        /// <param name="blocked">Whether the user is blocked</param>
        /// <returns>Formatted name</returns>
        private string formatDisplayName(string displayName, bool blocked)
        {
            if (blocked)
            {
                // Strip display name formatting and grey it out
                return TextHelper.StripRichText(displayName).Colorize(blockedNameColour);
            }
            else
            {
                return Client.Instance.Settings.ShowNameFormatting ? displayName : TextHelper.StripRichText(displayName);
            }
        }
    }
}