using System.Collections.Generic;
using System.Linq;
using PhinixClient.GUI;
using RimWorld;
using Trading;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up window for putting up a new bounty: what you want on the left, what you'll pay on the right.
    /// </summary>
    public class PostBountyWindow : Window
    {
        private const float SPACING = 10f;
        private const float ROW_HEIGHT = 30f;
        private const float TITLE_HEIGHT = 35f;
        private const float POST_BUTTON_WIDTH = 140f;
        private const float RESET_BUTTON_WIDTH = 100f;
        private const float WANTED_COLUMN_WIDTH = 420f;
        /// <summary>Space under the reward list for the item count</summary>
        private const float COUNT_HEIGHT = 24f;
        /// <summary>Gap between the reward list's box and the list</summary>
        private const float LIST_PADDING = 10f;

        // Wider than the other windows, since the game's list needs about 530 pixels for its sort buttons and search box
        public override Vector2 InitialSize => new Vector2(1100f, 680f);

        /// <summary>
        /// Left column: what the poster wants. Any item, with a minimum quality.
        /// </summary>
        private readonly ItemPicker wantedPicker = new ItemPicker(ownedOnly: false, pickMinQuality: true);

        /// <summary>
        /// Right column: the reward. One row per kind of item the colony has, each remembering how many the player has
        /// picked, so they choose exactly which things go (a particular legendary rather than "a revolver").
        /// </summary>
        private List<TransferableOneWay> rewardRows;

        /// <summary>
        /// The game's caravan-window list that draws <see cref="rewardRows"/>, with its sort buttons and search box.
        /// </summary>
        private TransferableOneWayWidget rewardList;

        private string note = "";

        public PostBountyWindow()
        {
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shouldn't close it while typing, same as the listing window
            draggable = true;

            // The same item limit the server enforces
            wantedPicker.MaxEntries = Agora.ServerAgora.MAX_ITEMS_PER_BOUNTY;

            refreshRewardRows();
        }

        /// <summary>
        /// Lists the colony's items for the reward again from scratch, with nothing picked.
        /// </summary>
        private void refreshRewardRows()
        {
            rewardRows = ColonyTransferables.Items();
            rewardList = new TransferableOneWayWidget(rewardRows, null, null, "Phinix_picker_inColonyTip".Translate());
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TITLE_HEIGHT), "Phinix_bounty_postTitle".Translate());
            Text.Font = GameFont.Small;

            // Bottom strip: note box on the left, Reset and Post on the right
            Rect bottomRect = inRect.BottomPartPixels(ROW_HEIGHT);
            Rect postButtonRect = bottomRect.RightPartPixels(POST_BUTTON_WIDTH);
            Rect resetButtonRect = new Rect(postButtonRect.x - SPACING - RESET_BUTTON_WIDTH, bottomRect.y, RESET_BUTTON_WIDTH, ROW_HEIGHT);
            Rect noteRect = new Rect(bottomRect.x, bottomRect.y, resetButtonRect.x - SPACING - bottomRect.x, ROW_HEIGHT);
            Rect noteLabelRect = new Rect(inRect.x, bottomRect.y - ROW_HEIGHT, inRect.width, ROW_HEIGHT);

            // The wanted picker on the left, the reward list taking the rest
            float top = inRect.y + TITLE_HEIGHT + SPACING;
            float columnHeight = noteLabelRect.y - SPACING - top;
            Rect wantedRect = new Rect(inRect.x, top, WANTED_COLUMN_WIDTH, columnHeight);
            Rect rewardRect = new Rect(wantedRect.xMax + SPACING, top, inRect.xMax - wantedRect.xMax - SPACING, columnHeight);

            Widgets.Label(wantedRect.TopPartPixels(ROW_HEIGHT), "Phinix_bounty_wantLabel".Translate());
            wantedPicker.Draw(wantedRect.BottomPartPixels(wantedRect.height - ROW_HEIGHT));
            int rewardCount = drawReward(rewardRect);

            Widgets.Label(noteLabelRect, "Phinix_market_noteLabel".Translate());
            note = Widgets.TextField(noteRect, note);
            if (note.Length > Agora.ServerAgora.MAX_NOTE_LENGTH) note = note.Substring(0, Agora.ServerAgora.MAX_NOTE_LENGTH);

            if (Widgets.ButtonText(resetButtonRect, "ResetButton".Translate()))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                refreshRewardRows();
            }

            // Only clickable once both sides have something in them, and the reward isn't too big for the server
            bool canPost = wantedPicker.Entries.Count > 0 && rewardCount > 0 && rewardCount <= Agora.ServerAgora.MAX_ITEMS_PER_BOUNTY;
            if (Widgets.ButtonText(postButtonRect, "Phinix_bounty_postButton".Translate(), active: canPost) && canPost)
            {
                post();
            }
        }

        /// <summary>
        /// Draws the reward heading, the list in a box like the caravan window's, and how many items are picked.
        /// </summary>
        /// <returns>How many items the picked reward comes to (see <see cref="ColonyTransferables.ItemCount"/>)</returns>
        private int drawReward(Rect inRect)
        {
            Widgets.Label(inRect.TopPartPixels(ROW_HEIGHT), "Phinix_bounty_payLabel".Translate());

            // BeginGroup makes (0, 0) the box's corner, which is where the game's widget puts its sort buttons and search box
            Rect boxRect = new Rect(inRect.x, inRect.y + ROW_HEIGHT, inRect.width, inRect.height - ROW_HEIGHT - COUNT_HEIGHT);
            Widgets.DrawMenuSection(boxRect);
            Rect listRect = boxRect.ContractedBy(LIST_PADDING);
            Widgets.BeginGroup(listRect);
            rewardList.OnGUI(listRect.AtZero());
            Widgets.EndGroup();

            // "4 of 10 items · worth about 612 silver", in red when there are too many for the server to take
            int itemCount = ColonyTransferables.ItemCount(rewardRows);
            bool tooMany = itemCount > Agora.ServerAgora.MAX_ITEMS_PER_BOUNTY;
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = tooMany ? ColorLibrary.RedReadable : Color.gray;
            Rect countRect = new Rect(inRect.x, boxRect.yMax + 4f, inRect.width, COUNT_HEIGHT - 4f);
            Widgets.Label(countRect, "Phinix_gift_itemCount".Translate(itemCount, Agora.ServerAgora.MAX_ITEMS_PER_BOUNTY, Mathf.RoundToInt(ColonyTransferables.PickedValue(rewardRows))));
            if (tooMany) TooltipHandler.TipRegion(countRect, "Phinix_bounty_tooManyRewardItems".Translate(Agora.ServerAgora.MAX_ITEMS_PER_BOUNTY));
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;

            return itemCount;
        }

        /// <summary>
        /// Takes the reward off the map, sends the bounty to the server and closes the window.
        /// </summary>
        private void post()
        {
            // The reward has to physically leave the colony now, so it can't also be spent elsewhere. These are the
            // very things the poster picked
            List<TransferableOneWay> picked = rewardRows.Where(row => row.CountToTransfer > 0).ToList();
            if (!ColonyTransferables.TryTakeItems(picked, Client.Instance.BoardEscrowModule, out string token, out List<List<ProtoThing>> itemsPerRow))
            {
                // Something changed since the window opened (e.g. a pawn ate the reward). Nothing was taken, so list
                // what's there now
                Messages.Message("Phinix_bounty_notEnoughReward".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                refreshRewardRows();
                return;
            }
            List<ProtoThing> rewardItems = itemsPerRow.SelectMany(items => items).ToList();

            // Wanted items are just a description, not real things: a def name, an amount and the lowest quality.
            // Items that can't have a quality get Quality.None, which the server treats as "doesn't matter"
            IEnumerable<ProtoThing> wantedItems = wantedPicker.Entries.Select(entry => new ProtoThing
            {
                DefName = entry.Def.defName,
                StackCount = entry.Count,
                Quality = entry.Def.HasComp(typeof(CompQuality)) ? entry.MinQuality : Quality.None
            });

            // The server answers with the same token. Client.cs passes that answer to ItemEscrow.Resolve, which
            // either destroys the held reward (posted) or puts it back (refused)
            if (!Client.Instance.PostBounty(token, wantedItems, rewardItems, note))
            {
                // Couldn't even send it (we're not logged in), so put the reward straight back
                ItemEscrow.Resolve(token, false);
                return;
            }
            Close();
        }
    }
}
