using System.Collections.Generic;
using System.Linq;
using Agora;
using PhinixClient.GUI;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up window for proposing a contract to another player: what you want each delivery on the left, what you'll
    /// pay each delivery on the right, and how many deliveries and how far apart along the bottom.
    /// </summary>
    /// <remarks>
    /// Proposing takes the WHOLE payment off the map straight away (one delivery's payment times the number of
    /// deliveries), so the supplier knows it's really there. Anything not spent comes back if the contract ends early.
    /// </remarks>
    public class ProposeContractWindow : Window
    {
        private const float SPACING = 10f;
        private const float ROW_HEIGHT = 30f;
        private const float TITLE_HEIGHT = 35f;
        private const float BUTTON_WIDTH = 160f;
        private const float NUMBER_LABEL_WIDTH = 150f;
        private const float NUMBER_FIELD_WIDTH = 60f;

        public override Vector2 InitialSize => new Vector2(900f, 720f);

        private readonly string supplierUuid;

        /// <summary>
        /// Left column: the goods. Any item, with a minimum quality, like a bounty's wanted items.
        /// </summary>
        private readonly ItemPicker goodsPicker = new ItemPicker(ownedOnly: false, pickMinQuality: true);

        /// <summary>
        /// Right column: one delivery's payment. Only items the colony has.
        /// </summary>
        private readonly ItemPicker paymentPicker = new ItemPicker(ownedOnly: true, pickMinQuality: false);

        private string note = "";

        // Numeric text fields in RimWorld need the number AND a text "buffer" holding what's been typed, so a
        // half-typed number like "" or "1" doesn't get snapped back while you're still typing
        private int deliveries = 5;
        private string deliveriesBuffer = "5";
        private int intervalHours = 24;
        private string intervalBuffer = "24";

        /// <summary>
        /// Whether the colony has enough for the whole payment. Re-checked once a second rather than every frame,
        /// because it counts the whole colony's items.
        /// </summary>
        private bool canAfford = false;
        private float lastAffordCheck = -999f;

        /// <param name="supplierUuid">UUID of the player being asked to supply</param>
        public ProposeContractWindow(string supplierUuid)
        {
            this.supplierUuid = supplierUuid;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shouldn't propose by accident while typing
            draggable = true;

            goodsPicker.MaxEntries = ServerAgora.MAX_ITEMS_PER_CONTRACT;
            paymentPicker.MaxEntries = ServerAgora.MAX_ITEMS_PER_CONTRACT;
        }

        public override void DoWindowContents(Rect inRect)
        {
            string supplierName = Client.Instance.TryGetDisplayName(supplierUuid, out string name) ? TextHelper.StripRichText(name) : "???";

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TITLE_HEIGHT), "Phinix_contract_proposeTitle".Translate(supplierName));
            Text.Font = GameFont.Small;

            // From the bottom up: [note ........][Propose], the note's label, the total payment line, the numbers row
            Rect bottomRect = inRect.BottomPartPixels(ROW_HEIGHT);
            Rect proposeButtonRect = bottomRect.RightPartPixels(BUTTON_WIDTH);
            Rect noteRect = new Rect(bottomRect.x, bottomRect.y, bottomRect.width - BUTTON_WIDTH - SPACING, ROW_HEIGHT);
            Rect noteLabelRect = new Rect(inRect.x, bottomRect.y - ROW_HEIGHT, inRect.width, ROW_HEIGHT);
            Rect totalRect = new Rect(inRect.x, noteLabelRect.y - ROW_HEIGHT, inRect.width, ROW_HEIGHT);
            Rect numbersRect = new Rect(inRect.x, totalRect.y - ROW_HEIGHT - SPACING, inRect.width, ROW_HEIGHT);

            float top = inRect.y + TITLE_HEIGHT + SPACING;
            float columnWidth = (inRect.width - SPACING) / 2;
            float columnHeight = numbersRect.y - SPACING - top;
            drawColumn(new Rect(inRect.x, top, columnWidth, columnHeight), "Phinix_contract_goodsColumn".Translate(), goodsPicker);
            drawColumn(new Rect(inRect.x + columnWidth + SPACING, top, columnWidth, columnHeight), "Phinix_contract_paymentColumn".Translate(), paymentPicker);

            drawNumbers(numbersRect);

            // "Total payment held now: 500x Silver". Red when the colony doesn't have that much
            if (Time.realtimeSinceStartup - lastAffordCheck > 1f)
            {
                lastAffordCheck = Time.realtimeSinceStartup;
                canAfford = paymentPicker.Entries.Count > 0 && ItemEscrow.CanTake(totalPaymentRequests());
            }
            UnityEngine.GUI.color = canAfford || paymentPicker.Entries.Count == 0 ? Color.white : ColorLibrary.RedReadable;
            Widgets.Label(totalRect, "Phinix_contract_totalPayment".Translate(describeTotal()));
            UnityEngine.GUI.color = Color.white;

            Widgets.Label(noteLabelRect, "Phinix_market_noteLabel".Translate());
            note = Widgets.TextField(noteRect, note);
            if (note.Length > ServerAgora.MAX_NOTE_LENGTH) note = note.Substring(0, ServerAgora.MAX_NOTE_LENGTH);

            bool canPropose = goodsPicker.Entries.Count > 0 && paymentPicker.Entries.Count > 0 && canAfford;
            if (Widgets.ButtonText(proposeButtonRect, "Phinix_contract_proposeButton".Translate(), active: canPropose) && canPropose)
            {
                propose();
            }
        }

        /// <summary>
        /// The "Deliveries: [5]   Hours between deliveries: [24]" row.
        /// </summary>
        private void drawNumbers(Rect inRect)
        {
            float x = inRect.x;
            Widgets.Label(new Rect(x, inRect.y, NUMBER_LABEL_WIDTH, ROW_HEIGHT), "Phinix_contract_deliveriesLabel".Translate());
            x += NUMBER_LABEL_WIDTH;
            // The min and max here match the server's limits, so the field can't hold a value the server would refuse
            Widgets.TextFieldNumeric(new Rect(x, inRect.y, NUMBER_FIELD_WIDTH, ROW_HEIGHT), ref deliveries, ref deliveriesBuffer, 1, ServerAgora.MAX_CONTRACT_DELIVERIES);
            x += NUMBER_FIELD_WIDTH + SPACING * 3;

            Widgets.Label(new Rect(x, inRect.y, NUMBER_LABEL_WIDTH + 50f, ROW_HEIGHT), "Phinix_contract_intervalLabel".Translate());
            x += NUMBER_LABEL_WIDTH + 50f;
            Widgets.TextFieldNumeric(new Rect(x, inRect.y, NUMBER_FIELD_WIDTH, ROW_HEIGHT), ref intervalHours, ref intervalBuffer, 1, ServerAgora.MAX_CONTRACT_INTERVAL_HOURS);
        }

        private static void drawColumn(Rect inRect, string heading, ItemPicker picker)
        {
            Widgets.Label(inRect.TopPartPixels(ROW_HEIGHT), heading);
            picker.Draw(inRect.BottomPartPixels(inRect.height - ROW_HEIGHT));
        }

        /// <summary>
        /// The whole payment as escrow requests: each payment line times the number of deliveries.
        /// </summary>
        private List<ItemEscrow.Request> totalPaymentRequests()
        {
            return paymentPicker.Entries.Select(entry => new ItemEscrow.Request
            {
                Def = entry.Def,
                Count = entry.Count * deliveries,
                MinQuality = Quality.Awful
            }).ToList();
        }

        private string describeTotal()
        {
            if (paymentPicker.Entries.Count == 0) return "-";
            return string.Join(", ", paymentPicker.Entries.Select(entry => (entry.Count * deliveries) + "x " + entry.Def.LabelCap));
        }

        /// <summary>
        /// Takes the whole payment off the map, sends the proposal and closes the window.
        /// </summary>
        private void propose()
        {
            if (!ItemEscrow.TryTake(totalPaymentRequests(), out string token, out List<ProtoThing> heldPayment, Client.Instance.BoardEscrowModule))
            {
                Messages.Message("Phinix_contract_cantAfford".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Goods are descriptions with a minimum quality (Quality.None for items that can't have one), like bounties
            IEnumerable<ProtoThing> goods = goodsPicker.Entries.Select(entry => new ProtoThing
            {
                DefName = entry.Def.defName,
                StackCount = entry.Count,
                Quality = entry.Def.HasComp(typeof(CompQuality)) ? entry.MinQuality : Quality.None
            });

            // One delivery's payment, as descriptions (def and amount only)
            IEnumerable<ProtoThing> paymentPerDelivery = paymentPicker.Entries.Select(entry => new ProtoThing
            {
                DefName = entry.Def.defName,
                StackCount = entry.Count,
                Quality = Quality.None
            });

            if (!Client.Instance.ProposeContract(token, supplierUuid, goods, paymentPerDelivery, heldPayment, deliveries, intervalHours, note))
            {
                // Couldn't even send it (we went offline), so put the payment straight back
                ItemEscrow.Resolve(token, false);
                return;
            }

            Close();
        }
    }
}
