using System.Collections.Generic;
using System.Linq;
using Trading;
using Verse;
using RimWorld;

namespace PhinixClient
{
    /// <summary>
    /// Finds the parts of an offered animal or colonist that this game doesn't have, usually because they come from a
    /// mod we don't have: a modded bionic arm, a trait, a gene, a backstory, an item in their pockets.
    /// </summary>
    /// <remarks>
    /// When the pawn arrives those parts are simply left out (see <see cref="ColonistConverter"/>,
    /// <see cref="AnimalConverter"/> and <see cref="HediffConverter"/>, which skip anything they can't find). This class
    /// lets the trade window, the markets and the pawn inspector warn the player before they agree to it, and lets the
    /// arrival message say what was left behind.
    /// </remarks>
    public static class PawnCheck
    {
        /// <summary>
        /// Everything about a pawn this game doesn't have, each as a short line like "Trait: Bloodlust2". Empty for a
        /// pawn that would arrive complete, or for something that isn't a pawn.
        /// </summary>
        public static List<string> MissingParts(ProtoThing thing)
        {
            List<string> missing = new List<string>();
            if (thing?.Colonist != null) checkColonist(thing.Colonist, missing);
            else if (thing?.Animal != null) checkAnimal(thing.Animal, missing);
            return missing;
        }

        /// <summary>
        /// The missing parts as one block of text for a tooltip or dialog, or null if nothing is missing.
        /// </summary>
        public static string Describe(ProtoThing thing)
        {
            List<string> missing = MissingParts(thing);
            return missing.Count == 0 ? null : string.Join("\n", missing.Select(line => "  - " + line));
        }

        /// <summary>
        /// One line per offered pawn with missing parts, for a "are you sure?" dialog. Null if there are none.
        /// </summary>
        public static string DescribeAll(IEnumerable<ProtoThing> things)
        {
            List<string> blocks = things
                .Where(thing => thing.Animal != null || thing.Colonist != null)
                .Select(thing => (Label: StackedThings.PawnLabel(thing), Missing: Describe(thing)))
                .Where(pawn => pawn.Missing != null)
                .Select(pawn => pawn.Label + ":\n" + pawn.Missing)
                .ToList();
            return blocks.Count == 0 ? null : string.Join("\n\n", blocks);
        }

        private static void checkColonist(ProtoColonist colonist, List<string> missing)
        {
            ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail(colonist.DefName);
            if (race?.race == null || !race.race.Humanlike)
            {
                // Without the race nothing else matters: the whole pawn can't be made
                missing.Add("Phinix_pawnCheck_race".Translate(colonist.DefName));
                return;
            }

            if (!string.IsNullOrEmpty(colonist.ChildhoodDefName) && DefDatabase<BackstoryDef>.GetNamedSilentFail(colonist.ChildhoodDefName) == null)
                missing.Add("Phinix_pawnCheck_backstory".Translate(colonist.ChildhoodDefName));
            if (!string.IsNullOrEmpty(colonist.AdulthoodDefName) && DefDatabase<BackstoryDef>.GetNamedSilentFail(colonist.AdulthoodDefName) == null)
                missing.Add("Phinix_pawnCheck_backstory".Translate(colonist.AdulthoodDefName));

            foreach (ProtoTrait trait in colonist.Traits.Where(t => DefDatabase<TraitDef>.GetNamedSilentFail(t.DefName) == null))
                missing.Add("Phinix_pawnCheck_trait".Translate(trait.DefName));

            foreach (ProtoSkill skill in colonist.Skills.Where(s => DefDatabase<SkillDef>.GetNamedSilentFail(s.DefName) == null))
                missing.Add("Phinix_pawnCheck_skill".Translate(skill.DefName));

            checkHediffs(race, colonist.Hediffs, missing);

            // A custom xenotype has no def, so only a named one can be missing
            if (!string.IsNullOrEmpty(colonist.XenotypeDefName) && DefDatabase<XenotypeDef>.GetNamedSilentFail(colonist.XenotypeDefName) == null)
                missing.Add("Phinix_pawnCheck_xenotype".Translate(colonist.XenotypeDefName));
            foreach (string gene in colonist.Endogenes.Concat(colonist.Xenogenes).Distinct().Where(g => DefDatabase<GeneDef>.GetNamedSilentFail(g) == null))
                missing.Add("Phinix_pawnCheck_gene".Translate(gene));

            foreach (ProtoThing item in colonist.Apparel.Concat(colonist.Equipment).Concat(colonist.Inventory).Where(item => !ShopItems.IsIdentifiable(item)))
                missing.Add("Phinix_pawnCheck_item".Translate(item.DefName));

            // Titles travel as honor, which needs the same faction in this world (and Royalty)
            foreach (ProtoHonor honor in colonist.Honor)
            {
                FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(honor.FactionDefName);
                bool inThisWorld = ModsConfig.RoyaltyActive && factionDef != null && Current.Game != null && Find.FactionManager.FirstFactionOfDef(factionDef) != null;
                if (!inThisWorld) missing.Add("Phinix_pawnCheck_honor".Translate(honor.TotalHonor, factionDef?.LabelCap.ToString() ?? honor.FactionDefName));
            }

            PsycastConverter.CheckPsycasts(colonist, missing);

            ProtoAppearance looks = colonist.Appearance;
            if (looks != null)
            {
                checkLooks<BodyTypeDef>(looks.BodyTypeDefName, missing);
                checkLooks<HeadTypeDef>(looks.HeadTypeDefName, missing);
                checkLooks<HairDef>(looks.HairDefName, missing);
                checkLooks<BeardDef>(looks.BeardDefName, missing);
                checkLooks<TattooDef>(looks.FaceTattooDefName, missing);
                checkLooks<TattooDef>(looks.BodyTattooDefName, missing);
                checkLooks<FurDef>(looks.FurDefName, missing);
            }
        }

        private static void checkAnimal(ProtoAnimal animal, List<string> missing)
        {
            ThingDef species = DefDatabase<ThingDef>.GetNamedSilentFail(animal.DefName);
            if (species?.race == null || !species.race.Animal)
            {
                missing.Add("Phinix_pawnCheck_species".Translate(animal.DefName));
                return;
            }

            foreach (string trick in animal.TrainingSteps.Keys.Where(t => DefDatabase<TrainableDef>.GetNamedSilentFail(t) == null))
                missing.Add("Phinix_pawnCheck_training".Translate(trick));

            checkHediffs(species, animal.Hediffs, missing);
        }

        /// <summary>
        /// Health conditions we don't have, and ones on body parts this race doesn't have here (e.g. a modded body).
        /// </summary>
        private static void checkHediffs(ThingDef race, IEnumerable<ProtoHediff> hediffs, List<string> missing)
        {
            foreach (ProtoHediff hediff in hediffs)
            {
                // Checked with the rest of the psycasts instead
                if (PsycastConverter.RestoredSeparately(hediff.DefName)) continue;

                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediff.DefName);
                if (hediffDef == null)
                {
                    missing.Add("Phinix_pawnCheck_health".Translate(hediff.DefName));
                    continue;
                }

                // A level above our maximum gets lowered, e.g. a Vanilla Psycasts Expanded psylink in a game without it
                if (hediff.Level > hediffDef.maxSeverity && typeof(Hediff_Level).IsAssignableFrom(hediffDef.hediffClass))
                {
                    missing.Add("Phinix_pawnCheck_level".Translate(hediffDef.LabelCap, hediff.Level, (int) hediffDef.maxSeverity));
                }

                if (!string.IsNullOrEmpty(hediff.BodyPartDefName) &&
                    race.race.body?.AllParts.Any(part => part.def.defName == hediff.BodyPartDefName) != true)
                {
                    missing.Add("Phinix_pawnCheck_bodyPart".Translate(hediff.DefName, hediff.BodyPartDefName));
                }
            }
        }

        private static void checkLooks<T>(string defName, List<string> missing) where T : Def
        {
            if (!string.IsNullOrEmpty(defName) && DefDatabase<T>.GetNamedSilentFail(defName) == null)
                missing.Add("Phinix_pawnCheck_looks".Translate(defName));
        }
    }
}
