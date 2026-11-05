using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hermes;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    /// <summary>
    /// The Inbox tab: private messages and trades together, one entry per player. The list on the left has everyone
    /// we're talking to or trading with. On the right is the selected player's name and status, their open trades, then
    /// the conversation, with a box to type in and Send, Trade and Send gift buttons along the bottom.
    /// </summary>
    /// <remarks>
    /// Only used when private messages work (on the main server or through a companion). Without them, the tab is
    /// the plain trade list instead (see ServerTab).
    /// <para>
    /// This is drawn with RimWorld's plain <c>Widgets</c> calls rather than Phinix's container classes, because it
    /// needs two scrolling areas side by side and it's easier to read as one function per area. Colours and badges
    /// come from <see cref="PhimeStyle"/>.
    /// </para>
    /// <para>
    /// RimWorld redraws every window every frame (this is called "immediate mode" UI), so <see cref="Draw"/> runs about
    /// 60 times a second. Anything that should be remembered between frames, like which player is open, lives in a
    /// field.
    /// </para>
    /// </remarks>
    public class InboxPanel
    {
        private const float SPACING = 10f;
        private const float LIST_WIDTH = 220f;
        private const float LIST_HEADING_HEIGHT = 26f;
        private const float LIST_ROW_HEIGHT = 48f;
        private const float HEADER_HEIGHT = 32f;
        private const float ROW_HEIGHT = 30f;
        private const float TRADE_ROW_HEIGHT = 38f;
        private const float BUTTON_WIDTH = 100f;
        private const float TRADE_BUTTON_WIDTH = 80f;
        private const float MESSAGE_PADDING = 4f;
        private const float SCROLLBAR_WIDTH = 16f;

        /// <summary>
        /// Extra space above a message from a different sender than the one before, so each turn reads as a group.
        /// </summary>
        private const float SENDER_CHANGE_GAP = 6f;

        private const float DAY_SEPARATOR_HEIGHT = 24f;
        private const float EVENT_HEIGHT = 18f;

        /// <summary>
        /// How far trade event lines are indented, roughly past the time column.
        /// </summary>
        private const float EVENT_INDENT = 44f;

        /// <summary>
        /// Most trade rows shown above the conversation. There's normally just one trade per player.
        /// </summary>
        private const int MAX_TRADE_ROWS = 3;

        /// <summary>
        /// UUID of the player that's open. Static so <see cref="ServerTab.OpenConversation"/> can pick one from
        /// anywhere, e.g. the "Send message" option in the user list's menu.
        /// </summary>
        public static string SelectedUuid;

        /// <summary>
        /// What the player has typed so far.
        /// </summary>
        private string draft = "";

        /// <summary>
        /// Where each scrolling area is scrolled to. Unity keeps these as plain values we hand back every frame.
        /// </summary>
        private Vector2 listScroll = Vector2.zero;
        private Vector2 messageScroll = Vector2.zero;

        /// <summary>
        /// How many lines the open conversation had last frame, so we can jump to the bottom when a new one arrives.
        /// </summary>
        private int lastLineCount = -1;
        private string lastSelectedUuid;

        /// <summary>
        /// Draws the panel.
        /// </summary>
        /// <param name="inRect">Area to draw in</param>
        public void Draw(Rect inRect)
        {
            Rect listRect = inRect.LeftPartPixels(LIST_WIDTH);
            Rect playerRect = inRect.RightPartPixels(inRect.width - LIST_WIDTH - SPACING);

            // Both sides need the trades and who's online, so fetch them once per frame
            ImmutableTrade[] trades = Client.Instance.GetVisibleTrades();
            HashSet<string> online = new HashSet<string>(Client.Instance.GetUsers(true).Select(user => user.Uuid));

            drawPlayerList(listRect, trades, online);
            drawPlayer(playerRect, trades, online);
        }

        /// <summary>
        /// Sends whatever has been typed to the open conversation. Also called when Enter is pressed.
        /// </summary>
        public void SendDraft()
        {
            if (SelectedUuid == null || string.IsNullOrWhiteSpace(draft) || !Client.Instance.Online) return;

            Client.Instance.SendDirectMessage(SelectedUuid, draft);
            draft = "";
        }

        /// <summary>
        /// Everyone to list on the left: players we have an open trade with but haven't messaged first (a trade is
        /// waiting on someone), then conversations with the newest activity at the top.
        /// </summary>
        private static string[] playersToList(ImmutableTrade[] trades)
        {
            // Blocked players' conversations are hidden, the same way their chat messages are. Their trades are
            // already left out by GetVisibleTrades unless the player chose to see them
            string[] partners = Client.Instance.GetConversationPartners()
                .Where(uuid => !Client.Instance.Settings.BlockedUsers.Contains(uuid))
                .ToArray();

            IEnumerable<string> tradeOnly = trades
                .Select(trade => trade.OtherPartyUuid)
                .Where(uuid => !partners.Contains(uuid))
                .Distinct();

            return tradeOnly.Concat(partners).ToArray();
        }

        /// <summary>
        /// Left side: one row per player with an online dot, their name in their colour, a second line with the trade
        /// status or their last message, and an unread count.
        /// </summary>
        private void drawPlayerList(Rect inRect, ImmutableTrade[] trades, HashSet<string> online)
        {
            Widgets.DrawMenuSection(inRect);
            PhimeStyle.Heading(new Rect(inRect.x + 10f, inRect.y + 2f, inRect.width - 20f, LIST_HEADING_HEIGHT - 2f), "Phinix_inbox_heading".Translate());

            string[] players = playersToList(trades);
            if (players.Length == 0)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_inbox_empty".Translate());
                return;
            }

            // A scroll view needs to know how tall its contents are ("viewRect") as well as the visible area. The
            // scroll bar only takes space when there are enough players to need it
            Rect outRect = new Rect(inRect.x + 1f, inRect.y + LIST_HEADING_HEIGHT, inRect.width - 2f, inRect.height - LIST_HEADING_HEIGHT - 1f);
            float contentHeight = players.Length * LIST_ROW_HEIGHT;
            float rowWidth = outRect.width - (contentHeight > outRect.height ? SCROLLBAR_WIDTH : 0f);
            Rect viewRect = new Rect(0, 0, rowWidth, contentHeight);
            Widgets.BeginScrollView(outRect, ref listScroll, viewRect);

            for (int i = 0; i < players.Length; i++)
            {
                string uuid = players[i];
                Rect rowRect = new Rect(0, i * LIST_ROW_HEIGHT, rowWidth, LIST_ROW_HEIGHT);

                // The open player gets a gold highlight and edge. Others light up under the mouse
                if (uuid == SelectedUuid)
                {
                    Widgets.DrawBoxSolid(rowRect, PhimeStyle.GoldHighlight);
                    PhimeStyle.AccentEdge(rowRect);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(rowRect);
                }
                Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), PhimeStyle.Separator);

                PhimeStyle.Dot(new Vector2(rowRect.x + 14f, rowRect.y + 17f), 8f, online.Contains(uuid) ? PhimeStyle.Green : PhimeStyle.OfflineDot);

                // Unread count on the right, then the name and second line fill what's left
                float textX = rowRect.x + 26f;
                float rightEdge = rowRect.xMax - 8f;
                int unread = Client.Instance.UnreadDirectMessages(uuid);
                if (unread > 0)
                {
                    string countText = PhimeStyle.CountText(unread);
                    rightEdge -= PhimeStyle.BadgeWidth(countText);
                    PhimeStyle.Badge(rightEdge, rowRect.center.y, countText, PhimeStyle.BadgeRed);
                    rightEdge -= 6f;
                }
                float textWidth = rightEdge - textX;

                string rawName = rawNameOf(uuid, trades);
                string name = PhimeStyle.Fit(TextHelper.StripRichText(rawName), textWidth, GameFont.Small);
                PhimeStyle.Label(new Rect(textX, rowRect.y + 5f, textWidth, 22f), name, PhimeStyle.NameColor(rawName, Color.white));

                Rect subRect = new Rect(textX, rowRect.y + 26f, textWidth, 16f);
                ImmutableTrade[] theirTrades = trades.Where(trade => trade.OtherPartyUuid == uuid).ToArray();
                if (theirTrades.Length > 0)
                {
                    // Gold while it's open, green once they've accepted
                    if (theirTrades.Any(trade => trade.OtherPartyAccepted)) PhimeStyle.IconLabel(subRect, Widgets.CheckboxOnTex, "Phinix_inbox_tradeAccepted".Translate(), PhimeStyle.Green);
                    else PhimeStyle.IconLabel(subRect, PhimeStyle.TradeIcon, "Phinix_inbox_tradeOpen".Translate(), PhimeStyle.Gold);
                }
                else
                {
                    // A peek at the last message, ours marked with "You:"
                    DirectMessage last = Client.Instance.GetConversation(uuid).LastOrDefault();
                    if (last != null)
                    {
                        string lastText = ScreenshotSharing.ShowLinks(TradeRequests.Readable(last.Text), colour: false);
                        string preview = last.SenderUuid == Client.Instance.Uuid ? "Phinix_inbox_youPrefix".Translate(lastText).ToString() : lastText;
                        preview = TextHelper.StripRichText(preview).Replace('\n', ' ');
                        PhimeStyle.Label(subRect, PhimeStyle.Fit(preview, textWidth, GameFont.Tiny), PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
                    }
                }

                // Right-click for the same friend and block options as the chat's player list. Checked first and
                // the click used up, so it doesn't also open the conversation
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rowRect))
                {
                    Event.current.Use();
                    openPlayerMenu(uuid);
                }
                if (Widgets.ButtonInvisible(rowRect)) SelectedUuid = uuid;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// Right side: the open player's name and status, their trades, their conversation, then the text box and
        /// buttons along the bottom.
        /// </summary>
        private void drawPlayer(Rect inRect, ImmutableTrade[] trades, HashSet<string> online)
        {
            if (SelectedUuid == null)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_inbox_pick".Translate());
                return;
            }

            ImmutableTrade[] theirTrades = trades.Where(trade => trade.OtherPartyUuid == SelectedUuid).ToArray();
            string rawName = rawNameOf(SelectedUuid, trades);
            string name = TextHelper.StripRichText(rawName);

            // Top: who this is, and whether they're around
            Rect headerRect = inRect.TopPartPixels(HEADER_HEIGHT);
            drawHeader(headerRect, rawName, online.Contains(SelectedUuid));

            // Past trades with them, in the header's top right corner
            Rect historyButtonRect = new Rect(headerRect.xMax - BUTTON_WIDTH, headerRect.y + (headerRect.height - 24f) / 2f, BUTTON_WIDTH, 24f);
            if (PhimeStyle.Button(historyButtonRect, "Phinix_tradeHistory_button".Translate()))
            {
                Find.WindowStack.Add(new TradeHistoryWindow(SelectedUuid, name));
            }

            // Then any open trades with them
            int tradeRows = Math.Min(theirTrades.Length, MAX_TRADE_ROWS);
            float tradesHeight = tradeRows > 0 ? tradeRows * TRADE_ROW_HEIGHT + MESSAGE_PADDING * 2 : 0f;
            Rect tradesRect = new Rect(inRect.x, headerRect.yMax + MESSAGE_PADDING, inRect.width, tradesHeight);
            if (tradeRows > 0) drawTrades(tradesRect, theirTrades.Take(tradeRows).ToArray());

            // Bottom strip: [ text box ...... ][ Screenshot ][ Send ][ Trade ][ Send gift ]
            // The gift button is left out when neither the main server nor a companion can take gifts, and the other
            // buttons move over into its place. The Screenshot button likewise only shows where screenshots are kept
            bool showGiftButton = Client.Instance.GiftsAvailable;
            bool showScreenshotButton = Client.Instance.ScreenshotsAvailable;
            Rect bottomRect = inRect.BottomPartPixels(ROW_HEIGHT);
            Rect giftButtonRect = showGiftButton
                ? bottomRect.RightPartPixels(BUTTON_WIDTH)
                : new Rect(bottomRect.xMax + SPACING, bottomRect.y, 0f, ROW_HEIGHT);
            Rect tradeButtonRect = new Rect(giftButtonRect.x - SPACING - BUTTON_WIDTH, bottomRect.y, BUTTON_WIDTH, ROW_HEIGHT);
            Rect sendButtonRect = new Rect(tradeButtonRect.x - SPACING - BUTTON_WIDTH, bottomRect.y, BUTTON_WIDTH, ROW_HEIGHT);
            Rect screenshotButtonRect = showScreenshotButton
                ? new Rect(sendButtonRect.x - SPACING - BUTTON_WIDTH, bottomRect.y, BUTTON_WIDTH, ROW_HEIGHT)
                : new Rect(sendButtonRect.x, bottomRect.y, 0f, ROW_HEIGHT);
            Rect draftRect = new Rect(bottomRect.x, bottomRect.y, screenshotButtonRect.x - SPACING - bottomRect.x, ROW_HEIGHT);

            // The conversation fills whatever's left in between
            float messagesTop = (tradeRows > 0 ? tradesRect.yMax : headerRect.yMax) + SPACING;
            Rect messagesRect = new Rect(inRect.x, messagesTop, inRect.width, bottomRect.y - SPACING - messagesTop);
            drawConversation(messagesRect, rawName);

            // Cut the draft off at the server's limit so the player can see where it stops. Faint "Message Name..."
            // while it's empty
            draft = Widgets.TextField(draftRect, draft);
            if (draft.Length > Hermes.Hermes.MAX_MESSAGE_LENGTH) draft = draft.Substring(0, Hermes.Hermes.MAX_MESSAGE_LENGTH);
            if (draft.Length == 0)
            {
                PhimeStyle.Label(new Rect(draftRect.x + 6f, draftRect.y, draftRect.width - 6f, draftRect.height),
                    "Phinix_inbox_messagePlaceholder".Translate(name), new Color(1f, 1f, 1f, 0.35f), GameFont.Small, TextAnchor.MiddleLeft);
            }

            // Screenshot takes one and offers to send it to this player
            if (showScreenshotButton && PhimeStyle.Button(screenshotButtonRect, "Phinix_screenshot_button".Translate()))
            {
                ScreenshotSharing.TakeAndShare(SelectedUuid);
            }

            if (PhimeStyle.Button(sendButtonRect, "Phinix_chat_sendButton".Translate())) SendDraft();

            // Trade starts a new trade. With one already open it's greyed out, since the Open button above does that
            bool canStartTrade = theirTrades.Length == 0;
            if (PhimeStyle.Button(tradeButtonRect, "Phinix_inbox_tradeButton".Translate(), active: canStartTrade))
            {
                Client.Instance.CreateTrade(SelectedUuid);
            }
            if (!canStartTrade) TooltipHandler.TipRegion(tradeButtonRect, "Phinix_inbox_tradeAlreadyOpenTip".Translate());

            if (showGiftButton && PhimeStyle.Button(giftButtonRect, "Phinix_dm_sendGiftButton".Translate()))
            {
                Find.WindowStack.Add(new SendGiftWindow(SelectedUuid));
            }

            // Looking at a conversation reads it
            Client.Instance.MarkDirectMessagesRead(SelectedUuid);
        }

        /// <summary>
        /// The player's name in large text and their colour, then a dot with "Online" or "Offline", and "via companion" when
        /// messages go through a companion server.
        /// </summary>
        private static void drawHeader(Rect inRect, string rawName, bool isOnline)
        {
            string name = TextHelper.StripRichText(rawName);
            float x = inRect.x;

            float nameWidth = Mathf.Min(PhimeStyle.TextWidth(name, GameFont.Medium), inRect.width * 0.6f);
            PhimeStyle.Label(new Rect(x, inRect.y, nameWidth + 2f, inRect.height), PhimeStyle.Fit(name, nameWidth + 2f, GameFont.Medium),
                PhimeStyle.NameColor(rawName, Color.white), GameFont.Medium, TextAnchor.MiddleLeft);
            x += nameWidth + 12f;

            Color statusColor = isOnline ? PhimeStyle.Green : PhimeStyle.Muted;
            PhimeStyle.Dot(new Vector2(x + 3f, inRect.center.y + 1f), 6f, isOnline ? PhimeStyle.Green : PhimeStyle.OfflineDot);
            x += 10f;
            string status = (isOnline ? "Phinix_inbox_online" : "Phinix_inbox_offline").Translate();
            float statusWidth = PhimeStyle.TextWidth(status, GameFont.Tiny);
            PhimeStyle.Label(new Rect(x, inRect.y, statusWidth + 2f, inRect.height), status, statusColor, GameFont.Tiny, TextAnchor.MiddleLeft);
            x += statusWidth + 6f;

            if (Client.Instance.DirectMessagesViaCompanion)
            {
                PhimeStyle.Label(new Rect(x, inRect.y, inRect.xMax - x, inRect.height), "Phinix_inbox_viaCompanion".Translate(), PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>
        /// The right-click menu on a player in the list: add or remove them as a friend, and block or unblock them.
        /// Trading, messaging and gifts already have buttons in the conversation.
        /// </summary>
        private static void openPlayerMenu(string uuid)
        {
            bool friend = Client.Instance.IsFriend(uuid);
            bool blocked = Client.Instance.Settings.BlockedUsers.Contains(uuid);
            List<FloatMenuOption> items = new List<FloatMenuOption>
            {
                new FloatMenuOption(
                    (friend ? "Phinix_chat_contextMenu_removeFriend" : "Phinix_chat_contextMenu_addFriend").Translate(),
                    () =>
                    {
                        if (friend) Client.Instance.RemoveFriend(uuid);
                        else Client.Instance.AddFriend(uuid);
                    }),
                new FloatMenuOption(
                    (blocked ? "Phinix_chat_contextMenu_unblockUser" : "Phinix_chat_contextMenu_blockUser").Translate(),
                    () =>
                    {
                        if (blocked) Client.Instance.UnBlockUser(uuid);
                        else Client.Instance.BlockUser(uuid);
                    })
            };
            if (!Client.Instance.FriendsEnabled) items.RemoveAt(0);
            if (Client.Instance.FeatureEnabled(UserManagement.Features.PROFILES))
            {
                items.Insert(0, new FloatMenuOption("Phinix_chat_contextMenu_viewProfile".Translate(), () => Find.WindowStack.Add(new PlayerCardWindow(uuid))));
            }
            Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// Draws the open trades with one player, each with Open and Cancel buttons, on a gold-edged card.
        /// </summary>
        private static void drawTrades(Rect inRect, ImmutableTrade[] trades)
        {
            PhimeStyle.Card(inRect, PhimeStyle.GoldCardFill, PhimeStyle.GoldCardBorder);
            PhimeStyle.AccentEdge(inRect);
            Rect innerRect = inRect.ContractedBy(MESSAGE_PADDING);

            for (int i = 0; i < trades.Length; i++)
            {
                ImmutableTrade trade = trades[i];
                Rect rowRect = new Rect(innerRect.x, innerRect.y + i * TRADE_ROW_HEIGHT, innerRect.width, TRADE_ROW_HEIGHT);
                Rect cancelRect = new Rect(rowRect.xMax - TRADE_BUTTON_WIDTH, rowRect.y + (TRADE_ROW_HEIGHT - ROW_HEIGHT) / 2, TRADE_BUTTON_WIDTH, ROW_HEIGHT);
                Rect openRect = new Rect(cancelRect.x - SPACING - TRADE_BUTTON_WIDTH, cancelRect.y, TRADE_BUTTON_WIDTH, ROW_HEIGHT);
                Rect textRect = new Rect(rowRect.x + 8f, rowRect.y, openRect.x - SPACING - rowRect.x - 8f, TRADE_ROW_HEIGHT);

                // "Trade with Name" on top, and where it stands under it
                PhimeStyle.IconLabel(textRect.TopHalf(), PhimeStyle.TradeIcon,
                    "Phinix_trade_activeTrade_tradeWithLabel".Translate(TextHelper.StripRichText(trade.OtherPartyDisplayName)), Color.white, GameFont.Small);

                Rect statusRect = new Rect(textRect.x + 20f, textRect.center.y, textRect.width - 20f, textRect.height / 2);
                if (trade.OtherPartyAccepted)
                {
                    PhimeStyle.IconLabel(statusRect, Widgets.CheckboxOnTex, "Phinix_inbox_trade_yourTurn".Translate(), PhimeStyle.Green);
                }
                else
                {
                    string status = trade.Accepted ? "Phinix_inbox_trade_waitingForThem" : "Phinix_trade_activeTrade_theyHaveNotAccepted";
                    PhimeStyle.Label(statusRect, status.Translate(), PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleLeft);
                }

                if (PhimeStyle.Button(openRect, "Phinix_trade_activeTrade_openButton".Translate()))
                {
                    Find.WindowStack.Add(new TradeWindow(trade));
                }
                if (PhimeStyle.Button(cancelRect, "Phinix_trade_activeTrade_cancelButton".Translate()))
                {
                    Client.Instance.CancelTrade(trade.TradeId);
                }
            }
        }

        /// <summary>
        /// One line of the conversation: a message, a trade happening, or a day divider.
        /// </summary>
        private struct Line
        {
            public DateTime Time;
            public DirectMessage Message;
            public TradeEventLog.Kind? Event;
            public string DayLabel;
            public string Text;
            public float Height;

            /// <summary>
            /// Emojis in <see cref="Text"/>, in order (see <see cref="Emoji.Mark"/>).
            /// </summary>
            public List<string> Emojis;

            /// <summary>
            /// Space above the text, part of <see cref="Height"/>.
            /// </summary>
            public float Gap;
        }

        /// <summary>
        /// Draws one conversation in a scroll view, oldest at the top: messages and trade happenings in time order, with
        /// a divider whenever the day changes.
        /// </summary>
        private void drawConversation(Rect inRect, string rawName)
        {
            Widgets.DrawMenuSection(inRect);
            Rect innerRect = inRect.ContractedBy(MESSAGE_PADDING);
            string name = TextHelper.StripRichText(rawName);

            DirectMessage[] messages = Client.Instance.GetConversation(SelectedUuid);
            TradeEventLog.Entry[] events = TradeEventLog.For(SelectedUuid);
            if (messages.Length == 0 && events.Length == 0)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_dm_emptyConversation".Translate(name));
                return;
            }

            // Merge messages and events by time. Work out every height first, because the scroll view needs the total
            // before anything is drawn
            float width = innerRect.width - SCROLLBAR_WIDTH;
            List<Line> lines = messages
                .Select(message => new Line { Time = DateTimeOffset.FromUnixTimeSeconds(message.SentTime).ToLocalTime().DateTime, Message = message })
                .Concat(events.Select(entry => new Line { Time = entry.Time, Event = entry.Kind }))
                .OrderBy(line => line.Time)
                .ToList();

            Color theirColor = PhimeStyle.NameColor(rawName, PhimeStyle.Them);
            string theirName = PhimeStyle.Colored(name, theirColor);
            string yourName = PhimeStyle.Colored("Phinix_dm_you".Translate(), PhimeStyle.You);

            List<Line> laidOut = new List<Line>();
            DateTime? lastDay = null;
            string lastSender = null;
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Small;
            foreach (Line line in lines)
            {
                if (lastDay != line.Time.Date)
                {
                    laidOut.Add(new Line { DayLabel = dayLabel(line.Time), Height = DAY_SEPARATOR_HEIGHT });
                    lastDay = line.Time.Date;
                    lastSender = null;
                }

                Line placed = line;
                if (line.Message != null)
                {
                    // "19:02  Rook  hey there", time dimmed and the name in its colour
                    bool mine = line.Message.SenderUuid == Client.Instance.Uuid;
                    placed.Text = PhimeStyle.Colored(line.Time.ToString("HH:mm"), PhimeStyle.Dim) + "  " + (mine ? yourName : theirName) + "  " + ScreenshotSharing.ShowLinks(Emoji.Mark(TradeRequests.Readable(line.Message.Text), placed.Emojis = new List<string>()), colour: true);
                    placed.Height = Text.CalcHeight(Emoji.Placeholders(placed.Text, hidden: false), width) + MESSAGE_PADDING;
                    if (lastSender != null && lastSender != line.Message.SenderUuid) placed.Gap = SENDER_CHANGE_GAP;
                    placed.Height += placed.Gap;
                    lastSender = line.Message.SenderUuid;
                }
                else
                {
                    placed.Text = eventText(line.Event.Value, name);
                    placed.Height = EVENT_HEIGHT;
                }
                laidOut.Add(placed);
            }
            Text.Font = previousFont;

            Rect viewRect = new Rect(0, 0, width, laidOut.Sum(line => line.Height));

            // Jump to the bottom when the conversation changes or something new arrives
            if (SelectedUuid != lastSelectedUuid || laidOut.Count != lastLineCount)
            {
                messageScroll.y = viewRect.height;
                lastSelectedUuid = SelectedUuid;
                lastLineCount = laidOut.Count;
            }

            Widgets.BeginScrollView(innerRect, ref messageScroll, viewRect);
            float y = 0;
            foreach (Line line in laidOut)
            {
                Rect lineRect = new Rect(0, y, width, line.Height);
                if (line.DayLabel != null) drawDaySeparator(lineRect, line.DayLabel);
                else if (line.Message != null) drawMessage(lineRect, line);
                else drawEvent(lineRect, line);
                y += line.Height;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws one message, highlighted under the mouse. Clicking it (left or right) gives the same options as a message
        /// in the chat: open a link in it, or copy it. A left-click on a message with a screenshot opens the screenshot.
        /// </summary>
        private void drawMessage(Rect lineRect, Line line)
        {
            Rect textRect = new Rect(lineRect.x, lineRect.y + line.Gap, lineRect.width, lineRect.height - line.Gap);
            if (Mouse.IsOver(textRect)) Widgets.DrawHighlight(textRect);
            Emoji.Label(textRect, line.Text, line.Emojis);

            // A right-click is used up so it doesn't count again as a left-click below
            bool rightClicked = Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(textRect);
            if (rightClicked) Event.current.Use();
            if (rightClicked)
            {
                openMessageMenu(line.Message);
            }
            else if (Widgets.ButtonInvisible(textRect, false))
            {
                if (ScreenshotSharing.TryFindId(line.Message.Text, out string screenshotId)) ScreenshotSharing.Open(screenshotId);
                else openMessageMenu(line.Message);
            }
        }

        /// <summary>
        /// Finds web links in a message, the same pattern the chat uses (see ChatMessageList).
        /// </summary>
        private static readonly Regex urlRegex = new Regex(@"https?:\/\/\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// The menu the chat gives a message: "Open link in browser" for each link in it, "Copy message to clipboard", and
        /// "Report message" for the other player's messages.
        /// </summary>
        /// <param name="directMessage">The message as it was sent</param>
        private static void openMessageMenu(DirectMessage directMessage)
        {
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            string message = directMessage.Text;

            if (ScreenshotSharing.TryFindId(message, out string screenshotId))
            {
                items.Add(new FloatMenuOption("Phinix_screenshot_view".Translate(), () => ScreenshotSharing.Open(screenshotId)));
            }

            foreach (Match match in urlRegex.Matches(message.StripTags()))
            {
                // Only real web addresses; anything that doesn't parse as one is left out
                if (!Uri.TryCreate(match.Value, UriKind.Absolute, out Uri uri)) continue;

                string url = uri.ToString();
                string shortUrl = url.Length > 100 ? url.Substring(0, 100) + "..." : url;
                items.Add(new FloatMenuOption(string.Format("Phinix_chat_contextMenu_openInBrowser".Translate(), shortUrl), () => Application.OpenURL(url)));
            }

            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_copyToClipboard".Translate(), () => GUIUtility.systemCopyBuffer = message));

            if (directMessage.SenderUuid != Client.Instance.Uuid)
            {
                items.Add(new FloatMenuOption("Phinix_report_menuOption".Translate(), () => Find.WindowStack.Add(new ReportMessageWindow(MessageReports.ForDirectMessage(directMessage)))));
            }

            Find.WindowStack.Add(new FloatMenu(items));
        }

        /// <summary>
        /// "Today", "Yesterday", or the date.
        /// </summary>
        private static string dayLabel(DateTime time)
        {
            if (time.Date == DateTime.Today) return "Phinix_inbox_today".Translate();
            if (time.Date == DateTime.Today.AddDays(-1)) return "Phinix_inbox_yesterday".Translate();
            return time.ToString("D");
        }

        /// <summary>
        /// A faint line across the conversation with the day in the middle.
        /// </summary>
        private static void drawDaySeparator(Rect rect, string label)
        {
            float labelWidth = PhimeStyle.TextWidth(label, GameFont.Tiny) + 16f;
            float lineY = rect.y + rect.height / 2 - 2f;
            float sideWidth = (rect.width - labelWidth) / 2;
            Color lineColor = new Color(1f, 1f, 1f, 0.1f);
            Widgets.DrawBoxSolid(new Rect(rect.x + 4f, lineY, sideWidth - 4f, 1f), lineColor);
            Widgets.DrawBoxSolid(new Rect(rect.x + sideWidth + labelWidth, lineY, sideWidth - 4f, 1f), lineColor);
            PhimeStyle.Label(new Rect(rect.x + sideWidth, rect.y, labelWidth, rect.height - 4f), label, PhimeStyle.Muted, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static string eventText(TradeEventLog.Kind kind, string name)
        {
            switch (kind)
            {
                case TradeEventLog.Kind.Opened: return "Phinix_inbox_event_opened".Translate();
                case TradeEventLog.Kind.TheyAccepted: return "Phinix_inbox_event_theyAccepted".Translate(name);
                case TradeEventLog.Kind.YouAccepted: return "Phinix_inbox_event_youAccepted".Translate();
                case TradeEventLog.Kind.Completed: return "Phinix_inbox_event_completed".Translate();
                default: return "Phinix_inbox_event_cancelled".Translate();
            }
        }

        /// <summary>
        /// A small grey line about a trade, green for the good news, indented past the time column.
        /// </summary>
        private static void drawEvent(Rect rect, Line line)
        {
            bool good = line.Event == TradeEventLog.Kind.TheyAccepted || line.Event == TradeEventLog.Kind.YouAccepted || line.Event == TradeEventLog.Kind.Completed;
            Texture2D icon = good ? Widgets.CheckboxOnTex : PhimeStyle.TradeIcon;
            Color color = good ? PhimeStyle.Green : PhimeStyle.Muted;
            PhimeStyle.IconLabel(new Rect(rect.x + EVENT_INDENT, rect.y, rect.width - EVENT_INDENT, rect.height), icon, line.Text, color);
        }

        /// <summary>
        /// A player's display name as they set it (colours included). Falls back to the name on a trade with them,
        /// then "???".
        /// </summary>
        private static string rawNameOf(string uuid, ImmutableTrade[] trades)
        {
            if (Client.Instance.TryGetDisplayName(uuid, out string name)) return name;

            foreach (ImmutableTrade trade in trades)
            {
                if (trade.OtherPartyUuid == uuid) return trade.OtherPartyDisplayName;
            }
            return "???";
        }
    }
}
