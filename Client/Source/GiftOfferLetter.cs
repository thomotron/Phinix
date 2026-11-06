using System;
using System.Collections.Generic;
using System.Linq;
using Hermes;
using RimWorld;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// A letter for a gift from someone who isn't a friend, when the player has asked to see those first
    /// (<see cref="GiftsFromStrangers.AskFirst"/>). It lists what's in the gift, with buttons to take it or send it back.
    /// </summary>
    /// <remarks>
    /// Nothing is confirmed to the server until the player picks, so if they never do (or the game closes) the server
    /// offers the gift again next time they connect. The gift itself is only kept in memory, so a letter loaded from a
    /// save without it just says to wait for the offer to come back.
    /// <para>
    /// The player has <see cref="AnswerDays"/> from when it was sent to take it. The server sends it back to the
    /// sender a while after that (ServerHermes.UNANSWERED_RETURN_DAYS), so it can never be taken after it has gone.
    /// </para>
    /// </remarks>
    public class GiftOfferLetter : ChoiceLetter
    {
        /// <summary>
        /// Gifts waiting on an answer, by gift ID, with the connection to answer them on.
        /// </summary>
        private static readonly Dictionary<string, (ClientHermes giver, GiftDeliveryPacket gift)> waiting =
            new Dictionary<string, (ClientHermes, GiftDeliveryPacket)>();

        /// <summary>
        /// How many days after it was sent a gift can still be taken.
        /// </summary>
        public const int AnswerDays = 30;

        private string giftId;
        private string senderName;

        /// <summary>
        /// Shows a letter for a gift, or updates the one already showing if the server sent it again.
        /// Must run on the main thread, in a game.
        /// </summary>
        /// <param name="giver">The connection the gift came from</param>
        /// <param name="gift">The gift</param>
        /// <param name="senderName">Who it's from, as the player would see them</param>
        public static void Offer(ClientHermes giver, GiftDeliveryPacket gift, string senderName)
        {
            // Too late to take it: the server sends it back to them soon, so take down any letter still asking
            if (tooLate(gift))
            {
                waiting.Remove(gift.GiftId);
                foreach (GiftOfferLetter letter in Find.LetterStack.LettersListForReading.OfType<GiftOfferLetter>().Where(letter => letter.giftId == gift.GiftId).ToList())
                {
                    letter.remove();
                    Messages.Message("Phinix_gift_offer_tooLate".Translate(senderName), MessageTypeDefOf.NeutralEvent, historical: false);
                }
                return;
            }

            waiting[gift.GiftId] = (giver, gift);
            if (Find.LetterStack.LettersListForReading.OfType<GiftOfferLetter>().Any(letter => letter.giftId == gift.GiftId)) return;

            string items = string.Join("\n", gift.Items.Select(item => "  - " + (item.StackCount > 1 ? item.StackCount + "x " : "") + label(item)));
            string text = "Phinix_gift_offer_description".Translate(senderName, items);
            if (!string.IsNullOrWhiteSpace(gift.Note))
            {
                text += "\n\n" + "Phinix_gift_received_note".Translate(TextHelper.StripRichText(gift.Note));
            }
            string deadline = DateTimeOffset.FromUnixTimeSeconds(gift.SentTime).AddDays(AnswerDays).LocalDateTime.ToString("MMMM d");
            text += "\n\n" + "Phinix_gift_offer_deadline".Translate(deadline);

            GiftOfferLetter offer = (GiftOfferLetter)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("PhinixGiftOffer"));
            offer.Label = "Phinix_gift_offer_label".Translate(senderName);
            offer.Text = text;
            offer.giftId = gift.GiftId;
            offer.senderName = senderName;
            Find.LetterStack.ReceiveLetter(offer);
        }

        /// <summary>
        /// A gift still waiting on an answer has to be accepted or sent back, not just dismissed.
        /// </summary>
        public override bool CanDismissWithRightClick => !waiting.ContainsKey(giftId ?? "");

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) yield return Option_Close;
                else if (!waiting.ContainsKey(giftId ?? ""))
                {
                    // Loaded from a save: the server will offer it again when we next connect
                    yield return new DiaOption("Phinix_gift_offer_gone".Translate()) { action = remove, resolveTree = true };
                }
                else
                {
                    yield return new DiaOption("Phinix_gift_offer_accept".Translate()) { action = () => answer(true), resolveTree = true };
                    yield return new DiaOption("Phinix_gift_offer_decline".Translate()) { action = () => answer(false), resolveTree = true };
                    yield return Option_Postpone;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref giftId, "giftId");
            Scribe_Values.Look(ref senderName, "senderName");
        }

        private void answer(bool accept)
        {
            remove();
            if (!waiting.TryGetValue(giftId, out var offer)) return;
            waiting.Remove(giftId);

            // The letter sat too long: it's going back to the sender, so it can't be taken now
            if (accept && tooLate(offer.gift))
            {
                Messages.Message("Phinix_gift_offer_tooLate".Translate(senderName ?? ""), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (accept) Client.Instance.AcceptGift(offer.giver, offer.gift);
            else offer.giver.AckDeliveries(new[] { new DeliveryAck { DeliveryId = giftId, Refused = true } });
        }

        private void remove()
        {
            if (Find.LetterStack.LettersListForReading.Contains(this)) Find.LetterStack.RemoveLetter(this);
        }

        private static bool tooLate(GiftDeliveryPacket gift) =>
            DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeSeconds(gift.SentTime).AddDays(AnswerDays);

        private static string label(Trading.ProtoThing item)
        {
            try
            {
                return ShopItems.Label(item);
            }
            catch
            {
                // Something this game doesn't have; the def name still says what it is
                return item.DefName;
            }
        }
    }
}
