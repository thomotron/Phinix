using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Takes items off the map for a bounty or gift and holds them until the server says whether it accepted them.
    /// </summary>
    /// <remarks>
    /// Posting or fulfilling a bounty is a two-step conversation with the server:
    /// <list type="number">
    /// <item>We take the items off the map (so they can't be used twice) and send them along with a random "token".</item>
    /// <item>The server answers with the same token and either Success = true or false.</item>
    /// </list>
    /// In between, the items wait here, unspawned. When the answer comes back, <see cref="Resolve"/> either destroys
    /// them (the server has them now) or puts them back where they came from (the server said no).
    /// <para>
    /// The bounty windows, the gift window, contracts and the trade window all share it. The trade window used to keep
    /// its own copy of this idea in memory only, so items on their way into a trade could vanish if the connection
    /// dropped or the game closed before the server answered.
    /// </para>
    /// <para>
    /// If the connection drops before the answer comes, the items stay held (and are saved with the game). After the
    /// next login, Client.cs asks the server which tokens it accepted and calls <see cref="Reconcile"/>, so nothing is
    /// lost and nothing is duplicated.
    /// </para>
    /// </remarks>
    public static class ItemEscrow
    {
        /// <summary>
        /// One request for items: "this many of this item, at least this good".
        /// </summary>
        public struct Request
        {
            public ThingDef Def;
            public int Count;
            public Quality MinQuality;

            /// <summary>The material it must be made of, or null for any (markets sell one exact kind of item)</summary>
            public ThingDef Stuff;

            /// <summary>When true, the quality must be exactly <see cref="MinQuality"/> rather than at least it</summary>
            public bool ExactQuality;
        }

        /// <summary>
        /// Module name for items handed to the market board's module (bounties and contracts).
        /// </summary>
        public const string AGORA = "Agora";

        /// <summary>
        /// Module name for items handed to the messaging module (gifts).
        /// </summary>
        public const string HERMES = "Hermes";

        /// <summary>
        /// Module name for items put on offer in the trade window.
        /// </summary>
        public const string TRADING = "Trading";

        /// <summary>
        /// Everything currently waiting on the server. Lives in the save game (see <see cref="EscrowGameComponent"/>),
        /// so held items survive the player quitting or the connection dropping.
        /// </summary>
        /// <remarks>
        /// Only ever touched on the main game thread (network replies are handed over with RunOnMainThread first),
        /// so it doesn't need a lock.
        /// </remarks>
        private static List<HeldEntry> held => Current.Game.GetComponent<EscrowGameComponent>().Entries;

        /// <summary>
        /// Whether the colony has enough items to cover every request.
        /// </summary>
        /// <param name="requests">What's needed</param>
        /// <returns>Whether all of it is available</returns>
        public static bool CanTake(IEnumerable<Request> requests)
        {
            return requests.All(request => ColonyItems.TradeableItems().Where(thing => matches(thing, request)).Sum(thing => thing.stackCount) >= request.Count);
        }

        /// <summary>
        /// Whether a colony item can be used for a request: the right item, and the right material and quality if the
        /// request asks for them.
        /// </summary>
        private static bool matches(Thing thing, Request request)
        {
            if (thing.def != request.Def) return false;
            if (request.Stuff != null && thing.Stuff != request.Stuff) return false;

            if (request.ExactQuality)
            {
                // Items with no quality only match a request for "no quality"
                Quality quality = thing.TryGetQuality(out QualityCategory category) ? (Quality) category : Quality.None;
                return quality == request.MinQuality;
            }
            return ColonyItems.MeetsQuality(thing, request.MinQuality);
        }

        /// <summary>
        /// Takes items off the map to cover every request and holds them under a new token.
        /// Takes nothing at all if any request can't be covered.
        /// </summary>
        /// <param name="requests">What's needed</param>
        /// <param name="token">Token the items are held under, to send to the server</param>
        /// <param name="items">The taken items, already converted for sending</param>
        /// <param name="module">Which module the items are going to (<see cref="AGORA"/> or <see cref="HERMES"/>, plus
        /// "@" and the companion's address when they go to a companion server), so we know which server to ask about
        /// them if the connection drops</param>
        /// <returns>Whether the items were taken</returns>
        public static bool TryTake(IEnumerable<Request> requests, out string token, out List<ProtoThing> items, string module = AGORA)
        {
            token = null;
            items = null;

            // Check first, so we never end up taking half the items and then finding the rest are missing
            List<Request> requestList = requests.ToList();
            if (!CanTake(requestList)) return false;

            string newToken = Guid.NewGuid().ToString();
            List<HeldEntry> taken = new List<HeldEntry>();
            foreach (Request request in requestList)
            {
                int stillNeeded = request.Count;

                // Use the worst acceptable items first, so a bounty asking for "normal or better" doesn't eat the
                // player's legendary gear when a normal one would do. ToList() takes a snapshot, since SplitOff below
                // changes the map while we loop
                List<Thing> candidates = ColonyItems.TradeableItems()
                    .Where(thing => matches(thing, request))
                    .OrderBy(thing => thing.TryGetQuality(out QualityCategory quality) ? (int) quality : 0)
                    .ToList();

                foreach (Thing thing in candidates)
                {
                    if (stillNeeded <= 0) break;

                    // Remember where it was before it leaves the map. A despawned thing forgets its map and position
                    IntVec3 position = thing.Position;
                    Map map = thing.Map;

                    // SplitOff(n) takes n off the stack and gives them back as their own thing. If n is the whole stack
                    // it hands back the original thing, already removed from the map. Either way the result isn't on the map
                    Thing piece = thing.SplitOff(Math.Min(stillNeeded, thing.stackCount));
                    stillNeeded -= piece.stackCount;

                    taken.Add(new HeldEntry { Token = newToken, Module = module, Thing = piece, Position = position, Map = map });
                }
            }

            token = newToken;
            held.AddRange(taken);
            items = taken.Select(entry => TradingThingConverter.ConvertThingForSending(entry.Thing)).ToList();
            return true;
        }

        /// <summary>
        /// Holds things the caller has already taken off the map, under a new token. Used by the trade window, where
        /// the player picks exact things (a particular animal, a particular stack) rather than "50 steel".
        /// </summary>
        /// <param name="things">Things already removed from the map</param>
        /// <param name="maps">The map each thing came from, in the same order (a despawned thing forgets its map)</param>
        /// <param name="module">Module the things are going to</param>
        /// <param name="positions">Where each thing was, in the same order. Only needed for pieces split off a stack
        /// (<c>SplitOff</c>), which never had a position of their own; null uses each thing's last position</param>
        /// <returns>Token to send with the request</returns>
        public static string Hold(IList<Thing> things, IList<Map> maps, string module, IList<IntVec3> positions = null)
        {
            string token = Guid.NewGuid().ToString();
            for (int i = 0; i < things.Count; i++)
            {
                // A despawned thing still remembers its last position, which is where we'd put it back
                IntVec3 position = positions != null ? positions[i] : things[i].Position;
                held.Add(new HeldEntry { Token = token, Module = module, Thing = things[i], Position = position, Map = maps[i] });
            }
            return token;
        }

        /// <summary>
        /// Holds things the server is about to give back to us, under a new token. This is escrow the other way round:
        /// used when taking our items back off a trade offer.
        /// </summary>
        /// <remarks>
        /// If the server accepts the token it has removed the items from our offer, so these are put on the map. If it
        /// doesn't, they're still on offer over there, so ours are thrown away. Either way, if the connection drops
        /// first, the check after logging back in settles it the same way, so nothing is lost or doubled.
        /// </remarks>
        /// <param name="things">Things we'll get back (made from the server's copy, not on any map)</param>
        /// <param name="module">Module giving them back</param>
        /// <returns>Token to send with the request</returns>
        public static string HoldReturning(IList<Thing> things, string module)
        {
            string token = Guid.NewGuid().ToString();
            foreach (Thing thing in things)
            {
                // No map: putBack sends them to the colony's drop spot, where trades arrive
                held.Add(new HeldEntry { Token = token, Module = module, Thing = thing, Returning = true });
            }
            return token;
        }

        /// <summary>
        /// Settles a token where the server took the items but dropped the details of any animals or colonists among
        /// them (see Client.settleTradeOffer): pawns go back on the map, everything else is let go as usual.
        /// </summary>
        /// <param name="token">Token to settle</param>
        public static void ResolveKeepingPawns(string token)
        {
            if (string.IsNullOrEmpty(token) || Current.Game == null) return;

            List<HeldEntry> entries = held.Where(entry => entry.Token == token).ToList();
            held.RemoveAll(entry => entry.Token == token);

            foreach (HeldEntry entry in entries)
            {
                if (entry.Thing == null) continue;
                if (entry.Thing is Pawn) putBack(entry);
                else letGo(entry.Thing);
            }
        }

        /// <summary>
        /// Whether we're still holding anything under this token.
        /// </summary>
        public static bool IsHolding(string token) => Current.Game != null && held.Any(entry => entry.Token == token);

        /// <summary>
        /// Settles the items held under a token, once the server has answered.
        /// </summary>
        /// <param name="token">Token from the server's answer</param>
        /// <param name="success">True if the server kept the items, false if it refused them</param>
        public static void Resolve(string token, bool success)
        {
            if (string.IsNullOrEmpty(token) || Current.Game == null) return;

            // Pull out this token's entries. If there are none we already settled it (e.g. the reconnect check got
            // there first), so there's nothing to do
            List<HeldEntry> entries = held.Where(entry => entry.Token == token).ToList();
            if (entries.Count == 0) return;
            held.RemoveAll(entry => entry.Token == token);

            foreach (HeldEntry entry in entries)
            {
                if (entry.Thing == null) continue;

                // For returning items it's the other way round: accepted means they're ours again
                if (success != entry.Returning)
                {
                    // The server has them now, so ours are no longer needed. Note where a traded item sat, so if it
                    // comes back off the offer it goes back there rather than to the drop spot
                    if (!entry.Returning && entry.Module == TRADING) rememberOrigin(entry);
                    letGo(entry.Thing);
                }
                else
                {
                    putBack(entry);
                }
            }
        }

        /// <summary>
        /// Every token we're still holding items for that went to the given module. Asked about after logging in.
        /// </summary>
        public static List<string> HeldTokens(string module)
        {
            if (Current.Game == null) return new List<string>();
            return held.Where(entry => entry.Module == module).Select(entry => entry.Token).Distinct().ToList();
        }

        /// <summary>
        /// Settles held items using the server's answer to a token check (see Packets/EscrowPackets.proto).
        /// </summary>
        /// <remarks>
        /// Every token the server checked is either in its accepted list (it has the items, so destroy ours) or not
        /// (it never got them, so put ours back). Tokens it wasn't asked about are left alone.
        /// </remarks>
        /// <param name="checkedTokens">Tokens the server looked up</param>
        /// <param name="acceptedTokens">The ones it had accepted</param>
        public static void Reconcile(IEnumerable<string> checkedTokens, IEnumerable<string> acceptedTokens)
        {
            HashSet<string> accepted = new HashSet<string>(acceptedTokens);
            foreach (string token in checkedTokens) Resolve(token, accepted.Contains(token));
        }

        /// <summary>
        /// Gets rid of our copy of something the server now has.
        /// </summary>
        private static void letGo(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                // Pawns are kept for a while instead, so the very same pawn comes back if the trade is cancelled or the
                // market stock taken back (see ParkedPawns, which lets them go through the world later)
                ParkedPawns.Park(pawn);
            }
            else if (!thing.Destroyed)
            {
                thing.Destroy();
            }
        }

        /// <summary>
        /// Puts one held item back on the map, as close to where it came from as possible.
        /// </summary>
        /// <summary>Most remembered spots kept, oldest forgotten first</summary>
        private const int MAX_ORIGINS = 300;

        private static List<ItemOrigin> origins => Current.Game.GetComponent<EscrowGameComponent>().Origins;

        /// <summary>
        /// Remembers where an item put on a trade offer was taken from.
        /// </summary>
        private static void rememberOrigin(HeldEntry entry)
        {
            if (entry.Thing == null || entry.Thing is Pawn || entry.Map == null) return;
            origins.Add(new ItemOrigin { Key = TradeHistory.ItemKey(entry.Thing), Map = entry.Map, Position = entry.Position });
            if (origins.Count > MAX_ORIGINS) origins.RemoveRange(0, origins.Count - MAX_ORIGINS);
        }

        /// <summary>
        /// Where an item coming back off a trade offer was taken from, if we remember (newest first), forgetting it
        /// once used.
        /// </summary>
        private static bool takeOrigin(Thing thing, out Map map, out IntVec3 position)
        {
            map = null;
            position = IntVec3.Invalid;
            if (thing == null || thing is Pawn) return false;

            string key = TradeHistory.ItemKey(thing);
            for (int i = origins.Count - 1; i >= 0; i--)
            {
                ItemOrigin origin = origins[i];
                if (origin.Key != key) continue;
                origins.RemoveAt(i);
                if (origin.Map == null || !Find.Maps.Contains(origin.Map)) continue;

                map = origin.Map;
                position = origin.Position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Puts things coming back from a cancelled trade where they were taken from, as far as we remember.
        /// Must run on the main thread, in a game.
        /// </summary>
        /// <returns>The things that couldn't be put back that way, to be dropped in as usual</returns>
        public static Thing[] ReturnToOrigins(IEnumerable<Thing> things)
        {
            List<Thing> rest = new List<Thing>();
            if (Current.Game == null) return things.ToArray();

            foreach (Thing thing in things)
            {
                if (takeOrigin(thing, out Map map, out IntVec3 position) && GenPlace.TryPlaceThing(thing, position, map, ThingPlaceMode.Near))
                {
                    thing.SetForbidden(false, warnOnFail: false);
                    continue;
                }
                rest.Add(thing);
            }
            return rest.ToArray();
        }

        private static void putBack(HeldEntry entry)
        {
            // Something coming back off a trade offer goes back to where it was taken from, if we remember
            if (entry.Returning && entry.Map == null && takeOrigin(entry.Thing, out Map originMap, out IntVec3 originPosition))
            {
                entry.Map = originMap;
                entry.Position = originPosition;
            }

            // The original map might be gone since (e.g. a temporary map was abandoned while we were offline).
            // Fall back to the home colony's drop spot, the same place trades arrive
            Map map = entry.Map != null && Find.Maps.Contains(entry.Map) ? entry.Map : Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map == null)
            {
                // No map to put it on (only caravans left). Keep it until there is one, rather than losing it
                PendingDrops.DropOrKeep(new[] { entry.Thing });
                return;
            }
            IntVec3 position = map == entry.Map ? entry.Position : PendingDrops.DropSpot(map);

            // "Near" lets the game pick a free spot if the original cell has filled up since. If even that fails
            // (a completely packed area), send it in by drop pod instead so it's never lost
            if (!GenPlace.TryPlaceThing(entry.Thing, position, map, ThingPlaceMode.Near))
            {
                DropPodUtility.DropThingsNear(PendingDrops.DropSpot(map), map, new[] { entry.Thing }, canRoofPunch: false);
            }
        }
    }

    /// <summary>
    /// One held item: which token it's under, which module it went to, the item itself and where it came from.
    /// </summary>
    /// <remarks>
    /// <see cref="IExposable"/> is RimWorld's "this can be saved" interface. <see cref="ExposeData"/> is called both
    /// when saving and when loading, and each Scribe_ call either writes the field or reads it back, depending on
    /// which is happening. It's a bit like writing one function that does both json.dump and json.load.
    /// </remarks>
    public class HeldEntry : IExposable
    {
        public string Token;
        public string Module;
        public Thing Thing;
        public IntVec3 Position;
        public Map Map;
        /// <summary>
        /// True for items coming back to us rather than going out (see <see cref="ItemEscrow.HoldReturning"/>).
        /// </summary>
        public bool Returning;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Token, "token");
            Scribe_Values.Look(ref Module, "module");
            // "Deep" saves the whole item (its stack size, quality, hit points and so on) inside our entry, because
            // it isn't on any map, so nothing else would save it
            Scribe_Deep.Look(ref Thing, "thing");
            Scribe_Values.Look(ref Position, "position");
            // "References" saves just an ID for the map, since the map itself is saved by the game already
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref Returning, "returning", false);
        }
    }

    /// <summary>
    /// Keeps <see cref="ItemEscrow"/>'s held items inside the save file.
    /// </summary>
    /// <remarks>
    /// RimWorld creates one of every GameComponent subclass for each game automatically (new or loaded), so just
    /// defining this class is enough to hook it in. Each save has its own, so items held in one colony can never
    /// turn up in another.
    /// </remarks>
    public class EscrowGameComponent : GameComponent
    {
        public List<HeldEntry> Entries = new List<HeldEntry>();

        /// <summary>Where items now on trade offers were taken from (see ItemEscrow.rememberOrigin)</summary>
        public List<ItemOrigin> Origins = new List<ItemOrigin>();

        // RimWorld needs this exact constructor to create the component
        public EscrowGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Entries, "phinixHeldItems", LookMode.Deep);

            // Loading a save from before this existed gives null, so start an empty list instead
            if (Entries == null) Entries = new List<HeldEntry>();

            Scribe_Collections.Look(ref Origins, "phinixItemOrigins", LookMode.Deep);
            if (Origins == null) Origins = new List<ItemOrigin>();
            Origins.RemoveAll(origin => origin == null);
        }
    }

    /// <summary>
    /// Where an item put on a trade offer was taken from, so it can go back there if it's taken off again.
    /// </summary>
    public class ItemOrigin : IExposable
    {
        /// <summary>What kind of item (see TradeHistory.ItemKey)</summary>
        public string Key;
        public Map Map;
        public IntVec3 Position;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref Position, "position");
        }
    }
}
