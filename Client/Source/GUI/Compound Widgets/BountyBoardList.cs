using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;
using Bounty = Agora.Bounty;

namespace PhinixClient.GUI
{
    /// <summary>
    /// Scrollable list of every bounty, each with a Fulfil button (other people's) or a Cancel button (ours).
    /// </summary>
    public class BountyBoardList
    {
        private const float SCROLLBAR_WIDTH = 16f;
        private const float SPACING = 10f;
        /// <summary>
        /// Shortest a row can be, so the button always fits.
        /// </summary>
        private const float MIN_ROW_HEIGHT = 40f;
        private const float BUTTON_WIDTH = 80f;
        private const float BUTTON_HEIGHT = 30f;
        /// <summary>
        /// Space between cards, and padding inside each card. Matches the Market tab's cards.
        /// </summary>
        private const float CARD_GAP = 6f;
        private const float CARD_PADDING = 12f;

        /// <summary>
        /// How often to re-check which bounties we have the items for, in seconds.
        /// </summary>
        /// <remarks>
        /// Checking means counting items across the whole colony, which is too slow to do 60 times a second for
        /// every row. Once every couple of seconds is plenty, since stockpiles don't change that fast.
        /// </remarks>
        private const float FULFIL_CHECK_INTERVAL = 2f;

        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Which bounties we can fulfil right now, by bounty ID. Rebuilt every <see cref="FULFIL_CHECK_INTERVAL"/>.
        /// </summary>
        private Dictionary<string, bool> canFulfilCache = new Dictionary<string, bool>();
        private float lastFulfilCheck = -999f;

        /// <summary>
        /// Draws the board. Called every frame while the bounties tab is open.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public void Draw(Rect inRect)
        {
            Bounty[] bounties = Client.Instance.GetVisibleBounties();

            if (bounties.Length == 0)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_bounty_noBountiesPlaceholder".Translate());
                return;
            }

            // Time.realtimeSinceStartup is seconds since the game launched, and keeps counting while paused
            if (Time.realtimeSinceStartup - lastFulfilCheck > FULFIL_CHECK_INTERVAL)
            {
                lastFulfilCheck = Time.realtimeSinceStartup;
                canFulfilCache = bounties.ToDictionary(bounty => bounty.BountyId, bounty => ItemEscrow.CanTake(RequestsFor(bounty)));
            }

            // Rows can be different heights (a long note wraps onto more lines), so measure every row first.
            // The scroll view needs the total before anything is drawn
            // Each bounty is a card like the Market tab's, with ours gold-edged
            float rowWidth = inRect.width - SCROLLBAR_WIDTH;
            float textWidth = rowWidth - 2 * CARD_PADDING - BUTTON_WIDTH - SPACING;
            float[] heights = bounties.Select(bounty => Mathf.Max(MIN_ROW_HEIGHT, RowText.Measure(linesFor(bounty), textWidth) + 2 * SPACING)).ToArray();

            Rect viewRect = new Rect(0f, 0f, rowWidth, heights.Sum() + CARD_GAP * (bounties.Length - 1));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < bounties.Length; i++)
            {
                Rect cardRect = new Rect(0f, y, viewRect.width, heights[i]);
                if (bounties[i].PosterUuid == Client.Instance.Uuid)
                {
                    PhimeStyle.Card(cardRect, PhimeStyle.GoldCardFill, PhimeStyle.GoldCardBorder);
                    PhimeStyle.AccentEdge(cardRect);
                }
                else
                {
                    PhimeStyle.Card(cardRect, PhimeStyle.CardFill, PhimeStyle.CardBorder);
                }
                drawRow(new Rect(cardRect.x + CARD_PADDING, cardRect.y + SPACING / 2, cardRect.width - 2 * CARD_PADDING, cardRect.height - SPACING), bounties[i]);
                y += heights[i] + CARD_GAP;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws one bounty: who posted it, what it wants, what it pays, the note, time left and a button.
        /// </summary>
        private void drawRow(Rect rowRect, Bounty bounty)
        {
            Rect buttonRect = new Rect(rowRect.xMax - BUTTON_WIDTH, rowRect.center.y - BUTTON_HEIGHT / 2, BUTTON_WIDTH, BUTTON_HEIGHT);
            float textWidth = rowRect.width - BUTTON_WIDTH - SPACING;
            string posterName = nameOf(bounty.PosterUuid);

            RowText.Draw(linesFor(bounty), rowRect.x, rowRect.y + SPACING / 2, textWidth);

            if (bounty.PosterUuid == Client.Instance.Uuid)
            {
                // Ours: cancelling gets the reward back
                if (PhimeStyle.Button(buttonRect, "Phinix_bounty_cancelButton".Translate()))
                {
                    Client.Instance.CancelBounty(bounty.BountyId);
                }
            }
            else
            {
                // Someone else's: only clickable if we have everything it wants
                canFulfilCache.TryGetValue(bounty.BountyId, out bool canFulfil);
                if (PhimeStyle.Button(buttonRect, "Phinix_bounty_fulfilButton".Translate(), active: canFulfil))
                {
                    confirmFulfil(bounty, posterName);
                }

                // Hovering a greyed-out button explains why
                if (!canFulfil) TooltipHandler.TipRegion(buttonRect, "Phinix_bounty_cantFulfilTip".Translate());
            }
        }

        /// <summary>
        /// The text lines of one bounty's row, top to bottom. Used both to measure the row and to draw it.
        /// </summary>
        private static List<(string Text, bool Small)> linesFor(Bounty bounty)
        {
            List<(string Text, bool Small)> lines = new List<(string Text, bool Small)>
            {
                // "Bounty · SkyHawk"
                ("Phinix_bounty_titleLabel".Translate(nameOf(bounty.PosterUuid)), false),
                // "Wants: 200x Medicine, 1x Assault rifle (Good+)"
                ("Phinix_bounty_wantsLabel".Translate(DescribeItems(bounty.WantedItems, true)), false),
                // "Reward: 10x Gold, 1x Revolver (Excellent)"
                ("Phinix_bounty_rewardLabel".Translate(DescribeItems(bounty.RewardItems, false)), false)
            };

            // The note gets its own small line (wrapping as far as it needs), then the time left
            if (!string.IsNullOrEmpty(bounty.Note)) lines.Add(("\"" + bounty.Note + "\"", true));
            lines.Add((timeLeft(bounty.ExpiryTime), true));
            return lines;
        }

        private static string nameOf(string uuid)
        {
            return Client.Instance.TryGetDisplayName(uuid, out string name) ? TextHelper.StripRichText(name) : "???";
        }

        /// <summary>
        /// Asks "are you sure?" before handing anything over, since the items leave the colony straight away.
        /// </summary>
        private static void confirmFulfil(Bounty bounty, string posterName)
        {
            string text = "Phinix_bounty_confirmFulfil".Translate(posterName, DescribeItems(bounty.WantedItems, true), DescribeItems(bounty.RewardItems, false));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => fulfil(bounty)));
        }

        /// <summary>
        /// Takes the wanted items off the map and hands them to the server.
        /// </summary>
        private static void fulfil(Bounty bounty)
        {
            if (!ItemEscrow.TryTake(RequestsFor(bounty), out string token, out List<ProtoThing> items, Client.Instance.BoardEscrowModule))
            {
                // The items went missing between the button and the confirmation. Nothing was taken
                Messages.Message("Phinix_bounty_notEnoughItems".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Client.cs passes the server's answer to ItemEscrow.Resolve, which destroys the held items if it
            // worked or puts them back if it didn't (e.g. someone else got there first)
            // If the request couldn't even go out (we're not logged in), put the items straight back
            if (!Client.Instance.FulfilBounty(token, bounty.BountyId, items)) ItemEscrow.Resolve(token, false);
        }

        /// <summary>
        /// Turns a bounty's wanted items into requests for <see cref="ItemEscrow"/>.
        /// Wanted items whose def this game doesn't have (e.g. from a mod we don't have) can't be fulfilled.
        /// </summary>
        public static IEnumerable<ItemEscrow.Request> RequestsFor(Bounty bounty)
        {
            foreach (ProtoThing wanted in bounty.WantedItems)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(wanted.DefName);

                // A def of null with a count above zero can never be covered, so CanTake will say no
                yield return new ItemEscrow.Request { Def = def, Count = def == null ? int.MaxValue : wanted.StackCount, MinQuality = wanted.Quality };
            }
        }

        /// <summary>
        /// Turns items into one line like "200x Medicine, 1x Assault rifle (Good+)".
        /// </summary>
        /// <param name="items">Items to describe</param>
        /// <param name="isMinimum">True for wanted items (quality is a minimum, shown as "Good+"), false for real items</param>
        public static string DescribeItems(IEnumerable<ProtoThing> items, bool isMinimum)
        {
            return string.Join(", ", items.Select(item =>
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                string label = item.StackCount + "x " + (def != null ? def.LabelCap.ToString() : item.DefName);

                // Wanted items only mention quality when it's above "any"; real items mention whatever they have
                if (isMinimum && item.Quality != Quality.Awful && item.Quality != Quality.None)
                {
                    label += " (" + ItemPicker.QualityLabel(item.Quality) + ")";
                }
                else if (!isMinimum && item.Quality != Quality.None)
                {
                    label += " (" + ((QualityCategory) item.Quality).GetLabel().CapitalizeFirst() + ")";
                }
                return label;
            }));
        }

        /// <summary>
        /// Turns an expiry time into text like "3 d left".
        /// </summary>
        private static string timeLeft(long expiryUnixSeconds)
        {
            TimeSpan left = DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds) - DateTimeOffset.UtcNow;

            if (left.TotalMinutes < 1) return "Phinix_bounty_expiringNow".Translate();
            if (left.TotalHours < 1) return "Phinix_bounty_minutesLeft".Translate((int) left.TotalMinutes);
            if (left.TotalDays < 1) return "Phinix_bounty_hoursLeft".Translate((int) left.TotalHours);
            return "Phinix_bounty_daysLeft".Translate((int) left.TotalDays);
        }
    }
}
