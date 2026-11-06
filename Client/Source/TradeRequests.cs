using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Asking the other player to take something out of their trade offer, shown as an alert on that thing in their
    /// trade window.
    /// </summary>
    /// <remarks>
    /// Requests travel as ordinary private messages, so the server doesn't need to know about them: a readable line
    /// ("Could you take 10 x steel out of the trade?") with a short tag on the end naming the trade, the thing and how
    /// many. This mod hides the tag and turns the message into an alert in the trade window. A player on an older
    /// mod just gets the message.
    /// <para>
    /// A thing is named by a short hash of what makes its row in the offer (its kind, material and quality, or for a
    /// colonist or animal, the pawn it was made from), since its place in the list can change.
    /// </para>
    /// </remarks>
    public static class TradeRequests
    {
        private static readonly Regex tagPattern = new Regex(@"\s*\[phinix-ask:([^:\]\s]+):([0-9a-f]{8}):(\d+)\]\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Requests the player has dealt with (taken back or ignored) this session, by message ID.
        /// </summary>
        private static readonly HashSet<string> dismissed = new HashSet<string>();

        /// <summary>
        /// A request to take something out of a trade.
        /// </summary>
        public class Request
        {
            public string MessageId;
            public string Key;
            public int Count;

            /// <summary>Whether the other player sent it (asking us), rather than us asking them</summary>
            public bool FromThem;
        }

        /// <summary>
        /// The key naming an offered thing's row: the same for things that stack together in the trade window.
        /// </summary>
        /// <param name="protoThing">One of the things in the row</param>
        /// <param name="foodCode">The row's <see cref="StackedThings.FoodCode"/>, which keeps rows of the same meal
        /// made from different kinds of food apart</param>
        public static string KeyOf(ProtoThing protoThing, string foodCode = "")
        {
            string identity = !string.IsNullOrEmpty(protoThing.OriginId)
                ? "pawn/" + protoThing.OriginId
                : protoThing.DefName + "/" + protoThing.StuffDefName + "/" + (int) protoThing.Quality;
            if (!string.IsNullOrEmpty(foodCode)) identity += "/" + foodCode;

            // FNV-1a, so the key is the same in every game
            uint hash = 2166136261;
            foreach (char c in identity)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }

        /// <summary>
        /// Sends a request for the other player to take <paramref name="count"/> of a thing out of their offer.
        /// </summary>
        /// <param name="tradeId">The trade</param>
        /// <param name="otherUuid">The other player</param>
        /// <param name="row">The row of their offer</param>
        /// <param name="count">How many to take out</param>
        /// <param name="label">The row's label, for the readable part</param>
        public static void Send(string tradeId, string otherUuid, StackedThings row, int count, string label)
        {
            string text = "Phinix_trade_askText".Translate(count, label) + " [phinix-ask:" + tradeId + ":" + KeyOf(row.OfferedProtos[0], row.FoodCode) + ":" + count + "]";
            Client.Instance.SendDirectMessage(otherUuid, text);
        }

        /// <summary>
        /// A private message's text with any request tag taken off, for showing it.
        /// </summary>
        public static string Readable(string text) => string.IsNullOrEmpty(text) ? text : tagPattern.Replace(text, "");

        /// <summary>
        /// The open requests on a trade, both ways: sent in our conversation with the other player, not yet dealt with.
        /// </summary>
        public static List<Request> For(string tradeId, string otherUuid)
        {
            List<Request> requests = new List<Request>();
            if (!Client.Instance.DirectMessagesEnabled || string.IsNullOrEmpty(otherUuid)) return requests;

            foreach (Hermes.DirectMessage message in Client.Instance.GetConversation(otherUuid))
            {
                if (dismissed.Contains(message.MessageId)) continue;

                Match match = tagPattern.Match(message.Text ?? "");
                if (!match.Success || match.Groups[1].Value != tradeId) continue;

                requests.Add(new Request
                {
                    MessageId = message.MessageId,
                    Key = match.Groups[2].Value,
                    Count = int.Parse(match.Groups[3].Value),
                    FromThem = message.SenderUuid == otherUuid
                });
            }
            return requests;
        }

        /// <summary>
        /// The open requests for a row of an offer.
        /// </summary>
        public static List<Request> ForRow(List<Request> requests, StackedThings row, bool fromThem)
        {
            if (requests.Count == 0 || row.OfferedProtos.Count == 0) return new List<Request>();
            HashSet<string> keys = new HashSet<string>(row.OfferedProtos.Select(proto => KeyOf(proto, row.FoodCode)));
            return requests.Where(request => request.FromThem == fromThem && keys.Contains(request.Key)).ToList();
        }

        /// <summary>
        /// Marks requests as dealt with, so they stop showing.
        /// </summary>
        public static void Dismiss(IEnumerable<Request> requests)
        {
            foreach (Request request in requests) dismissed.Add(request.MessageId);
        }
    }
}
