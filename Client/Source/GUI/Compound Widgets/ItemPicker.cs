using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    /// <summary>
    /// A search box, a list of matching items with Add buttons, and a list of added items with amounts.
    /// The bounty window uses two of these side by side: one for "I want" and one for "I'll pay".
    /// </summary>
    /// <remarks>
    /// A reusable widget, so one window can show two pickers with different rules. The two rules it can switch on are:
    /// <list type="bullet">
    /// <item><c>ownedOnly</c>: only list items the colony has, and cap each amount at what it has (for rewards).</item>
    /// <item><c>pickMinQuality</c>: show a quality button on items that have a quality (for wanted items).</item>
    /// </list>
    /// </remarks>
    public class ItemPicker
    {
        private const float ROW_HEIGHT = 30f;
        private const float SPACING = 6f;
        private const float SCROLLBAR_WIDTH = 16f;
        private const float COUNT_FIELD_WIDTH = 60f;
        private const float QUALITY_BUTTON_WIDTH = 90f;
        private const float ADD_BUTTON_WIDTH = 50f;

        /// <summary>
        /// Most search results to show at once, so typing one letter doesn't try to draw hundreds of rows.
        /// </summary>
        private const int MAX_SEARCH_RESULTS = 50;

        /// <summary>
        /// One item the player has added.
        /// </summary>
        public class Entry
        {
            public ThingDef Def;
            public int Count = 1;
            /// <summary>Text in the amount box while the player is typing. RimWorld's number box needs one</summary>
            public string CountBuffer = "1";
            /// <summary>Lowest quality accepted. Awful means any quality</summary>
            public Quality MinQuality = Quality.Awful;
        }

        /// <summary>
        /// Everything the player has added so far. The window reads this when they press Post.
        /// </summary>
        public readonly List<Entry> Entries = new List<Entry>();

        /// <summary>
        /// Most entries allowed.
        /// </summary>
        public int MaxEntries = 10;

        private readonly bool ownedOnly;
        private readonly bool pickMinQuality;

        /// <summary>
        /// How many of each item the colony has. Only filled in when <see cref="ownedOnly"/> is on.
        /// </summary>
        private readonly Dictionary<ThingDef, int> ownedCounts = new Dictionary<ThingDef, int>();

        /// <summary>
        /// Every item def that can go in a bounty, sorted by name. "static" means it's shared by every picker and
        /// only built once, the first time any picker is opened, since the list never changes during a game.
        /// </summary>
        private static List<ThingDef> allItemDefs;

        private string search = "";
        private Vector2 resultsScroll = Vector2.zero;
        private Vector2 entriesScroll = Vector2.zero;

        /// <summary>
        /// Creates a new picker.
        /// </summary>
        /// <param name="ownedOnly">Only offer items the colony has, capped at how many it has</param>
        /// <param name="pickMinQuality">Let the player set a minimum quality on each item</param>
        public ItemPicker(bool ownedOnly, bool pickMinQuality)
        {
            this.ownedOnly = ownedOnly;
            this.pickMinQuality = pickMinQuality;

            if (allItemDefs == null)
            {
                allItemDefs = DefDatabase<ThingDef>.AllDefs
                    .Where(def => def.category == ThingCategory.Item)
                    .Where(def => def.tradeability != Tradeability.None)
                    .Where(def => !def.IsCorpse && !def.isUnfinishedThing && def.minifiedDef == null)
                    .OrderBy(def => def.label)
                    .ToList();
            }

            if (ownedOnly)
            {
                // Count once when the window opens, rather than every frame
                foreach (Thing thing in ColonyItems.TradeableItems())
                {
                    ownedCounts.TryGetValue(thing.def, out int count);
                    ownedCounts[thing.def] = count + thing.stackCount;
                }
            }
        }

        /// <summary>
        /// Most of an item that can be added: what the colony has when owned-only, otherwise plenty.
        /// </summary>
        private int maxCount(ThingDef def)
        {
            if (!ownedOnly) return 99999;
            return ownedCounts.TryGetValue(def, out int owned) ? owned : 0;
        }

        /// <summary>
        /// Draws the whole picker: search box at the top, results under it, added items filling the rest.
        /// </summary>
        /// <param name="inRect">Container to draw within</param>
        public void Draw(Rect inRect)
        {
            Rect searchRect = new Rect(inRect.x, inRect.y, inRect.width, ROW_HEIGHT);
            float resultsHeight = (inRect.height - ROW_HEIGHT - SPACING * 2) * 0.45f;
            Rect resultsRect = new Rect(inRect.x, searchRect.yMax + SPACING, inRect.width, resultsHeight);
            Rect entriesRect = new Rect(inRect.x, resultsRect.yMax + SPACING, inRect.width, inRect.yMax - resultsRect.yMax - SPACING);

            search = Widgets.TextField(searchRect, search);
            drawResults(resultsRect);
            drawEntries(entriesRect);
        }

        /// <summary>
        /// Draws the items matching the search, each with an Add button.
        /// </summary>
        private void drawResults(Rect inRect)
        {
            Widgets.DrawMenuSection(inRect);

            if (search.Length < 2)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_market_typeToSearch".Translate());
                return;
            }

            List<ThingDef> results = allItemDefs
                .Where(def => !ownedOnly || maxCount(def) > 0)
                .Where(def => def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(MAX_SEARCH_RESULTS)
                .ToList();

            Rect viewRect = new Rect(0f, 0f, inRect.width - SCROLLBAR_WIDTH, ROW_HEIGHT * results.Count);
            Widgets.BeginScrollView(inRect, ref resultsScroll, viewRect);
            for (int i = 0; i < results.Count; i++)
            {
                ThingDef def = results[i];
                Rect rowRect = new Rect(0f, ROW_HEIGHT * i, viewRect.width, ROW_HEIGHT);
                if (i % 2 != 0) Widgets.DrawHighlight(rowRect);

                Widgets.ThingIcon(rowRect.LeftPartPixels(ROW_HEIGHT).ContractedBy(2f), def);

                string label = def.LabelCap;
                if (ownedOnly) label += " (" + "Phinix_market_owned".Translate(maxCount(def)) + ")";
                Rect labelRect = new Rect(rowRect.x + ROW_HEIGHT + 4f, rowRect.y, rowRect.width - ROW_HEIGHT - ADD_BUTTON_WIDTH - 8f, ROW_HEIGHT);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;

                bool canAdd = Entries.Count < MaxEntries && Entries.All(entry => entry.Def != def);
                if (Widgets.ButtonText(rowRect.RightPartPixels(ADD_BUTTON_WIDTH), "Phinix_market_addButton".Translate(), active: canAdd) && canAdd)
                {
                    Entries.Add(new Entry { Def = def });
                }
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws the added items, each with an amount box, an optional quality button and a remove button.
        /// </summary>
        private void drawEntries(Rect inRect)
        {
            Widgets.DrawMenuSection(inRect);

            if (Entries.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_market_nothingAdded".Translate());
                return;
            }

            Rect viewRect = new Rect(0f, 0f, inRect.width - SCROLLBAR_WIDTH, ROW_HEIGHT * Entries.Count);
            Widgets.BeginScrollView(inRect, ref entriesScroll, viewRect);

            // Remember which to remove and do it after the loop, since changing a list while looping over it breaks
            Entry toRemove = null;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                Rect rowRect = new Rect(0f, ROW_HEIGHT * i, viewRect.width, ROW_HEIGHT);
                if (i % 2 != 0) Widgets.DrawHighlight(rowRect);

                // Lay the row out right to left: remove button, amount box, quality button, then the name gets the rest
                Rect removeRect = rowRect.RightPartPixels(ROW_HEIGHT).ContractedBy(4f);
                Rect countRect = new Rect(removeRect.x - SPACING - COUNT_FIELD_WIDTH, rowRect.y + 2f, COUNT_FIELD_WIDTH, ROW_HEIGHT - 4f);
                bool hasQuality = pickMinQuality && entry.Def.HasComp(typeof(CompQuality));
                Rect qualityRect = new Rect(countRect.x - (hasQuality ? SPACING + QUALITY_BUTTON_WIDTH : 0f), rowRect.y + 2f, hasQuality ? QUALITY_BUTTON_WIDTH : 0f, ROW_HEIGHT - 4f);
                Rect labelRect = new Rect(rowRect.x + ROW_HEIGHT + 4f, rowRect.y, qualityRect.x - rowRect.x - ROW_HEIGHT - 8f, ROW_HEIGHT);

                Widgets.ThingIcon(rowRect.LeftPartPixels(ROW_HEIGHT).ContractedBy(2f), entry.Def);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, entry.Def.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                if (hasQuality && Widgets.ButtonText(qualityRect, QualityLabel(entry.MinQuality)))
                {
                    showQualityMenu(entry);
                }

                // "ref" lets the number box change Count and CountBuffer directly, like passing a mutable box in Python
                Widgets.TextFieldNumeric(countRect, ref entry.Count, ref entry.CountBuffer, 1, maxCount(entry.Def));

                if (Widgets.ButtonImage(removeRect, TexButton.Delete)) toRemove = entry;
            }
            Widgets.EndScrollView();

            if (toRemove != null) Entries.Remove(toRemove);
        }

        /// <summary>
        /// Pops up a menu of qualities to pick a minimum from.
        /// </summary>
        private static void showQualityMenu(Entry entry)
        {
            // One option per quality from Awful (0) to Legendary (6). Each option's action is a little lambda that
            // remembers which quality it was made for, a bit like functools.partial in Python
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int q = (int) Quality.Awful; q <= (int) Quality.Legendary; q++)
            {
                Quality quality = (Quality) q;
                options.Add(new FloatMenuOption(QualityLabel(quality), () => entry.MinQuality = quality));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// Text for a minimum quality: "Any quality" for Awful, otherwise e.g. "Good+".
        /// </summary>
        /// <param name="minimum">Lowest acceptable quality</param>
        /// <returns>Button text</returns>
        public static string QualityLabel(Quality minimum)
        {
            if (minimum == Quality.Awful || minimum == Quality.None) return "Phinix_bounty_anyQuality".Translate();
            return ((QualityCategory) minimum).GetLabel().CapitalizeFirst() + "+";
        }
    }
}
