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

        /// <summary>
        /// Space between the sections of the list (friends, online, offline and blocked).
        /// </summary>
        private const float SECTION_GAP = 8f;

        /// <summary>
        /// Font size of the "last seen" line under offline friends, a little smaller than names.
        /// </summary>
        private const int LAST_SEEN_FONT_SIZE = 11;

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
        /// Text colour for offline friends, and for the name a friend had when they were friended.
        /// </summary>
        private readonly Color friendDetailColour = new Color(0.6f, 0.6f, 0.6f);
        /// <summary>
        /// Background colour for friends, so they stand out at the top of the list.
        /// </summary>
        private readonly Color friendBackgroundColour = new Color(0.3f, 0.6f, 0.3f, 0.12f);

        /// <summary>
        /// List of friends, online ones first.
        /// </summary>
        private readonly List<ImmutableUser> friendUsers = new List<ImmutableUser>();
        /// <summary>
        /// Each friend's UUID and the display name they had when they were friended. Replaced along with
        /// <see cref="friendUsers"/>.
        /// </summary>
        private Dictionary<string, string> friendNames = new Dictionary<string, string>();
        /// <summary>
        /// Subset of <see cref="friendUsers"/> filtered by <see cref="searchText"/>. Only used by the UI thread, like
        /// <see cref="filteredOnlineUsers"/>.
        /// </summary>
        private readonly List<ImmutableUser> filteredFriendUsers = new List<ImmutableUser>();
        /// <summary>
        /// Whether <see cref="filteredFriendUsers"/> should be repopulated from <see cref="friendUsers"/>.
        /// </summary>
        private bool friendUsersChanged = false;
        /// <summary>
        /// Collection of pre-calculated heights for each friend, like <see cref="userRectHeights"/>.
        /// </summary>
        private readonly Dictionary<ImmutableUser, (float Normal, float Scrollbar)> friendUserRectHeights = new Dictionary<ImmutableUser, (float Normal, float Scrollbar)>();
        /// <summary>
        /// Total height of all friends. Derived from <see cref="friendUserRectHeights"/>.
        /// </summary>
        private (float Normal, float Scrollbar) friendUserRectHeightsSum = (0f, 0f);

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
            // Friends can be online or offline and are sorted by it, so every change refreshes all of the lists
            Client.Instance.OnUserSync += (s, e) => refreshUserLists();
            Client.Instance.OnUserCreated += (s, e) => refreshUserLists();
            Client.Instance.OnUserLoggedIn += (s, e) => refreshUserLists();
            Client.Instance.OnUserLoggedOut += (s, e) => refreshUserLists();
            Client.Instance.OnUserDisplayNameChanged += (s, e) => refreshUserLists();
            Client.Instance.OnBlockedUsersChanged += (s, e) => refreshUserLists();
            Client.Instance.OnFriendsChanged += (s, e) => refreshUserLists();

            // Populate the user lists before the first draw
            refreshUserLists();
        }

        /// <summary>
        /// Draws the user list within the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public void Draw(Rect inRect)
        {
            if (onlineUsersChanged || blockedUsersChanged || friendUsersChanged)
            {
                // Update the friends list if it's been changed
                if (friendUsersChanged)
                {
                    // Try lock the unfiltered lists, otherwise wait until the next frame to refresh content
                    if (Monitor.TryEnter(userListsLock))
                    {
                        // Match the search against their current name or the name they were friended as
                        filteredFriendUsers.Clear();
                        filteredFriendUsers.AddRange(friendUsers.Where(u => matchesSearch(u.DisplayName.StripTags()) ||
                                                                            (friendNames.TryGetValue(u.Uuid, out string friendedAs) && matchesSearch(friendedAs))));

                        // Unset the changed flag and release the lock
                        friendUsersChanged = false;
                        Monitor.Exit(userListsLock);
                    }
                }

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
                friendUserRectHeights.Clear();
                friendUserRectHeightsSum = (0f, 0f);
                foreach (ImmutableUser user in filteredFriendUsers)
                {
                    float normalHeight = Text.CalcHeight(formatFriend(user), inRect.width) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    float heightWithScrollbar = Text.CalcHeight(formatFriend(user), inRect.width - SCROLLBAR_WIDTH) + (USER_BUTTON_PADDING_VERTICAL * 2);
                    friendUserRectHeights.Add(user, (normalHeight, heightWithScrollbar));
                    friendUserRectHeightsSum.Normal += normalHeight;
                    friendUserRectHeightsSum.Scrollbar += heightWithScrollbar;
                }
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

            // The list is in sections, each under a small heading with a gap above it: online friends, everyone else
            // who's on, then friends who are offline (with when they were last seen)
            List<ImmutableUser> onlineFriends = filteredFriendUsers.Where(u => u.LoggedIn).ToList();
            List<ImmutableUser> offlineFriends = filteredFriendUsers.Where(u => !u.LoggedIn).ToList();
            List<(string Heading, List<ImmutableUser> Users, Dictionary<ImmutableUser, (float Normal, float Scrollbar)> Heights, bool Friend)> sections =
                new List<(string, List<ImmutableUser>, Dictionary<ImmutableUser, (float Normal, float Scrollbar)>, bool)>();
            if (onlineFriends.Any()) sections.Add(("Phinix_chat_friends".Translate(), onlineFriends, friendUserRectHeights, true));
            // Everyone else only needs a heading to set them apart from friends; on their own the "Online" above the
            // list already says what they are
            if (filteredOnlineUsers.Any()) sections.Add((filteredFriendUsers.Any() ? "Phinix_chat_onlineSection".Translate().Resolve() : null, filteredOnlineUsers, userRectHeights, false));
            if (offlineFriends.Any()) sections.Add(("Phinix_chat_offlineSection".Translate(), offlineFriends, friendUserRectHeights, true));

            // Set up the scrollable container
            float sectionsHeight(bool scrollbar)
            {
                float height = 0f;
                for (int i = 0; i < sections.Count; i++)
                {
                    if (i > 0) height += SECTION_GAP;
                    if (sections[i].Heading != null) height += BLOCKED_SPACER_HEIGHT;
                    height += sections[i].Users.Sum(u => scrollbar ? sections[i].Heights[u].Scrollbar : sections[i].Heights[u].Normal);
                }
                if (filteredBlockedUsers.Any())
                {
                    height += SECTION_GAP + BLOCKED_SPACER_HEIGHT;
                    if (!Client.Instance.Settings.CollapseBlockedUsers) height += scrollbar ? blockedUserRectHeightsSum.Scrollbar : blockedUserRectHeightsSum.Normal;
                }
                return height;
            }

            Rect contentRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: sectionsHeight(false)
            );
            if (contentRect.height > inRect.height)
            {
                contentRect.width = inRect.width - SCROLLBAR_WIDTH;
                contentRect.height = sectionsHeight(true);
            }

            // Start scrolling
            Widgets.BeginScrollView(inRect, ref scrollPos, contentRect);

            // Keep track of how far down the list we are
            float currentY = contentRect.yMin;

            for (int i = 0; i < sections.Count; i++)
            {
                if (i > 0) currentY += SECTION_GAP;

                if (sections[i].Heading != null)
                {
                    Rect headingRect = new Rect(
                        x: contentRect.xMin,
                        y: currentY + BLOCKED_SPACER_PADDING_TOP,
                        width: contentRect.width,
                        height: BLOCKED_SPACER_HEIGHT - BLOCKED_SPACER_PADDING_TOP - BLOCKED_SPACER_PADDING_BOTTOM
                    );
                    TextAnchor oldTextAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(headingRect, sections[i].Heading);
                    Text.Anchor = oldTextAnchor;
                    currentY += BLOCKED_SPACER_HEIGHT;
                }

                foreach (ImmutableUser user in sections[i].Users)
                {
                    float height = contentRect.height > inRect.height ? sections[i].Heights[user].Scrollbar : sections[i].Heights[user].Normal;
                    drawUser(new Rect(contentRect.xMin, currentY, contentRect.width, height), user, friend: sections[i].Friend);
                    currentY += height;
                }
            }

            // Blocked users
            if (filteredBlockedUsers.Any())
            {
                currentY += SECTION_GAP;

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
            friendUsersChanged = true;
        }

        /// <summary>
        /// Whether some text contains the search text, ignoring case.
        /// </summary>
        private bool matchesSearch(string text) => text.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) > -1;

        /// <summary>
        /// Repopulates <see cref="friendUsers"/>, <see cref="onlineUsers"/> and <see cref="blockedUsers"/>.
        /// </summary>
        /// <remarks>
        /// Everything is fetched from the client before taking <see cref="userListsLock"/>. The user manager raises
        /// the events that call this while holding its own lock, so asking it for users while holding ours would
        /// take the two locks in the opposite order to the network thread, which can deadlock.
        /// </remarks>
        private void refreshUserLists()
        {
            Dictionary<string, string> friends = Client.Instance.GetFriends();
            HashSet<string> blockedUuids = new HashSet<string>(Client.Instance.Settings.BlockedUsers);

            // Friends we know about on this server, online ones first, then alphabetically
            List<ImmutableUser> newFriends = new List<ImmutableUser>();
            foreach (string uuid in friends.Keys)
            {
                if (Client.Instance.TryGetUser(uuid, out ImmutableUser user)) newFriends.Add(user);
            }
            newFriends = newFriends
                .OrderByDescending(u => u.LoggedIn)
                .ThenBy(u => u.DisplayName.StripTags(), StringComparer.InvariantCultureIgnoreCase)
                .ToList();

            List<ImmutableUser> newBlocked = new List<ImmutableUser>();
            foreach (string uuid in blockedUuids)
            {
                if (Client.Instance.TryGetUser(uuid, out ImmutableUser user)) newBlocked.Add(user);
            }

            // Friends and blocked users have their own sections, so leave them out of the main list
            List<ImmutableUser> newOnline = Client.Instance.GetUsers(true)
                .Where(u => !friends.ContainsKey(u.Uuid) && !blockedUuids.Contains(u.Uuid))
                .ToList();

            lock (userListsLock)
            {
                friendUsers.Clear();
                friendUsers.AddRange(newFriends);
                friendNames = friends;

                blockedUsers.Clear();
                blockedUsers.AddRange(newBlocked);

                onlineUsers.Clear();
                onlineUsers.AddRange(newOnline);
            }

            // Flag the filtered lists to be repopulated
            friendUsersChanged = true;
            blockedUsersChanged = true;
            onlineUsersChanged = true;
        }

        /// <summary>
        /// Draws a user in the given container.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        /// <param name="user">User to draw</param>
        /// <param name="blocked">Whether the user is blocked</param>
        /// <param name="friend">Whether the user is a friend</param>
        private void drawUser(Rect inRect, ImmutableUser user, bool blocked = false, bool friend = false)
        {
            string formattedDisplayName = friend ? formatFriend(user) : formatDisplayName(user.DisplayName, blocked);

            if (blocked)
            {
                // Draw a highlighted background
                Widgets.DrawRectFast(inRect, blockedBackgroundColour);
            }
            else if (friend)
            {
                Widgets.DrawRectFast(inRect, friendBackgroundColour);
            }

            // Get a padded area to draw the text in
            Rect paddedRect = inRect.ContractedBy(USER_BUTTON_PADDING_HORIZONTAL, USER_BUTTON_PADDING_VERTICAL);

            // Draw the text
            Widgets.Label(paddedRect, Mouse.IsOver(inRect) ? formattedDisplayName.Colorize(Widgets.MouseoverOptionColor) : formattedDisplayName);

            // Their bio, if they've written one
            if (Client.Instance.TryGetBio(user.Uuid, out string bio)) TooltipHandler.TipRegion(inRect, bio);

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

            // A blocked player only gets Unblock: no trading, messages, gifts, contracts or friending until then
            if (Client.Instance.IsBlocked(user.Uuid))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("Phinix_chat_contextMenu_unblockUser".Translate(), () => Client.Instance.UnBlockUser(user.Uuid))
                }));
                return;
            }

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            if (Client.Instance.FeatureEnabled(Features.PROFILES)) items.Add(new FloatMenuOption("Phinix_chat_contextMenu_viewProfile".Translate(), () => Find.WindowStack.Add(new PlayerCardWindow(user.Uuid))));
            items.Add(
                new FloatMenuOption(
                    label: "Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(user.DisplayName)),
                    action: () => Client.Instance.CreateTrade(user.Uuid)
                )
            );
            // Only offered when the server has private messages turned on
            if (Client.Instance.DirectMessagesEnabled)
            {
                items.Add(
                    new FloatMenuOption(
                        label: "Phinix_chat_contextMenu_sendMessage".Translate(),
                        action: () => ServerTab.OpenConversation(user.Uuid)
                    )
                );
            }
            // Only offered when the server or a companion can take gifts (an original Phinix server can't)
            if (Client.Instance.GiftsAvailable)
            {
                items.Add(
                    new FloatMenuOption(
                        label: "Phinix_chat_contextMenu_sendGift".Translate(),
                        action: () => Find.WindowStack.Add(new SendGiftWindow(user.Uuid))
                    )
                );
            }
            // Only offered when the server has contracts turned on, and only for players who could take one (through a
            // companion, ones who have linked to it)
            if (Client.Instance.ContractsEnabled && Client.Instance.CanProposeContractTo(user.Uuid))
            {
                items.Add(
                    new FloatMenuOption(
                        label: "Phinix_chat_contextMenu_proposeContract".Translate(),
                        action: () => Find.WindowStack.Add(new ProposeContractWindow(user.Uuid))
                    )
                );
            }
            if (Client.Instance.FriendsEnabled) items.Add(
                new FloatMenuOption(
                    label: (Client.Instance.IsFriend(user.Uuid) ? "Phinix_chat_contextMenu_removeFriend" : "Phinix_chat_contextMenu_addFriend").Translate(),
                    action: () =>
                    {
                        if (Client.Instance.IsFriend(user.Uuid)) Client.Instance.RemoveFriend(user.Uuid);
                        else Client.Instance.AddFriend(user.Uuid);
                    }
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

            // Dev mode only: their name with its formatting tags, for looking into names that draw wrong
            if (Prefs.DevMode) items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyRawName".Translate(), () => GUIUtility.systemCopyBuffer = user.DisplayName));

            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// Formats a friend's entry: their name (greyed out with "offline" if they're not on), plus the name they were
        /// friended as on a second line if they've changed it since.
        /// </summary>
        /// <param name="user">The friend</param>
        /// <returns>Formatted text</returns>
        private string formatFriend(ImmutableUser user)
        {
            string text;
            if (user.LoggedIn) text = formatDisplayName(user.DisplayName, false);
            else if (Client.Instance.TryGetLastSeen(user.Uuid, out long lastSeen))
            {
                // Greyed out, with when they were last on in small text underneath
                text = TextHelper.StripRichText(user.DisplayName).Colorize(friendDetailColour) +
                       "\n" + ("<size=" + LAST_SEEN_FONT_SIZE + ">" + Client.FormatLastSeen(lastSeen) + "</size>").Colorize(friendDetailColour);
            }
            else text = (TextHelper.StripRichText(user.DisplayName) + " " + "Phinix_chat_friendOffline".Translate()).Colorize(friendDetailColour);

            if (friendNames.TryGetValue(user.Uuid, out string friendedAs) && friendedAs != TextHelper.StripRichText(user.DisplayName))
            {
                text += "\n" + "Phinix_chat_friendedAs".Translate(friendedAs).Resolve().Colorize(friendDetailColour);
            }

            return text;
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
                return Client.ShownName(displayName);
            }
        }
    }
}