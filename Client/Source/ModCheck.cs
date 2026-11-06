using System.Collections.Generic;
using System.Linq;
using Trading;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Works out which mods a trade's items need that one side doesn't have, for the warnings in the trade window.
    /// </summary>
    /// <remarks>
    /// Two directions:
    /// - Their offer: items whose def this game doesn't have can't be built here, and are left behind when the trade
    ///   completes. Each item names the mods it comes from (<see cref="ProtoThing.Mods"/>), so we can say which ones.
    /// - Our offer: the server tells us the other player's mods (<see cref="Client.TryGetModList"/>), so we can say which
    ///   of our items need mods they don't have before they even look.
    /// Animals and colonists are included by their race's mod. Their individual parts (genes, implants...) are covered in
    /// more detail by <see cref="PawnCheck"/>.
    /// </remarks>
    public static class ModCheck
    {
        /// <summary>
        /// One missing mod and the items that need it.
        /// </summary>
        public class MissingMod
        {
            public string PackageId;
            public string Name;
            public List<string> Items = new List<string>();
        }

        private static HashSet<string> ownPackageIds;

        /// <summary>
        /// The lower-case package IDs of the mods we have loaded. The list can't change while the game runs.
        /// </summary>
        private static HashSet<string> OwnPackageIds => ownPackageIds ?? (ownPackageIds = new HashSet<string>(
            LoadedModManager.RunningMods.Where(mod => mod.PackageId != null).Select(mod => mod.PackageId.ToLowerInvariant())));

        /// <summary>
        /// Mods the other player's offer needs that we don't have, with the items that won't arrive because of them.
        /// </summary>
        /// <param name="theirOffer">Their items on offer</param>
        /// <returns>Missing mods, most items first. Empty if everything can be built here</returns>
        public static List<MissingMod> MissingFromTheirOffer(IEnumerable<ProtoThing> theirOffer)
        {
            Dictionary<string, MissingMod> missing = new Dictionary<string, MissingMod>();
            foreach (ProtoThing item in theirOffer)
            {
                if (canBuild(item)) continue;

                // An item from an older mod doesn't say where it's from
                List<ProtoMod> mods = allMods(item).Where(mod => !OwnPackageIds.Contains(mod.PackageId.ToLowerInvariant())).ToList();
                if (mods.Count == 0) mods.Add(new ProtoMod { PackageId = "", Name = "Phinix_modCheck_unknownMod".Translate() });

                foreach (ProtoMod mod in mods) add(missing, mod, item);
            }

            return sorted(missing);
        }

        /// <summary>
        /// Mods our offer needs that the other player doesn't have, with the items that won't reach them.
        /// </summary>
        /// <param name="ourOffer">Our items on offer</param>
        /// <param name="theirMods">Lower-case package IDs of their mods</param>
        /// <returns>Missing mods, most items first. Empty if they have everything</returns>
        public static List<MissingMod> MissingForThem(IEnumerable<ProtoThing> ourOffer, HashSet<string> theirMods)
        {
            Dictionary<string, MissingMod> missing = new Dictionary<string, MissingMod>();
            foreach (ProtoThing item in ourOffer)
            {
                foreach (ProtoMod mod in allMods(item).Where(mod => !theirMods.Contains(mod.PackageId.ToLowerInvariant())))
                {
                    add(missing, mod, item);
                }
            }

            return sorted(missing);
        }

        /// <summary>
        /// The missing mods as text for a confirmation box, like "Vanilla Weapons Expanded: 2x Hand cannon, Musket".
        /// </summary>
        /// <param name="missing">Missing mods</param>
        /// <returns>The text, or null if nothing is missing</returns>
        public static string Describe(List<MissingMod> missing)
        {
            if (missing.Count == 0) return null;
            return string.Join("\n", missing.Select(mod => mod.Name + ": " + string.Join(", ", mod.Items)));
        }

        /// <summary>
        /// Whether this game has the defs to build an item (and its material, and what's inside it if it's packed up).
        /// </summary>
        private static bool canBuild(ProtoThing item)
        {
            // Animals and colonists are rebuilt from their race, which PawnCheck reports on
            string defName = item.Animal != null ? item.Animal.DefName : item.Colonist != null ? item.Colonist.DefName : item.DefName;
            if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) == null) return false;
            if (!string.IsNullOrEmpty(item.StuffDefName) && DefDatabase<ThingDef>.GetNamedSilentFail(item.StuffDefName) == null) return false;
            return item.InnerProtoThing == null || canBuild(item.InnerProtoThing);
        }

        /// <summary>
        /// The mods an item and anything packed inside it come from.
        /// </summary>
        private static IEnumerable<ProtoMod> allMods(ProtoThing item)
        {
            IEnumerable<ProtoMod> mods = item.Mods.Where(mod => !string.IsNullOrEmpty(mod.PackageId));
            return item.InnerProtoThing == null ? mods : mods.Concat(allMods(item.InnerProtoThing));
        }

        private static void add(Dictionary<string, MissingMod> missing, ProtoMod mod, ProtoThing item)
        {
            string key = mod.PackageId.ToLowerInvariant();
            if (!missing.TryGetValue(key, out MissingMod entry))
            {
                entry = new MissingMod { PackageId = key, Name = string.IsNullOrEmpty(mod.Name) ? mod.PackageId : mod.Name };
                missing[key] = entry;
            }

            // "3x Plasteel hand cannon", or just the name for one
            string label = itemLabel(item);
            string line = item.StackCount > 1 ? item.StackCount + "x " + label : label;
            if (!entry.Items.Contains(line)) entry.Items.Add(line);
        }

        private static string itemLabel(ProtoThing item)
        {
            try
            {
                return ShopItems.Label(item);
            }
            catch
            {
                // A label for something we can't build can fail in odd ways; the def name still says what it is
                return item.DefName;
            }
        }

        private static List<MissingMod> sorted(Dictionary<string, MissingMod> missing) => missing.Values
            .OrderByDescending(mod => mod.Items.Count)
            .ThenBy(mod => mod.Name)
            .ToList();
    }
}
