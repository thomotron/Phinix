using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Helpers for showing and checking the items in player markets.
    /// </summary>
    public static class ShopItems
    {
        /// <summary>
        /// How much silver the colony could pay with right now: the silver in the same places trades and markets take
        /// items from (see <see cref="ColonyItems.TradeableItems"/>).
        /// </summary>
        public static int ColonySilver()
        {
            return ColonyItems.TradeableItems().Where(thing => thing.def == ThingDefOf.Silver).Sum(thing => thing.stackCount);
        }

        /// <summary>
        /// Whether a market item is an animal or colonist rather than an ordinary item.
        /// </summary>
        public static bool IsPawn(ProtoThing item) => item?.Animal != null || item?.Colonist != null;

        /// <summary>
        /// Whether this game knows everything an item needs: its def, its material and anything packed inside it.
        /// </summary>
        /// <remarks>
        /// An item from a mod we don't have would arrive as an "unknown item" (or not at all), so markets don't let us
        /// buy one. An animal or colonist needs its species or race to exist here, the same check the converters make.
        /// </remarks>
        public static bool IsIdentifiable(ProtoThing item)
        {
            if (item == null) return false;

            if (item.Animal != null)
            {
                ThingDef species = DefDatabase<ThingDef>.GetNamedSilentFail(item.Animal.DefName);
                return species?.race != null && species.race.Animal;
            }
            if (item.Colonist != null)
            {
                ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail(item.Colonist.DefName);
                return race?.race != null && race.race.Humanlike;
            }

            // GetNamedSilentFail returns null rather than logging an error when the def doesn't exist
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
            if (def == null || def.race != null) return false;

            if (!string.IsNullOrEmpty(item.StuffDefName) && DefDatabase<ThingDef>.GetNamedSilentFail(item.StuffDefName) == null) return false;
            if (item.InnerProtoThing != null && !IsIdentifiable(item.InnerProtoThing)) return false;

            return true;
        }

        /// <summary>
        /// An item's name as the player would see it, e.g. "Cloth duster (good, 85%)". Unidentified items show their
        /// def name.
        /// </summary>
        public static string Label(ProtoThing item)
        {
            // Animals and colonists: "Bessie (muffalo)", "Engie (human)"
            if (IsPawn(item)) return StackedThings.PawnLabel(item);

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
            if (def == null || !IsIdentifiable(item)) return "Phinix_shop_unidentified".Translate(item.DefName);

            ThingDef stuff = string.IsNullOrEmpty(item.StuffDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(item.StuffDefName);
            string label = GenLabel.ThingLabel(def, stuff).CapitalizeFirst();

            // Quality and wear go in brackets, like the game's own item labels
            List<string> details = new List<string>();
            if (item.Quality != Quality.None && def.HasComp(typeof(CompQuality))) details.Add(((QualityCategory) item.Quality).GetLabel());
            if (def.useHitPoints && item.HitPoints > 0)
            {
                // Stuffed items get their hit points from their material, so ask the stat rather than the def
                float maxHitPoints = def.GetStatValueAbstract(StatDefOf.MaxHitPoints, stuff);
                if (maxHitPoints > 0 && item.HitPoints < maxHitPoints) details.Add(((float) item.HitPoints / maxHitPoints).ToStringPercent());
            }

            return details.Count > 0 ? label + " (" + string.Join(", ", details) + ")" : label;
        }

        /// <summary>
        /// Draws an item's icon, or a question mark if this game doesn't know it.
        /// </summary>
        public static void DrawIcon(Rect rect, ProtoThing item)
        {
            if (IsPawn(item))
            {
                ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(StackedThings.PawnDefName(item));
                if (raceDef != null) Widgets.ThingIcon(rect, raceDef);
                else Widgets.Label(rect, "?");
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
            if (def == null || !IsIdentifiable(item))
            {
                TextAnchor previous = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "?");
                Text.Anchor = previous;
                return;
            }

            ThingDef stuff = string.IsNullOrEmpty(item.StuffDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(item.StuffDefName);
            Widgets.ThingIcon(rect, def, stuff);
        }
    }
}
