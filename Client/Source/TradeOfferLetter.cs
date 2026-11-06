using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// The letter for a trade someone has sent, with a button to open the trade straight from it.
    /// </summary>
    public class TradeOfferLetter : ChoiceLetter
    {
        private string tradeId;

        /// <summary>
        /// Shows a letter for a new trade. Must run on the main thread, in a game.
        /// </summary>
        public static void Offer(string tradeId, string label, string text)
        {
            TradeOfferLetter letter = (TradeOfferLetter)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("TradeCreated"));
            letter.Label = label;
            letter.Text = text;
            letter.tradeId = tradeId;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!ArchivedOnly && !string.IsNullOrEmpty(tradeId))
                {
                    yield return new DiaOption("Phinix_trade_letter_open".Translate()) { action = open, resolveTree = true };
                }
                yield return Option_Close;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tradeId, "tradeId");
        }

        private void open()
        {
            ImmutableTrade[] matches = Client.Instance.Online
                ? Client.Instance.GetTrades().Where(t => t.TradeId == tradeId).ToArray()
                : new ImmutableTrade[0];
            if (matches.Length == 0)
            {
                Messages.Message("Phinix_trade_letter_gone".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Bring an open window for it to the front rather than opening a second one
            TradeWindow existing = Find.WindowStack.Windows.OfType<TradeWindow>().FirstOrDefault(window => window.TradeId == tradeId);
            if (existing != null) Find.WindowStack.Notify_ClickedInsideWindow(existing);
            else Find.WindowStack.Add(new TradeWindow(matches[0]));

            if (Find.LetterStack.LettersListForReading.Contains(this)) Find.LetterStack.RemoveLetter(this);
        }
    }
}
