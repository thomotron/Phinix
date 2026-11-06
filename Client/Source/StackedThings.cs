using System.Collections.Generic;
using System.Linq;
using Trading;
using UnityEngine;
using RimWorld;
using Verse;

namespace PhinixClient
{
    public class StackedThings
    {
        public List<Thing> Things;

        /// <summary>
        /// Set when this row only displays an animal or colonist on offer, without a real pawn behind it.
        /// Building a real pawn just to draw its name would mean generating a whole pawn every time the trade updates.
        /// </summary>
        private ProtoThing displayPawn;

        /// <summary>
        /// The animal or colonist this row displays, or null for a normal row of items. Used to open the pawn inspector.
        /// </summary>
        public ProtoThing DisplayPawn => displayPawn;

        // "=>" makes a read-only property that is recalculated each time it's read, a bit like Python's @property
        public int Count => displayPawn != null ? 1 : Things.Sum(thing => thing.stackCount);

        public string Label => displayPawn != null ? PawnLabel(displayPawn) : label ?? (label = labelOf(Things.First()));

        /// <summary>The row's label, worked out once</summary>
        private string label;

        /// <summary>
        /// The kind of food the row's things are (see <see cref="foodKindOf"/>), the same in every game and language,
        /// or "" for things that aren't meals. Rows of the same item are told apart by it.
        /// </summary>
        public string FoodCode => Things.Count > 0 ? foodCodeOf(Things[0]) : "";

        private static string foodCodeOf(Thing thing)
        {
            CompIngredients comp = thing.TryGetComp<CompIngredients>();
            if (comp?.ingredients == null || comp.ingredients.Count == 0) return "";

            bool meat = comp.ingredients.Any(def => def.IsMeat);
            bool veg = comp.ingredients.Any(def => !def.IsMeat);
            bool human = comp.ingredients.Any(def => def.ingestible?.sourceDef?.race?.Humanlike == true);
            bool insect = comp.ingredients.Any(def => def.ingestible?.sourceDef?.race?.FleshType == FleshTypeDefOf.Insectoid);
            return (meat ? "m" : "") + (veg ? "v" : "") + (human ? "h" : "") + (insect ? "i" : "");
        }

        /// <summary>
        /// What's in the row, for its tooltip, when there's something to tell apart: what each meal is made from, or
        /// how worn things are. "3 x cow meat, rice", "1 x 45% condition". Null when there's nothing to say.
        /// </summary>
        public string VariantsTip
        {
            get
            {
                if (variantsTipWorkedOut) return variantsTip;
                variantsTipWorkedOut = true;
                if (displayPawn != null || Things.Count == 0) return null;

                Dictionary<string, int> counts = new Dictionary<string, int>();
                List<string> order = new List<string>();
                foreach (Thing thing in Things)
                {
                    string variant = variantOf(thing);
                    if (variant.Length == 0) variant = "Phinix_trade_variantPlain".Translate();
                    if (!counts.ContainsKey(variant))
                    {
                        counts[variant] = 0;
                        order.Add(variant);
                    }
                    counts[variant] += thing.stackCount;
                }

                // Nothing worth saying if every thing is plain
                if (order.Count == 1 && variantOf(Things[0]).Length == 0) return null;
                variantsTip = string.Join("\n", order.Select(variant => counts[variant] + " x " + variant));
                return variantsTip;
            }
        }
        private string variantsTip;
        private bool variantsTipWorkedOut;

        /// <summary>
        /// How one thing differs from others of its kind: what it's made from and how worn it is, or "".
        /// </summary>
        private static string variantOf(Thing thing)
        {
            List<string> parts = new List<string>();
            CompIngredients comp = thing.TryGetComp<CompIngredients>();
            if (comp?.ingredients != null && comp.ingredients.Count > 0)
            {
                parts.Add(string.Join(", ", comp.ingredients.Select(def => def.label).Distinct().OrderBy(name => name)));
            }
            if (thing.def.useHitPoints && thing.MaxHitPoints > 0 && thing.HitPoints < thing.MaxHitPoints)
            {
                parts.Add("Phinix_trade_variantCondition".Translate(((float) thing.HitPoints / thing.MaxHitPoints).ToStringPercent()));
            }
            return string.Join("; ", parts);
        }

        /// <summary>
        /// A thing's label, with the kind of food it's made from for meals, where that matters to colonies
        /// (Ideology): "Simple meal (meat, insect meat)".
        /// </summary>
        private static string labelOf(Thing thing)
        {
            string kind = foodKindOf(thing);
            return kind.Length == 0 ? thing.LabelCapNoCount : thing.LabelCapNoCount + " (" + kind + ")";
        }

        /// <summary>
        /// What a meal is made from in the terms colonies care about: vegetarian, meat or mixed, plus human meat and
        /// insect meat when there's any. "" for things without ingredients.
        /// </summary>
        private static string foodKindOf(Thing thing)
        {
            CompIngredients comp = thing.TryGetComp<CompIngredients>();
            if (comp?.ingredients == null || comp.ingredients.Count == 0) return "";

            bool meat = comp.ingredients.Any(def => def.IsMeat);
            bool veg = comp.ingredients.Any(def => !def.IsMeat);
            bool human = comp.ingredients.Any(def => def.ingestible?.sourceDef?.race?.Humanlike == true);
            bool insect = comp.ingredients.Any(def => def.ingestible?.sourceDef?.race?.FleshType == FleshTypeDefOf.Insectoid);

            List<string> parts = new List<string>
            {
                (meat && veg ? "Phinix_trade_foodMixed" : meat ? "Phinix_trade_foodMeat" : "Phinix_trade_foodVegetarian").Translate()
            };
            if (human) parts.Add("Phinix_trade_foodHumanMeat".Translate());
            if (insect) parts.Add("Phinix_trade_foodInsectMeat".Translate());
            return string.Join(", ", parts);
        }
        public ThingDef ThingDef => displayPawn != null ? DefDatabase<ThingDef>.GetNamedSilentFail(PawnDefName(displayPawn)) : Things.First()?.def;
        // FirstOrDefault gives null on an empty list instead of throwing, which display-only rows need
        public ThingDef StuffDef => Things.FirstOrDefault()?.Stuff;
        public ThingStyleDef StyleDef => Things.FirstOrDefault()?.StyleDef;

        public int Selected = 0;

        /// <summary>
        /// What the whole row is worth in silver in our game, or 0 if we can't tell (a pawn on offer, an unknown item).
        /// Only set on rows in a trade's offers.
        /// </summary>
        public float YourValue;

        /// <summary>
        /// What the whole row is worth in silver in the sender's game, as they sent it, or 0 from older mods.
        /// Only set on rows in a trade's offers.
        /// </summary>
        public float TheirValue;

        /// <summary>
        /// The offered things this row was made from, as they are in the trade. Only set on rows in a trade's offers,
        /// so a row of our offer can be taken back.
        /// </summary>
        public List<ProtoThing> OfferedProtos = new List<ProtoThing>();

        public StackedThings(IEnumerable<Thing> things)
        {
            this.Things = things.ToList();
        }

        /// <summary>
        /// Whether the given offered thing is a pawn (an animal or colonist) that gets a display-only row.
        /// </summary>
        /// <param name="protoThing">Offered thing</param>
        /// <returns>Whether it is a pawn</returns>
        public static bool IsDisplayPawn(ProtoThing protoThing) => protoThing.Animal != null || protoThing.Colonist != null;

        /// <summary>
        /// Creates a display-only row for an animal or colonist on offer.
        /// </summary>
        /// <param name="protoThing">Pawn to display</param>
        /// <returns>Row with no real things in it</returns>
        public static StackedThings ForDisplayPawn(ProtoThing protoThing)
        {
            // "new X(...) { field = value }" sets fields right after construction, like keyword arguments
            return new StackedThings(Enumerable.Empty<Thing>()) { displayPawn = protoThing };
        }

        /// <summary>
        /// Gets the race def name of an animal or colonist on offer.
        /// </summary>
        /// <param name="protoThing">Pawn on offer</param>
        /// <returns>Race def name</returns>
        public static string PawnDefName(ProtoThing protoThing) => protoThing.Animal != null ? protoThing.Animal.DefName : protoThing.Colonist.DefName;

        /// <summary>
        /// Builds a label like "Bessie (muffalo)" for an animal, or "Engie (human)" for a colonist.
        /// </summary>
        /// <param name="protoThing">Pawn to label</param>
        /// <returns>Label text</returns>
        public static string PawnLabel(ProtoThing protoThing)
        {
            // Fall back to the raw def name if the receiver doesn't have this race (e.g. a missing mod)
            string defName = PawnDefName(protoThing);
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            string raceLabel = raceDef != null ? raceDef.label : defName;

            if (protoThing.Colonist != null)
            {
                ProtoColonist colonist = protoThing.Colonist;
                string colonistName = !string.IsNullOrEmpty(colonist.NickName) ? colonist.NickName : colonist.FirstName;

                // $"..." is C#'s version of a Python f-string
                return $"{colonistName} ({raceLabel})";
            }

            ProtoAnimal animal = protoThing.Animal;
            return string.IsNullOrEmpty(animal.CustomName)
                ? raceLabel.CapitalizeFirst()
                : $"{animal.CustomName} ({raceLabel})";
        }

        /// <summary>
        /// Returns whether the given thing can stack with all things in the stack.
        /// </summary>
        /// <param name="thing">Thing to check</param>
        /// <returns>Whether the given thing can stack with all things in the stack</returns>
        public bool CanStack(Thing thing)
        {
            // Things that come in stacks share a row when they look the same: same kind, material, quality and label,
            // and for meals, the same kind of food (vegetarian, meat, human meat...). The game itself keeps meals from
            // different animals or plants in separate piles, which would otherwise give a row per pile. Each thing
            // still goes over as itself
            if (Things.Count > 0 && thing.def.stackLimit > 1 && !(thing is Pawn))
            {
                Thing first = Things[0];
                return first.def == thing.def &&
                       first.Stuff == thing.Stuff &&
                       qualityOf(first) == qualityOf(thing) &&
                       foodKindOf(first) == foodKindOf(thing) &&
                       first.LabelCapNoCount == thing.LabelCapNoCount;
            }
            return Things.All(thing.CanStackWith);
        }

        private static QualityCategory? qualityOf(Thing thing) => thing.TryGetQuality(out QualityCategory quality) ? quality : (QualityCategory?) null;

        /// <summary>
        /// Gets the selected amount of things as their <see cref="ProtoThing"/> equivalents.
        /// Does some hacky-feeling stuff to get just the right amount of stacks set in stone as <see cref="ProtoThing"/>s.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtoThing> GetSelectedThingsAsProto()
        {
            List<Thing> thingsToConvert = new List<Thing>();
            Thing thingToModify = null;

            int remainingThings = Selected;
            foreach (Thing thing in Things)
            {
                // Check if we have collected all necessary things
                if (remainingThings == 0) break;

                // Check if this thing has more in its stack than we need to take
                if (thing.stackCount > remainingThings)
                {
                    // Set this as the thing to modify and stop
                    remainingThings = 0;
                    thingToModify = thing;
                }
                else
                {
                    // Subtract this thing's stack size from the remaining count and add it to the conversion list
                    remainingThings -= thing.stackCount;
                    thingsToConvert.Add(thing);
                }
            }

            // Convert the readily-convertible things
            List<ProtoThing> convertedThings = thingsToConvert.Select(TradingThingConverter.ConvertThingFromVerse).ToList();

            // Check if we need to modify a thing to get the right amount
            if (thingToModify != null)
            {
                // Get the target stack size and difference from the current stack size
                int targetAmount = Selected - thingsToConvert.Sum(thing => thing.stackCount);
                int actualAmount = thingToModify.stackCount;

                // Set the stack size to the target amount
                thingToModify.stackCount = targetAmount;

                // Convert and add the modified thing
                convertedThings.Add(TradingThingConverter.ConvertThingFromVerse(thingToModify));

                // Set the stack size to what it was before
                thingToModify.stackCount = actualAmount;
            }

            // Return the list of converted ProtoThings
            return convertedThings;
        }

        /// <summary>
        /// Removes the selected things from the stack and returns them.
        /// </summary>
        /// <param name="positions">Filled with where each returned thing was, in the same order. A piece split off a
        /// stack never had a position of its own, so it gets its stack's</param>
        /// <param name="maps">Filled with the map each returned thing was on, the same way</param>
        /// <returns>Selected things</returns>
        public IEnumerable<Thing> PopSelected(List<IntVec3> positions, List<Map> maps)
        {
            List<Thing> poppedThings = new List<Thing>();

            int remainingThings = Selected;
            foreach (Thing thing in Things)
            {
                // Check if we have popped all the necessary things, exiting the loop if so
                if (remainingThings == 0) break;

                // Remember where it is before any of it leaves the map
                positions.Add(thing.Position);
                maps.Add(thing.MapHeld);

                // Check if this thing has more in its stack than we need to take
                if (thing.stackCount > remainingThings)
                {
                    // Just split off the amount we need from this stack
                    poppedThings.Add(thing.SplitOff(remainingThings));
                    remainingThings = 0;
                }
                else
                {
                    // Subtract this thing's stack size from the remaining count and add it to the popped list
                    poppedThings.Add(thing);
                    remainingThings -= thing.stackCount;
                }
            }

            // Clear the selected counter now that everything has been split off
            Selected = 0;

            // Remove the things we popped
            foreach (Thing thing in poppedThings)
            {
                Things.Remove(thing);
            }

            return poppedThings;
        }

        /// <summary>
        /// Deletes the selected amount of things from the thing list.
        /// </summary>
        public void DeleteSelected()
        {
            // Set up a list to hold all things pending destruction
            List<Thing> thingsToDestroy = new List<Thing>();

            int remainingThings = Selected;
            foreach (Thing thing in Things)
            {
                // Check if we have deleted all the necessary things, exiting the loop if so
                if (remainingThings == 0) break;

                // Check if this thing has more in its stack than we need to take
                if (thing.stackCount > remainingThings)
                {
                    // Just take the amount we need from this stack
                    remainingThings = 0;
                    thing.stackCount -= remainingThings;
                }
                else
                {
                    // Subtract this thing's stack size from the remaining count and destroy it
                    remainingThings -= thing.stackCount;
                    thingsToDestroy.Add(thing);
                }
            }

            // Remove and destroy all things pending destruction
            foreach (Thing thing in thingsToDestroy)
            {
                // Remove this thing from the things list
                Things.Remove(thing);

                // Destroy it
                thing.Destroy();
            }
        }

        /// <summary>
        /// Groups the given collection of items by their def type and stackability.
        /// </summary>
        /// <param name="items">Items to group</param>
        /// <returns>Grouped items list</returns>
        public static List<StackedThings> GroupThings(IEnumerable<Thing> items)
        {
            // Set up an item dictionary
            Dictionary<string, List<StackedThings>> groupedItems = new Dictionary<string, List<StackedThings>>();

            foreach (Thing item in items)
            {
                // Check if this item type already has a group
                if (groupedItems.ContainsKey(item.def.defName))
                {
                    // Loop over all the item stacks in the group
                    bool stacked = false;
                    foreach (StackedThings itemStack in groupedItems[item.def.defName])
                    {
                        // Check if this item can stack on this stack
                        if (itemStack.CanStack(item))
                        {
                            // Increment this stack's item count by the item's stack count and break the loop
                            itemStack.Things.Add(item);
                            stacked = true;
                            break;
                        }
                    }

                    // Check if a stack wasn't found within this group
                    if (!stacked)
                    {
                        // Add a new stack with this item in it
                        groupedItems[item.def.defName].Add(new StackedThings(new[]{item}));
                    }
                }
                else
                {
                    // Create a new item stack with this item in it
                    StackedThings itemStack = new StackedThings(new[]{item});

                    // Add a new group with the item stack
                    groupedItems.Add(item.def.defName, new List<StackedThings>{itemStack});
                }
            }

            // Return the grouped items dictionary
            return groupedItems.SelectMany(pair => pair.Value).ToList();
        }
    }
}