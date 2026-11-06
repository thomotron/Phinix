using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;
using Verse.Sound;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up window for sending items to another player as a gift, with an optional note.
    /// </summary>
    /// <remarks>
    /// The items are picked from a list of everything the colony can send, drawn by the game's own list from the
    /// caravan window (<see cref="TransferableOneWayWidget"/>). Like posting a bounty's reward, the items leave the map
    /// as soon as Send is pressed, wait in <see cref="ItemEscrow"/>, and are destroyed once the server confirms (or put
    /// back if it refuses).
    /// </remarks>
    public class SendGiftWindow : Window
    {
        private const float SPACING = 10f;
        private const float TITLE_HEIGHT = 35f;
        private const float SUBTITLE_HEIGHT = 22f;
        private const float FIELD_HEIGHT = 30f;
        private const float BUTTON_HEIGHT = 40f;
        private const float BUTTON_WIDTH = 100f;
        private const float SEND_BUTTON_WIDTH = 130f;
        /// <summary>Space under the list for the note's label, the note and the item count</summary>
        private const float BOTTOM_HEIGHT = 80f;
        /// <summary>Gap between the list's box and the list, the same as the caravan window</summary>
        private const float LIST_PADDING = 17f;

        public override Vector2 InitialSize => new Vector2(740f, 680f);

        /// <summary>
        /// Who the gift is for.
        /// </summary>
        private readonly string recipientUuid;

        /// <summary>
        /// One row per kind of item the colony has, each remembering how many the player has picked.
        /// </summary>
        private List<TransferableOneWay> rows;

        /// <summary>
        /// The game's list widget that draws <see cref="rows"/>, with its sort buttons and search box.
        /// </summary>
        private TransferableOneWayWidget list;

        private string note = "";

        /// <param name="recipientUuid">UUID of the player to send the gift to</param>
        public SendGiftWindow(string recipientUuid)
        {
            this.recipientUuid = recipientUuid;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shouldn't send it by accident while typing the note
            draggable = true;

            refreshRows();
        }

        /// <summary>
        /// Lists the colony's items again from scratch, with nothing picked.
        /// </summary>
        private void refreshRows()
        {
            rows = ColonyTransferables.Items();
            list = new TransferableOneWayWidget(rows, null, null, "Phinix_picker_inColonyTip".Translate());
        }

        public override void DoWindowContents(Rect inRect)
        {
            string recipientName = Client.Instance.TryGetDisplayName(recipientUuid, out string name) ? TextHelper.StripRichText(name) : "???";

            // Title, with a line under it
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TITLE_HEIGHT), "Phinix_gift_title".Translate(recipientName));
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + TITLE_HEIGHT, inRect.width, SUBTITLE_HEIGHT), "Phinix_gift_subtitle".Translate());
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // The list, in a box like the caravan window's. BeginGroup makes (0, 0) the box's corner, which is where the
            // game's widget puts its sort buttons and search box
            float top = inRect.y + TITLE_HEIGHT + SUBTITLE_HEIGHT + SPACING;
            Rect boxRect = new Rect(inRect.x, top, inRect.width, inRect.yMax - BOTTOM_HEIGHT - SPACING - top);
            Widgets.DrawMenuSection(boxRect);
            Rect listRect = boxRect.ContractedBy(LIST_PADDING);
            Widgets.BeginGroup(listRect);
            list.OnGUI(listRect.AtZero());
            Widgets.EndGroup();

            drawBottom(new Rect(inRect.x, inRect.yMax - BOTTOM_HEIGHT, inRect.width, BOTTOM_HEIGHT), recipientName);
        }

        /// <summary>
        /// Draws the note box and item count on the left, and Reset, Cancel and Send on the right.
        /// </summary>
        private void drawBottom(Rect inRect, string recipientName)
        {
            // Buttons right to left, lined up with the note box
            Rect sendRect = new Rect(inRect.xMax - SEND_BUTTON_WIDTH, inRect.y + 18f, SEND_BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect cancelRect = new Rect(sendRect.x - SPACING - BUTTON_WIDTH, sendRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect resetRect = new Rect(cancelRect.x - SPACING - BUTTON_WIDTH, sendRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            float leftWidth = resetRect.x - SPACING * 2 - inRect.x;

            Widgets.Label(new Rect(inRect.x, inRect.y, leftWidth, 22f), "Phinix_market_noteLabel".Translate());
            note = Widgets.TextField(new Rect(inRect.x, inRect.y + 22f, leftWidth, FIELD_HEIGHT), note);
            if (note.Length > Hermes.Hermes.MAX_NOTE_LENGTH) note = note.Substring(0, Hermes.Hermes.MAX_NOTE_LENGTH);

            // "4 of 10 items · worth about 612 silver", in red when there are too many for the server to take
            int itemCount = ColonyTransferables.ItemCount(rows);
            bool tooMany = itemCount > Hermes.Hermes.MAX_ITEMS_PER_GIFT;
            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = tooMany ? ColorLibrary.RedReadable : Color.gray;
            Rect countRect = new Rect(inRect.x, inRect.y + 22f + FIELD_HEIGHT + 4f, leftWidth, 20f);
            Widgets.Label(countRect, "Phinix_gift_itemCount".Translate(itemCount, Hermes.Hermes.MAX_ITEMS_PER_GIFT, Mathf.RoundToInt(ColonyTransferables.PickedValue(rows))));
            if (tooMany) TooltipHandler.TipRegion(countRect, "Phinix_gift_tooManyItems".Translate(Hermes.Hermes.MAX_ITEMS_PER_GIFT));
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(resetRect, "ResetButton".Translate()))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                refreshRows();
            }
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate())) Close();

            bool canSend = itemCount > 0 && !tooMany;
            if (Widgets.ButtonText(sendRect, "Phinix_gift_sendButton".Translate(), active: canSend) && canSend)
            {
                // If they're not online, check the player really means to send it now. The items leave straight away
                // but won't arrive until the recipient next logs in with an up-to-date mod
                bool recipientOnline = Client.Instance.TryGetUser(recipientUuid, out var recipient) && recipient.LoggedIn;
                if (recipientOnline)
                {
                    send();
                }
                else
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Phinix_gift_confirmOffline".Translate(recipientName), send));
                }
            }
        }

        /// <summary>
        /// Takes the picked items off the map, sends the gift to the server and closes the window.
        /// </summary>
        private void send()
        {
            List<TransferableOneWay> picked = rows.Where(row => row.CountToTransfer > 0).ToList();

            // GiftEscrowModule tells the escrow these went to the messaging module, so it knows who to ask if the
            // connection drops
            if (!ColonyTransferables.TryTakeItems(picked, Client.Instance.GiftEscrowModule, out string token, out List<List<ProtoThing>> itemsPerRow))
            {
                // Something changed since the window opened (e.g. a pawn used some of it). Nothing was taken, so list
                // what's there now
                Messages.Message("Phinix_gift_notEnough".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                refreshRows();
                return;
            }

            // If the request couldn't even go out (we went offline), put everything straight back
            if (!Client.Instance.SendGift(token, recipientUuid, itemsPerRow.SelectMany(items => items), note))
            {
                ItemEscrow.Resolve(token, false);
                return;
            }

            // Otherwise the server answers with the same token, and Client.cs hands it to ItemEscrow.Resolve
            Close();
        }
    }
}
