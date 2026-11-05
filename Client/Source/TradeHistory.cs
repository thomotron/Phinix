using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Trading;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// A record of finished trades with each player: when, whether it went through, and what went each way. Kept in a
    /// file next to the game's settings so it lasts between sessions.
    /// </summary>
    /// <remarks>
    /// The server only says what we received when a trade completes, so what we gave is remembered from the trade's
    /// last update while it was open.
    /// </remarks>
    public static class TradeHistory
    {
        /// <summary>Most trades kept in the file, oldest dropped first</summary>
        private const int MAX_ENTRIES = 500;

        public class Line
        {
            public string Label;
            public int Count;

            /// <summary>What kind of item it is (see <see cref="ItemKey(ProtoThing)"/>), or empty for animals and colonists</summary>
            public string Key = "";

            /// <summary>Total value in silver, as priced by the game it came from (0 if unknown)</summary>
            public float Value;
        }

        public class Entry
        {
            public string TradeId;
            public DateTime Time;
            public string OtherUuid;
            public string OtherName;
            public bool Completed;

            /// <summary>What we gave, or for a cancelled trade, what came back</summary>
            public List<Line> Gave = new List<Line>();

            /// <summary>What we got (nothing for a cancelled trade)</summary>
            public List<Line> Got = new List<Line>();
        }

        private static readonly object historyLock = new object();
        private static List<Entry> entries;

        /// <summary>Our side of each open trade, as of its last update, by trade ID</summary>
        private static readonly Dictionary<string, ProtoThing[]> ourOffers = new Dictionary<string, ProtoThing[]>();

        private static string FilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "PhinixTradeHistory.xml");

        /// <summary>
        /// Starts listening to the client's trade events. Called once, from the <see cref="Client"/> constructor.
        /// </summary>
        public static void Subscribe(Client client)
        {
            client.OnTradeUpdateSuccess += (sender, args) => remember(args.Trade);
            client.OnTradesSynced += (sender, args) =>
            {
                foreach (ImmutableTrade trade in args.Trades) remember(trade);
            };
            client.OnTradeCompleted += (sender, args) => finish(client, args, true);
            client.OnTradeCancelled += (sender, args) => finish(client, args, false);
        }

        /// <summary>
        /// Every finished trade with a player, newest first.
        /// </summary>
        public static List<Entry> For(string otherUuid)
        {
            lock (historyLock)
            {
                load();
                return entries.Where(entry => entry.OtherUuid == otherUuid).Reverse().ToList();
            }
        }

        /// <summary>
        /// What we gave in our last completed trade with a player, or null if there isn't one.
        /// </summary>
        public static List<Line> LastGave(string otherUuid)
        {
            lock (historyLock)
            {
                load();
                return entries.LastOrDefault(entry => entry.OtherUuid == otherUuid && entry.Completed && entry.Gave.Count > 0)?.Gave;
            }
        }

        /// <summary>
        /// The kind of item a thing on offer is: its def, material and quality, the same for things that stack
        /// together. Empty for animals and colonists, which can't be offered again.
        /// </summary>
        public static string ItemKey(ProtoThing thing)
        {
            if (StackedThings.IsDisplayPawn(thing)) return "";
            return thing.DefName + "/" + thing.StuffDefName + "/" + (int) thing.Quality;
        }

        /// <summary>
        /// <see cref="ItemKey(ProtoThing)"/> for a thing in our colony.
        /// </summary>
        public static string ItemKey(Verse.Thing thing)
        {
            if (thing is Pawn) return "";
            Quality quality = thing.TryGetQuality(out QualityCategory category) ? (Quality) category : Quality.None;
            return thing.def.defName + "/" + (thing.Stuff?.defName ?? "") + "/" + (int) quality;
        }

        private static void remember(ImmutableTrade trade)
        {
            lock (historyLock) ourOffers[trade.TradeId] = trade.ItemsOnOffer.ToArray();
        }

        private static void finish(Client client, UICompleteTradeEventArgs args, bool completed)
        {
            ProtoThing[] ours;
            lock (historyLock)
            {
                if (ourOffers.TryGetValue(args.TradeId, out ours)) ourOffers.Remove(args.TradeId);
            }
            if (ours == null)
            {
                // Never saw it change: it may still be in the client's list
                ours = client.GetTrades().Where(trade => trade.TradeId == args.TradeId).Select(trade => trade.ItemsOnOffer.ToArray()).FirstOrDefault() ?? new ProtoThing[0];
            }

            string uuid = args.OtherPartyUuid;
            string name = client.TryGetDisplayName(uuid, out string displayName) ? TextHelper.StripRichText(displayName) : "???";
            ProtoThing[] got = completed ? args.Trade.OtherPartyItemsOnOffer.ToArray() : new ProtoThing[0];
            ProtoThing[] gave = completed ? ours : args.Items?.ToArray() ?? new ProtoThing[0];
            DateTime time = DateTime.Now;

            // Labels come from the game's defs, so they're worked out on the main thread
            client.RunOnMainThread(() =>
            {
                Entry entry = new Entry
                {
                    TradeId = args.TradeId,
                    Time = time,
                    OtherUuid = uuid,
                    OtherName = name,
                    Completed = completed,
                    Gave = lines(gave),
                    Got = lines(got)
                };

                lock (historyLock)
                {
                    load();

                    // The server sends a result again if our confirmation didn't reach it; it's only recorded once
                    if (entries.Any(existing => existing.TradeId == entry.TradeId)) return;
                    entries.Add(entry);
                    if (entries.Count > MAX_ENTRIES) entries.RemoveRange(0, entries.Count - MAX_ENTRIES);
                    save();
                }
            });
        }

        private static List<Line> lines(IEnumerable<ProtoThing> things)
        {
            Dictionary<string, Line> byLabel = new Dictionary<string, Line>();
            List<Line> result = new List<Line>();
            foreach (ProtoThing thing in things)
            {
                string key = ItemKey(thing);
                string label;
                try
                {
                    label = ShopItems.Label(thing);
                }
                catch
                {
                    label = thing.DefName;
                }

                int count = Math.Max(1, thing.StackCount);
                if (!byLabel.TryGetValue(label, out Line line))
                {
                    line = new Line { Label = label, Key = key };
                    byLabel[label] = line;
                    result.Add(line);
                }
                line.Count += count;
                line.Value += thing.MarketValue * count;
            }
            return result;
        }

        #region File

        private static void load()
        {
            if (entries != null) return;
            entries = new List<Entry>();

            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (XElement element in XDocument.Load(FilePath).Root.Elements("trade"))
                {
                    entries.Add(new Entry
                    {
                        TradeId = (string) element.Attribute("id"),
                        Time = DateTime.FromBinary(long.Parse((string) element.Attribute("time"))),
                        OtherUuid = (string) element.Attribute("with"),
                        OtherName = (string) element.Attribute("name"),
                        Completed = (bool) element.Attribute("completed"),
                        Gave = readLines(element.Element("gave")),
                        Got = readLines(element.Element("got"))
                    });
                }
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs("Couldn't read the trade history: " + e.Message, LogLevel.WARNING));
            }
        }

        private static List<Line> readLines(XElement parent)
        {
            if (parent == null) return new List<Line>();
            return parent.Elements("item").Select(item => new Line
            {
                Label = (string) item.Attribute("label"),
                Count = (int) item.Attribute("count"),
                Value = (float) item.Attribute("value"),
                Key = (string) item.Attribute("key") ?? ""
            }).ToList();
        }

        private static void save()
        {
            try
            {
                XElement root = new XElement("history", entries.Select(entry => new XElement("trade",
                    new XAttribute("id", entry.TradeId ?? ""),
                    new XAttribute("time", entry.Time.ToBinary()),
                    new XAttribute("with", entry.OtherUuid ?? ""),
                    new XAttribute("name", entry.OtherName ?? ""),
                    new XAttribute("completed", entry.Completed),
                    new XElement("gave", writeLines(entry.Gave)),
                    new XElement("got", writeLines(entry.Got)))));
                new XDocument(root).Save(FilePath);
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs("Couldn't save the trade history: " + e.Message, LogLevel.WARNING));
            }
        }

        private static IEnumerable<XElement> writeLines(List<Line> lines) => lines.Select(line => new XElement("item",
            new XAttribute("label", line.Label ?? ""),
            new XAttribute("count", line.Count),
            new XAttribute("value", line.Value),
            new XAttribute("key", line.Key ?? "")));

        #endregion
    }
}
