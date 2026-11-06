using System.Collections.Generic;
using System.Linq;
using Trading;
using Utils;
using PhinixClient.GUI;
using RimWorld;
using UnityEngine;
using Verse;
using static PhinixClient.Client;

namespace PhinixClient
{
    public class ServerTab : MainTabWindow
    {
        private const float DEFAULT_SPACING = 10f;
        private const float COLUMN_SPACING = 20f;
        private const float SCROLLBAR_WIDTH = 16f;

        private const float CHAT_TEXTBOX_HEIGHT = 30f;

        private const float CHAT_SEND_BUTTON_HEIGHT = 30f;
        private const float CHAT_SEND_BUTTON_WIDTH = 80f;
        private const float CHAT_SCREENSHOT_BUTTON_WIDTH = 100f;

        private const float SETTINGS_BUTTON_HEIGHT = 30f;
        private const float SETTINGS_BUTTON_WIDTH = 210f;

        private const float USER_SEARCH_HEIGHT = 30f;
        private const float USER_SEARCH_WIDTH = 210f;

        private const float RIGHT_COLUMN_WIDTH = 210f;

        private const float POST_LISTING_BUTTON_HEIGHT = 30f;
        private const float POST_LISTING_BUTTON_WIDTH = 160f;

        // TODO: Add some kind of option to resize chat tab. Maybe a draggable corner?
        public override Vector2 InitialSize => new Vector2(1000f, 680f);

        private static string message = "";
        private static string userSearch = "";

        private readonly List<TabRecord> tabList = new List<TabRecord>();
        private int activeTab = 0;

        private readonly ChatMessageList chatMessageList = new ChatMessageList();
        private readonly UserList userList = new UserList();
        private readonly TradeList tradeList = new TradeList();
        private readonly MarketList marketList = new MarketList();
        private readonly BountyBoardList bountyBoardList = new BountyBoardList();
        private readonly InboxPanel inboxPanel = new InboxPanel();
        private readonly ContractList contractList = new ContractList();

        /// <summary>
        /// Index of the Inbox tab: private messages and trades together when private messages work (on the main
        /// server or through a companion), or just the Trades tab when they don't.
        /// </summary>
        private const int INBOX_TAB = 1;

        /// <summary>
        /// Index of the Contracts tab.
        /// </summary>
        private const int CONTRACTS_TAB = 4;

        /// <summary>
        /// A tab another part of the mod has asked to switch to, or -1 for none. Static so
        /// <see cref="OpenConversation"/> can set it even when this window isn't open yet.
        /// </summary>
        private static int requestedTab = -1;

        private bool chatMessageFieldFocused = false;

        /// <summary>
        /// Widest a tab gets.
        /// </summary>
        private const float MAX_TAB_WIDTH = 200f;

        /// <summary>
        /// Each tab's name and unseen count, filled in every frame. The tabs themselves have empty labels, since
        /// <see cref="drawTabLabels"/> draws the names with their badges.
        /// </summary>
        private readonly string[] tabNames = new string[5];
        private readonly int[] tabCounts = new int[5];

        public ServerTab()
        {
            // Populate the tab list
            tabList.Add(new TabRecord("", () => activeTab = 0, () => activeTab == 0));
            tabList.Add(new TabRecord("", () => activeTab = INBOX_TAB, () => activeTab == INBOX_TAB));
            tabList.Add(new TabRecord("", () => activeTab = 2, () => activeTab == 2));
            tabList.Add(new TabRecord("", () => activeTab = 3, () => activeTab == 3));
            tabList.Add(new TabRecord("", () => activeTab = CONTRACTS_TAB, () => activeTab == CONTRACTS_TAB));
        }

        /// <summary>
        /// Opens the Phinix window on the Inbox tab with a conversation selected, e.g. from "Send message" in the
        /// user list's menu.
        /// </summary>
        /// <param name="uuid">UUID of the other player</param>
        public static void OpenConversation(string uuid)
        {
            Instance.StartConversation(uuid);
            InboxPanel.SelectedUuid = uuid;
            requestedTab = INBOX_TAB;

            // The Phinix window is a "main tab", the kind opened from the bar along the bottom of the screen. Its def
            // is called "Chat" (see Defs/MainButtonDefs/PhinixClient.xml). Open it if it isn't already
            MainButtonDef chatButton = DefDatabase<MainButtonDef>.GetNamed("Chat");
            if (Find.MainTabsRoot.OpenTab != chatButton) Find.MainTabsRoot.SetCurrentTab(chatButton);
        }

        ///<inheritdoc/>
        /// <summary>
        /// Overrides the default accept key behaviour and instead sends a message.
        /// </summary>
        public override void OnAcceptKeyPressed()
        {
            // On the Inbox tab, Enter sends the private message instead
            if (activeTab == INBOX_TAB && showInbox) inboxPanel.SendDraft();
            else sendChatMessage();
        }

        /// <summary>
        /// Extends the default window drawing behaviour by drawing the Phinix chat interface.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public override void DoWindowContents(Rect inRect)
        {
            // The tabs sit along the top edge of tabsRect. Everything else goes in contentRect, which starts a little
            // lower so nothing is pressed right up against the tabs
            Rect tabsRect = inRect.BottomPartPixels(inRect.height - TabDrawer.TabHeight);
            Rect usableRect = tabsRect.BottomPartPixels(tabsRect.height - DEFAULT_SPACING);
            Rect rightColumnRect = usableRect.RightPartPixels(RIGHT_COLUMN_WIDTH);
            Rect chatRect = usableRect.LeftPartPixels(usableRect.width - (rightColumnRect.width + COLUMN_SPACING));

            // Switch tab if something asked us to (see OpenConversation)
            if (requestedTab >= 0)
            {
                activeTab = requestedTab;
                requestedTab = -1;
            }

            // Tab names, and a count badge on any that have something the player hasn't seen (see drawTabLabels)
            tabNames[0] = "Phinix_tabs_chat".Translate();
            tabCounts[0] = activeTab == 0 ? 0 : Instance.NewChatCount;
            // The Inbox counts new trades plus unread messages. The message part stays up while the tab is open, since
            // it counts unread messages from OTHER players too
            int newTrades = activeTab == INBOX_TAB ? 0 : Instance.NewTradeCount;
            tabNames[INBOX_TAB] = (showInbox ? "Phinix_tabs_inbox" : "Phinix_tabs_trades").Translate();
            tabCounts[INBOX_TAB] = showInbox ? newTrades + Instance.NewDirectMessageCount : newTrades;
            tabNames[2] = "Phinix_tabs_market".Translate();
            tabCounts[2] = activeTab == 2 ? 0 : Instance.NewShopCount;
            tabNames[3] = "Phinix_tabs_bounties".Translate();
            tabCounts[3] = activeTab == 3 ? 0 : Instance.NewBountyCount;
            tabNames[CONTRACTS_TAB] = "Phinix_tabs_contracts".Translate();
            tabCounts[CONTRACTS_TAB] = activeTab == CONTRACTS_TAB ? 0 : Instance.NewContractCount;
            if (!Instance.Online) for (int i = 0; i < tabCounts.Length; i++) tabCounts[i] = 0;
            // Some tabs only show when the server has that feature turned on. Hide those by leaving them out of the
            // list we draw, and if the open tab just got hidden, fall back to Chat
            bool showContracts = Instance.Online && Instance.ShowContracts;
            // Market and Bounties only show once we're logged in to a server that has them switched on
            bool showMarket = Instance.MarketAvailable && Instance.FeatureEnabled(UserManagement.Features.MARKET);
            bool showBounties = Instance.MarketAvailable && Instance.FeatureEnabled(UserManagement.Features.BOUNTIES);
            if (!showContracts && activeTab == CONTRACTS_TAB) activeTab = 0;
            if (!showMarket && activeTab == 2) activeTab = 0;
            if (!showBounties && activeTab == 3) activeTab = 0;
            List<int> visibleIndices = Enumerable.Range(0, tabList.Count)
                .Where(index => (index != CONTRACTS_TAB || showContracts) && (index != 2 || showMarket) && (index != 3 || showBounties))
                .ToList();
            List<TabRecord> visibleTabs = visibleIndices.Select(index => tabList[index]).ToList();
            TabDrawer.DrawTabs(tabsRect, visibleTabs, MAX_TAB_WIDTH);
            drawTabLabels(tabsRect, visibleIndices);

            switch (activeTab)
            {
                case 0: // Chat tab
                    GenerateChat(chatRect); // Chat
                    GenerateRightColumn(rightColumnRect); // Right column
                    break;
                case INBOX_TAB: // Inbox (or Trades) tab
                    GenerateInbox(usableRect);
                    if (Instance.Online) Instance.MarkTradesSeen();
                    break;
                case 2: // Market tab
                    GenerateMarket(usableRect); // Player markets
                    if (Instance.Online) Instance.MarkShopsSeen();
                    break;
                case 3: // Bounties tab
                    GenerateBounties(usableRect); // Bounty board
                    if (Instance.Online) Instance.MarkBountiesSeen();
                    break;
                case CONTRACTS_TAB: // Contracts tab
                    GenerateContracts(usableRect);
                    if (Instance.Online) Instance.MarkContractsSeen();
                    break;
                default:
                    Widgets.DrawMenuSection(usableRect); // Placeholder
                    break;
            }
        }

        /// <summary>
        /// Draws each visible tab's name with its count badge, and a gold line under the open tab.
        /// </summary>
        /// <remarks>
        /// The game's tab drawer can only draw a plain centred label, so the tabs are given empty labels and the names
        /// are drawn here on top, in the same places the drawer puts the tabs (same maths as <c>TabDrawer.DrawTabs</c>).
        /// Badges are red for things waiting on the player (messages, trades, contract proposals) and grey for general
        /// news (chat, new markets and bounties).
        /// </remarks>
        /// <param name="tabsRect">The rect passed to <c>TabDrawer.DrawTabs</c></param>
        /// <param name="visibleIndices">Indices into <see cref="tabList"/> of the tabs that were drawn, in order</param>
        private void drawTabLabels(Rect tabsRect, List<int> visibleIndices)
        {
            int count = visibleIndices.Count;
            float tabWidth = Mathf.Min(MAX_TAB_WIDTH, (tabsRect.width + (count - 1) * TabDrawer.TabHoriztonalOverlap) / count);

            for (int i = 0; i < count; i++)
            {
                int index = visibleIndices[i];
                Rect tabRect = new Rect(tabsRect.x + i * (tabWidth - TabDrawer.TabHoriztonalOverlap), tabsRect.y - TabDrawer.TabHeight + 1f, tabWidth, TabDrawer.TabHeight);

                // Centre the name and badge together. The game turns a tab's label yellow under the mouse; do the same
                string name = tabNames[index];
                string badgeText = tabCounts[index] > 0 ? PhimeStyle.CountText(tabCounts[index]) : null;
                float nameWidth = PhimeStyle.TextWidth(name, GameFont.Small);
                float total = nameWidth + (badgeText != null ? 7f + PhimeStyle.BadgeWidth(badgeText) : 0f);
                float x = tabRect.center.x - total / 2;
                bool hovered = Mouse.IsOver(new Rect(tabRect.x, tabRect.y, tabRect.width - TabDrawer.TabHoriztonalOverlap, tabRect.height));
                PhimeStyle.Label(new Rect(x, tabRect.y, nameWidth + 2f, tabRect.height), name, hovered ? Color.yellow : Color.white, GameFont.Small, TextAnchor.MiddleCenter);

                if (badgeText != null)
                {
                    bool urgent = index == INBOX_TAB || index == CONTRACTS_TAB;
                    PhimeStyle.Badge(x + nameWidth + 7f, tabRect.center.y, badgeText, urgent ? PhimeStyle.BadgeRed : PhimeStyle.BadgeGrey);
                }

                if (index == activeTab) Widgets.DrawBoxSolid(new Rect(tabRect.x + 12f, tabRect.yMax - 3f, tabRect.width - 24f, 2f), PhimeStyle.Gold);
            }
        }

        /// <summary>
        /// Draws a column containing a settings button, user search box, and a user list.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateRightColumn(Rect inRect)
        {
            // Top to bottom: search box, "Online (n)" heading, the user list, and the Settings button along the
            // bottom, lined up with the chat box and Send button on the left
            Rect userSearchRect = inRect.TopPartPixels(USER_SEARCH_HEIGHT);
            Rect headingRect = new Rect(inRect.x, userSearchRect.yMax + DEFAULT_SPACING, inRect.width, Text.LineHeight);
            Rect settingsButtonRect = inRect.BottomPartPixels(SETTINGS_BUTTON_HEIGHT);
            Rect userListRect = new Rect(inRect.x, headingRect.yMax + 4f, inRect.width, settingsButtonRect.y - DEFAULT_SPACING - (headingRect.yMax + 4f));

            // User search box, with grey "Search players..." showing while it's empty
            string userSearchOld = userSearch;
            userSearch = Widgets.TextField(userSearchRect, userSearch);
            if (string.IsNullOrEmpty(userSearch)) drawPlaceholder(userSearchRect, "Phinix_chat_searchPlaceholder".Translate());
            if (!userSearch.Equals(userSearchOld))
            {
                userList.Filter(userSearch);
            }

            // Small grey heading so the list doesn't just float there
            int onlineCount = Instance.Online ? Instance.GetUsers(true).Length : 0;
            PhimeStyle.Heading(headingRect, "Phinix_chat_onlineHeading".Translate(onlineCount));

            // Settings button
            if (PhimeStyle.Button(settingsButtonRect, "Phinix_chat_settingsButton".Translate()))
            {
                Find.WindowStack.Add(new SettingsWindow());
            }

            // User list
            if (Instance.Online)
            {
                userList.Draw(userListRect);
            }
            else
            {
                Widgets.DrawMenuSection(userListRect);
            }
        }

        /// <summary>
        /// Draws faint hint text inside an empty text box, like the grey "Search..." in a browser's search bar.
        /// </summary>
        private static void drawPlaceholder(Rect textBoxRect, string text)
        {
            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            UnityEngine.GUI.color = new Color(1f, 1f, 1f, 0.35f);
            Widgets.Label(new Rect(textBoxRect.x + 6f, textBoxRect.y, textBoxRect.width - 6f, textBoxRect.height), text);
            UnityEngine.GUI.color = Color.white;
            Text.Anchor = previousAnchor;
        }

        /// <summary>
        /// Generates the chat window consisting of a message list, message entry box, and a send button.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateChat(Rect inRect)
        {
            // The Screenshot button only shows when our server (or companion) keeps screenshots
            bool canShareScreenshots = Instance.ScreenshotsAvailable;
            float screenshotButtonSpace = canShareScreenshots ? CHAT_SCREENSHOT_BUTTON_WIDTH + DEFAULT_SPACING : 0f;

            // A square emoji button sits between the message box and the other buttons, if the server has emojis on
            bool showEmojiButton = Emoji.Enabled;
            float emojiButtonSpace = showEmojiButton ? CHAT_TEXTBOX_HEIGHT + DEFAULT_SPACING : 0f;

            Rect sendButtonRect = inRect.BottomPartPixels(CHAT_TEXTBOX_HEIGHT).RightPartPixels(CHAT_SEND_BUTTON_WIDTH);
            Rect screenshotButtonRect = new Rect(sendButtonRect.x - screenshotButtonSpace, sendButtonRect.y, CHAT_SCREENSHOT_BUTTON_WIDTH, sendButtonRect.height);
            Rect emojiButtonRect = new Rect(sendButtonRect.x - screenshotButtonSpace - emojiButtonSpace, sendButtonRect.y, CHAT_TEXTBOX_HEIGHT, sendButtonRect.height);
            Rect messageBoxRect = inRect.BottomPartPixels(CHAT_TEXTBOX_HEIGHT).LeftPartPixels(inRect.width - (CHAT_SEND_BUTTON_WIDTH + DEFAULT_SPACING + screenshotButtonSpace + emojiButtonSpace));
            Rect chatRect = inRect.TopPartPixels(inRect.height - (messageBoxRect.height + DEFAULT_SPACING));

            // Chat message area
            if (Instance.Online)
            {
                // A framed box behind the messages, matching the other tabs, so the chat area doesn't blend into the
                // window when it's empty. The messages are drawn just inside the frame
                Widgets.DrawMenuSection(chatRect);
                chatMessageList.Draw(chatRect.ContractedBy(4f));
            }
            else
            {
                Widgets.DrawMenuSection(chatRect);
                Widgets.NoneLabelCenteredVertically(chatRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }

            // Message entry field
            UnityEngine.GUI.SetNextControlName("Phinix_chatMessageField");
            message = Widgets.TextField(messageBoxRect, message);

            if (Client.Instance.Settings.ForceMessageFieldFocus)
            {
                // Aggressively hold the focus on the chat field until clicked out of
                if (Input.GetMouseButtonDown(0))
                {
                    chatMessageFieldFocused = Mouse.IsOver(messageBoxRect);
                }
                if (chatMessageFieldFocused) UnityEngine.GUI.FocusControl("Phinix_chatMessageField");
            }

            // Emoji button: opens the picker, which adds :shortcodes: to the message
            if (showEmojiButton)
            {
                if (PhimeStyle.Button(emojiButtonRect, ""))
                {
                    Find.WindowStack.Add(new EmojiPickerWindow(name => message = (message.Length == 0 || message.EndsWith(" ") ? message : message + " ") + ":" + name + ": "));
                }
                Emoji.Draw(emojiButtonRect.ContractedBy(6f), "slightly_smiling_face");
                TooltipHandler.TipRegion(emojiButtonRect, "Phinix_emoji_buttonTip".Translate());
            }

            // Screenshot button: closes this window, takes a screenshot and offers to share it
            if (canShareScreenshots && PhimeStyle.Button(screenshotButtonRect, "Phinix_screenshot_button".Translate()))
            {
                ScreenshotSharing.TakeAndShare();
            }

            // Send button
            if (PhimeStyle.Button(sendButtonRect, "Phinix_chat_sendButton".Translate()))
            {
                sendChatMessage();
            }
        }

        /// <summary>
        /// Whether the Inbox replaces the Trades tab: when private messages work, on the main server or through a
        /// companion. Otherwise (an original Phinix server and no companion) there are only trades to show.
        /// </summary>
        private static bool showInbox => Instance.Online && Instance.DirectMessagesEnabled;

        /// <summary>
        /// Generates the Inbox (private messages and trades, per player), or just the active trade list when private
        /// messages aren't available.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateInbox(Rect inRect)
        {
            if (showInbox)
            {
                inboxPanel.Draw(inRect);
            }
            else if (Instance.Online)
            {
                tradeList.Draw(inRect);
            }
            else
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
            }
        }

        /// <summary>
        /// Generates the list of player markets, with buttons above it for our own market and open trades.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateMarket(Rect inRect)
        {
            if (!Instance.Online)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
                return;
            }

            Rect postButtonRect = inRect.TopPartPixels(POST_LISTING_BUTTON_HEIGHT).RightPartPixels(POST_LISTING_BUTTON_WIDTH);
            Rect boardRect = inRect.BottomPartPixels(inRect.height - (POST_LISTING_BUTTON_HEIGHT + DEFAULT_SPACING));

            // "MARKETS  5 open · 31 listings" on the left
            Agora.Shop[] shops = Instance.GetVisibleShops();
            drawTabHeading(inRect, "Phinix_market_heading".Translate(), "Phinix_market_summary".Translate(shops.Length, shops.Sum(shop => shop.Stock.Count)));

            // Our market: open one if we haven't, otherwise jump straight to managing it
            Agora.Shop myShop = Instance.MyShop;
            if (PhimeStyle.Button(postButtonRect, (myShop == null ? "Phinix_shop_openButton" : "Phinix_shop_myShopButton").Translate()))
            {
                if (myShop == null) Find.WindowStack.Add(new NameShopWindow());
                else Find.WindowStack.Add(new ShopWindow(myShop.ShopId));
            }

            // Open trades button, just left of the post button, with a count badge. Greyed out when there aren't any
            Rect tradesButtonRect = new Rect(postButtonRect.xMin - DEFAULT_SPACING - POST_LISTING_BUTTON_WIDTH, postButtonRect.yMin, POST_LISTING_BUTTON_WIDTH, POST_LISTING_BUTTON_HEIGHT);
            ImmutableTrade[] trades = Instance.Settings.ShowBlockedTrades ? Instance.GetTrades() : Instance.GetTradesExceptWith(Instance.Settings.BlockedUsers);
            if (PhimeStyle.Button(tradesButtonRect, "Phinix_market_openTradesLabel".Translate(), active: trades.Length > 0, badgeCount: trades.Length))
            {
                openTradeOrPick(trades);
            }

            // Everyone's markets
            marketList.Draw(boardRect);
        }

        /// <summary>
        /// Draws a tab's small grey heading and summary in its top-left corner, like "MARKETS  5 open · 31 listings".
        /// </summary>
        /// <param name="inRect">The tab's whole area</param>
        private static void drawTabHeading(Rect inRect, string heading, string summary)
        {
            float headingWidth = PhimeStyle.TextWidth(heading.ToUpperInvariant(), GameFont.Tiny);
            PhimeStyle.Heading(new Rect(inRect.x + 2f, inRect.y, headingWidth + 2f, POST_LISTING_BUTTON_HEIGHT), heading);
            PhimeStyle.Label(new Rect(inRect.x + 2f + headingWidth + 10f, inRect.y, 300f, POST_LISTING_BUTTON_HEIGHT), summary, PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// Generates the bounty board, with a button above it for posting a new bounty.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateBounties(Rect inRect)
        {
            if (!Instance.Online)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
                return;
            }

            Rect postButtonRect = inRect.TopPartPixels(POST_LISTING_BUTTON_HEIGHT).RightPartPixels(POST_LISTING_BUTTON_WIDTH);
            Rect boardRect = inRect.BottomPartPixels(inRect.height - (POST_LISTING_BUTTON_HEIGHT + DEFAULT_SPACING));

            // "BOUNTIES  3 up · 1 yours" on the left, like the Market tab
            Agora.Bounty[] bounties = Instance.GetVisibleBounties();
            drawTabHeading(inRect, "Phinix_bounty_heading".Translate(),
                "Phinix_bounty_summary".Translate(bounties.Length, bounties.Count(bounty => bounty.PosterUuid == Instance.Uuid)));

            if (PhimeStyle.Button(postButtonRect, "Phinix_bounty_newBountyButton".Translate()))
            {
                Find.WindowStack.Add(new PostBountyWindow());
            }

            bountyBoardList.Draw(boardRect);
        }

        /// <summary>
        /// Generates the contract list, with a button above it for proposing a new contract.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        private void GenerateContracts(Rect inRect)
        {
            if (!Instance.Online)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_chat_pleaseLogInPlaceholder".Translate());
                return;
            }

            Rect proposeButtonRect = inRect.TopPartPixels(POST_LISTING_BUTTON_HEIGHT).RightPartPixels(POST_LISTING_BUTTON_WIDTH);
            Rect listRect = inRect.BottomPartPixels(inRect.height - (POST_LISTING_BUTTON_HEIGHT + DEFAULT_SPACING));

            // A contract is with one specific player, so the button asks who first. Offline players are included,
            // since they'll see the proposal when they next log in. Hidden if the server has turned new contracts off
            Agora.Contract[] contracts = Instance.GetVisibleContracts();
            drawTabHeading(inRect, "Phinix_contract_heading".Translate(),
                "Phinix_contract_summary".Translate(contracts.Count(contract => contract.State == Agora.ContractState.Active), contracts.Count(contract => contract.State == Agora.ContractState.Proposed)));

            if (Instance.ContractsEnabled && PhimeStyle.Button(proposeButtonRect, "Phinix_contract_newContractButton".Translate()))
            {
                // Only players who can actually take a contract (see Client.GetContractPartners): online ones first, then
                // friends, then by name
                List<FloatMenuOption> options = Instance.GetContractPartners()
                    .OrderByDescending(user => user.LoggedIn)
                    .ThenByDescending(user => Instance.IsFriend(user.Uuid))
                    .ThenBy(user => TextHelper.StripRichText(user.DisplayName))
                    .Select(user => new FloatMenuOption(
                        user.LoggedIn
                            ? TextHelper.StripRichText(user.DisplayName)
                            : "Phinix_contract_partnerOffline".Translate(TextHelper.StripRichText(user.DisplayName)).ToString(),
                        () => Find.WindowStack.Add(new ProposeContractWindow(user.Uuid))
                    ))
                    .ToList();
                if (options.Count == 0) options.Add(new FloatMenuOption("Phinix_contract_noPlayers".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(options));
            }

            contractList.Draw(listRect);
        }

        /// <summary>
        /// Opens the trade window straight away if there's only one trade, otherwise shows a menu to pick which one.
        /// </summary>
        /// <param name="trades">Active trades</param>
        private void openTradeOrPick(ImmutableTrade[] trades)
        {
            if (trades.Length == 1)
            {
                Find.WindowStack.Add(new TradeWindow(trades[0]));
                return;
            }

            // A FloatMenu is the little pop-up list RimWorld shows when you right-click something
            List<FloatMenuOption> options = trades
                .Select(trade => new FloatMenuOption(
                    "Phinix_trade_activeTrade_tradeWithLabel".Translate(TextHelper.StripRichText(trade.OtherPartyDisplayName)),
                    () => Find.WindowStack.Add(new TradeWindow(trade))
                ))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// Sends <see cref="message"/> to the server.
        /// </summary>
        private void sendChatMessage()
        {
            if (!string.IsNullOrEmpty(message) && Instance.Online)
            {
                // TODO: Make chat message 'sent' callback to remove message, preventing removal of lengthy messages for nothing and causing frustration
                Instance.SendMessage(message);
                chatMessageList.ScrollToBottom();

                message = "";
            }
        }
    }
}
