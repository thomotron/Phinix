using System;
using System.Collections.Generic;
using System.Linq;
using Agora;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    /// <summary>
    /// The Contracts tab: every contract we're part of, with the buttons that make sense for our side and its state.
    /// </summary>
    /// <remarks>
    /// Which buttons show:
    /// <code>
    ///                 we're the buyer        we're the supplier
    ///   Proposed      Withdraw               Accept, Decline
    ///   Active        Cancel                 Deliver, Cancel
    /// </code>
    /// Laid out like <see cref="BountyBoardList"/>, so compare the two if anything here is unclear.
    /// </remarks>
    public class ContractList
    {
        private const float SCROLLBAR_WIDTH = 16f;
        private const float SPACING = 10f;
        /// <summary>
        /// Shortest a row can be, so two stacked buttons always fit.
        /// </summary>
        private const float MIN_ROW_HEIGHT = 72f;
        private const float BUTTON_WIDTH = 90f;
        private const float BUTTON_HEIGHT = 28f;
        private const float CARD_GAP = 6f;
        private const float CARD_PADDING = 12f;

        /// <summary>
        /// How often to re-check which deliveries we have the goods for, in seconds. Same reasoning as bounties:
        /// counting the whole colony's items every frame would be slow.
        /// </summary>
        private const float DELIVER_CHECK_INTERVAL = 2f;

        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<string, bool> canDeliverCache = new Dictionary<string, bool>();
        private float lastDeliverCheck = -999f;

        /// <summary>
        /// Draws the list. Called every frame while the Contracts tab is open.
        /// </summary>
        public void Draw(Rect inRect)
        {
            Contract[] contracts = Client.Instance.GetVisibleContracts();

            if (contracts.Length == 0)
            {
                Widgets.DrawMenuSection(inRect);
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_contract_noContracts".Translate());
                return;
            }

            if (Time.realtimeSinceStartup - lastDeliverCheck > DELIVER_CHECK_INTERVAL)
            {
                lastDeliverCheck = Time.realtimeSinceStartup;
                canDeliverCache = contracts.ToDictionary(contract => contract.ContractId, contract => ItemEscrow.CanTake(RequestsFor(contract.Goods)));
            }

            // Measure every row first (long notes make taller rows), since the scroll view needs the total height
            // Each contract is a card like the Market tab's. Ones waiting on us (a proposal to answer) are gold-edged
            float rowWidth = inRect.width - SCROLLBAR_WIDTH;
            float textWidth = rowWidth - 2 * CARD_PADDING - BUTTON_WIDTH - SPACING;
            float[] heights = contracts.Select(contract => Mathf.Max(MIN_ROW_HEIGHT, RowText.Measure(linesFor(contract), textWidth) + 2 * SPACING)).ToArray();

            Rect viewRect = new Rect(0f, 0f, rowWidth, heights.Sum() + CARD_GAP * (contracts.Length - 1));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < contracts.Length; i++)
            {
                Rect cardRect = new Rect(0f, y, viewRect.width, heights[i]);
                bool waitingOnUs = contracts[i].State == ContractState.Proposed && contracts[i].SupplierUuid == Client.Instance.Uuid;
                if (waitingOnUs)
                {
                    PhimeStyle.Card(cardRect, PhimeStyle.GoldCardFill, PhimeStyle.GoldCardBorder);
                    PhimeStyle.AccentEdge(cardRect);
                }
                else
                {
                    PhimeStyle.Card(cardRect, PhimeStyle.CardFill, PhimeStyle.CardBorder);
                }
                drawRow(new Rect(cardRect.x + CARD_PADDING, cardRect.y + SPACING / 2, cardRect.width - 2 * CARD_PADDING, cardRect.height - SPACING), contracts[i]);
                y += heights[i] + CARD_GAP;
            }
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws one contract: who it's with, the goods, the payment, its timing, and the buttons.
        /// </summary>
        private void drawRow(Rect rowRect, Contract contract)
        {
            bool weBuy = contract.BuyerUuid == Client.Instance.Uuid;
            string otherName = nameOf(weBuy ? contract.SupplierUuid : contract.BuyerUuid);

            // Two buttons stacked on the right
            Rect topButtonRect = new Rect(rowRect.xMax - BUTTON_WIDTH, rowRect.center.y - BUTTON_HEIGHT - 2f, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect bottomButtonRect = new Rect(rowRect.xMax - BUTTON_WIDTH, rowRect.center.y + 2f, BUTTON_WIDTH, BUTTON_HEIGHT);
            float textWidth = rowRect.width - BUTTON_WIDTH - SPACING;
            float y = rowRect.y + SPACING / 2;

            // The text lines (title, goods, payment, note, timing), wrapped to fit. See RowText.cs
            RowText.Draw(linesFor(contract), rowRect.x, y, textWidth);

            drawButtons(contract, weBuy, otherName, topButtonRect, bottomButtonRect);
        }

        /// <summary>
        /// The text lines of one contract's row, top to bottom. Used both to measure the row and to draw it.
        /// </summary>
        private static List<(string Text, bool Small)> linesFor(Contract contract)
        {
            bool weBuy = contract.BuyerUuid == Client.Instance.Uuid;
            string otherName = nameOf(weBuy ? contract.SupplierUuid : contract.BuyerUuid);

            // "Buying from SkyHawk · Delivery 2 of 5"   or   "Supplying SkyHawk · Proposed"
            string title = (weBuy ? "Phinix_contract_buyingFrom" : "Phinix_contract_supplying").Translate(otherName);
            string progress = contract.State == ContractState.Proposed
                ? "Phinix_contract_stateProposed".Translate().ToString()
                : "Phinix_contract_stateProgress".Translate(contract.DeliveriesMade + 1, contract.TotalDeliveries).ToString();

            List<(string Text, bool Small)> lines = new List<(string Text, bool Small)>
            {
                (title + " · " + progress, false),
                ("Phinix_contract_goodsLabel".Translate(BountyBoardList.DescribeItems(contract.Goods, true)), false),
                ("Phinix_contract_paymentLabel".Translate(BountyBoardList.DescribeItems(contract.PaymentPerDelivery, false)), false)
            };

            // The note gets its own small line (wrapping as far as it needs), then the timing
            if (!string.IsNullOrEmpty(contract.Note)) lines.Add(("\"" + contract.Note + "\"", true));
            lines.Add((timing(contract), true));
            return lines;
        }

        /// <summary>
        /// Draws the buttons for one contract (see the table at the top of this class).
        /// </summary>
        private void drawButtons(Contract contract, bool weBuy, string otherName, Rect topRect, Rect bottomRect)
        {
            if (contract.State == ContractState.Proposed)
            {
                if (weBuy)
                {
                    if (PhimeStyle.Button(topRect, "Phinix_contract_withdrawButton".Translate())) Client.Instance.CancelContract(contract.ContractId);
                }
                else
                {
                    if (PhimeStyle.Button(topRect, "Phinix_contract_acceptButton".Translate())) confirmAccept(contract, otherName);
                    if (PhimeStyle.Button(bottomRect, "Phinix_contract_declineButton".Translate())) Client.Instance.CancelContract(contract.ContractId);
                }
                return;
            }

            // Active
            if (!weBuy)
            {
                // Deliver is only clickable when the window is open AND we have the goods. The tooltip says which is missing
                bool windowOpen = DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ServerAgora.NextWindowOpens(contract);
                canDeliverCache.TryGetValue(contract.ContractId, out bool haveGoods);
                bool canDeliver = windowOpen && haveGoods;

                if (PhimeStyle.Button(topRect, "Phinix_contract_deliverButton".Translate(), active: canDeliver))
                {
                    deliver(contract);
                }
                if (!windowOpen) TooltipHandler.TipRegion(topRect, "Phinix_contract_notYetTip".Translate());
                else if (!haveGoods) TooltipHandler.TipRegion(topRect, "Phinix_contract_missingGoodsTip".Translate());
            }

            // Cancelling an active contract asks first, since it can't be undone
            Rect cancelRect = weBuy ? topRect : bottomRect;
            if (PhimeStyle.Button(cancelRect, "Phinix_contract_cancelButton".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Phinix_contract_confirmCancel".Translate(otherName),
                    () => Client.Instance.CancelContract(contract.ContractId),
                    destructive: true));
            }
        }

        /// <summary>
        /// Asks the supplier to confirm, spelling out the commitment, since missing a delivery ends the deal.
        /// </summary>
        private static void confirmAccept(Contract contract, string buyerName)
        {
            string text = "Phinix_contract_confirmAccept".Translate(
                buyerName,
                BountyBoardList.DescribeItems(contract.Goods, true),
                contract.TotalDeliveries,
                contract.IntervalHours,
                BountyBoardList.DescribeItems(contract.PaymentPerDelivery, false));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Client.Instance.AcceptContract(contract.ContractId)));
        }

        /// <summary>
        /// Takes one delivery's goods off the map and sends them.
        /// </summary>
        private static void deliver(Contract contract)
        {
            if (!ItemEscrow.TryTake(RequestsFor(contract.Goods), out string token, out List<ProtoThing> items, Client.Instance.BoardEscrowModule))
            {
                Messages.Message("Phinix_bounty_notEnoughItems".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // The server's answer goes to ItemEscrow.Resolve (see Client.cs). If the request couldn't even go out,
            // put the goods straight back
            if (!Client.Instance.DeliverContract(token, contract.ContractId, items)) ItemEscrow.Resolve(token, false);
        }

        /// <summary>
        /// The grey timing line, e.g. "Every 24h, 5 deliveries · next due in 3h 20m".
        /// </summary>
        private static string timing(Contract contract)
        {
            string schedule = "Phinix_contract_schedule".Translate(contract.IntervalHours, contract.TotalDeliveries);
            if (contract.State == ContractState.Proposed) return schedule;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long opens = ServerAgora.NextWindowOpens(contract);
            string next = now < opens
                ? "Phinix_contract_opensIn".Translate(FormatDuration(opens - now))
                : "Phinix_contract_dueIn".Translate(FormatDuration(ServerAgora.NextDeliveryDue(contract) - now));
            return schedule + " · " + next;
        }

        /// <summary>
        /// Turns a number of seconds into something short like "2d 4h", "3h 20m" or "45m".
        /// </summary>
        public static string FormatDuration(long seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (span.TotalDays >= 1) return string.Format("{0}d {1}h", (int) span.TotalDays, span.Hours);
            if (span.TotalHours >= 1) return string.Format("{0}h {1}m", (int) span.TotalHours, span.Minutes);
            return string.Format("{0}m", Math.Max(1, span.Minutes));
        }

        /// <summary>
        /// Turns wanted items into requests for <see cref="ItemEscrow"/>, the same way bounties do.
        /// </summary>
        public static IEnumerable<ItemEscrow.Request> RequestsFor(IEnumerable<ProtoThing> wantedItems)
        {
            foreach (ProtoThing wanted in wantedItems)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(wanted.DefName);
                yield return new ItemEscrow.Request { Def = def, Count = def == null ? int.MaxValue : wanted.StackCount, MinQuality = wanted.Quality };
            }
        }

        private static string nameOf(string uuid)
        {
            return Client.Instance.TryGetDisplayName(uuid, out string name) ? TextHelper.StripRichText(name) : "???";
        }
    }
}
