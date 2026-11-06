using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Converts colonists to and from <see cref="ProtoColonist"/>s so they can be traded.
    /// Used by <see cref="TradingThingConverter"/> whenever a traded thing is a colonist.
    /// </summary>
    /// <remarks>
    /// The receiving game can't take the original pawn, so a fresh pawn is generated and then made to match
    /// the one that was sent: same name, story, skills, genes, health, looks and gear. Royal titles travel as honor
    /// and are worked out again in the receiving world (see <see cref="applyHonor"/>). Psycasts, including Vanilla
    /// Psycasts Expanded's, are handled by <see cref="PsycastConverter"/>.
    /// Family and relationships, permits and work priorities are left behind.
    /// </remarks>
    public static class ColonistConverter
    {
        /// <summary>
        /// Whether the given thing is a colonist (or any human-like pawn) that this converter can handle.
        /// </summary>
        /// <param name="thing">Thing to check</param>
        /// <returns>Whether the thing is a human-like pawn</returns>
        public static bool IsColonist(Verse.Thing thing)
        {
            return thing is Pawn pawn && pawn.RaceProps.Humanlike;
        }

        /// <summary>
        /// Whether a colonist is allowed to be offered in a trade.
        /// Only free colonists can go: prisoners, slaves and quest guests belong to someone else.
        /// </summary>
        /// <param name="pawn">Colonist to check</param>
        /// <returns>Whether the colonist can be traded</returns>
        public static bool CanTrade(Pawn pawn)
        {
            return !pawn.Dead && pawn.IsFreeNonSlaveColonist && !pawn.IsQuestLodger();
        }

        /// <summary>
        /// Converts a live colonist into a <see cref="ProtoColonist"/>.
        /// Used for preparing a colonist for transport.
        /// </summary>
        /// <param name="pawn">Colonist to convert</param>
        /// <returns>Converted colonist</returns>
        public static ProtoColonist ConvertColonistFromVerse(Pawn pawn)
        {
            ProtoColonist protoColonist = new ProtoColonist
            {
                DefName = pawn.def.defName,
                KindDefName = pawn.kindDef.defName,
                BiologicalAgeTicks = pawn.ageTracker.AgeBiologicalTicks,
                ChronologicalAgeTicks = pawn.ageTracker.AgeChronologicalTicks,
                IsFemale = pawn.gender == Gender.Female,
                // An enum is a named number underneath, so it can be cast to an int and back
                DevelopmentalStage = (int) pawn.DevelopmentalStage
            };

            // Protobuf strings can't be null, so "?? """ swaps any null for an empty string (like Python's "x or ''")
            if (pawn.Name is NameTriple name)
            {
                protoColonist.FirstName = name.First ?? "";
                protoColonist.NickName = name.Nick ?? "";
                protoColonist.LastName = name.Last ?? "";
            }
            else if (pawn.Name != null)
            {
                protoColonist.NickName = pawn.Name.ToStringShort ?? "";
            }

            if (pawn.story != null)
            {
                protoColonist.ChildhoodDefName = pawn.story.Childhood?.defName ?? "";
                protoColonist.AdulthoodDefName = pawn.story.Adulthood?.defName ?? "";

                // Skip traits that come from a gene, since the gene will give them back on the other side
                foreach (Trait trait in pawn.story.traits.allTraits.Where(t => t.sourceGene == null))
                {
                    protoColonist.Traits.Add(new ProtoTrait { DefName = trait.def.defName, Degree = trait.Degree });
                }

                protoColonist.Appearance = convertAppearanceFromVerse(pawn);
            }

            if (pawn.skills != null)
            {
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    protoColonist.Skills.Add(new ProtoSkill
                    {
                        DefName = skill.def.defName,
                        Level = skill.Level,
                        XpSinceLastLevel = skill.xpSinceLastLevel,
                        Passion = (int) skill.passion
                    });
                }
            }

            protoColonist.Hediffs.AddRange(HediffConverter.ConvertHediffsFromVerse(pawn));
            protoColonist.Immunities.AddRange(HediffConverter.ConvertImmunitiesFromVerse(pawn));

            if (pawn.genes != null)
            {
                // A custom xenotype is one the player designed, so it has a name but no def
                if (pawn.genes.UniqueXenotype)
                {
                    protoColonist.CustomXenotypeName = pawn.genes.xenotypeName ?? "";
                }
                else
                {
                    protoColonist.XenotypeDefName = pawn.genes.Xenotype?.defName ?? "";
                }

                protoColonist.Endogenes.AddRange(pawn.genes.Endogenes.Select(gene => gene.def.defName));
                protoColonist.Xenogenes.AddRange(pawn.genes.Xenogenes.Select(gene => gene.def.defName));
            }

            // Gear is converted with the normal item converter, so it keeps its quality, material and damage
            if (pawn.apparel != null)
            {
                protoColonist.Apparel.AddRange(pawn.apparel.WornApparel.Select(TradingThingConverter.ConvertThingFromVerse));
            }
            if (pawn.equipment != null)
            {
                protoColonist.Equipment.AddRange(pawn.equipment.AllEquipmentListForReading.Select(TradingThingConverter.ConvertThingFromVerse));
            }
            if (pawn.inventory != null)
            {
                protoColonist.Inventory.AddRange(pawn.inventory.innerContainer.Select(TradingThingConverter.ConvertThingFromVerse));
            }

            if (pawn.royalty != null)
            {
                protoColonist.Honor.AddRange(convertHonorFromVerse(pawn));
            }

            protoColonist.Psycasts = PsycastConverter.ConvertPsycastsFromVerse(pawn);

            return protoColonist;
        }

        /// <summary>
        /// Swaps a converted colonist's gear for full copies (see <see cref="TradingThingConverter.ConvertThingForSending"/>),
        /// so it keeps its art and mod data like any other item sent, and marks the gear that's biocoded to them.
        /// Used when the colonist is actually being sent, not just shown. Must be called from the main thread.
        /// </summary>
        /// <param name="pawn">Colonist being sent</param>
        /// <param name="protoColonist">The colonist as converted by <see cref="ConvertColonistFromVerse"/></param>
        public static void AddGearCopies(Pawn pawn, ProtoColonist protoColonist)
        {
            if (pawn.apparel != null)
            {
                protoColonist.Apparel.Clear();
                protoColonist.Apparel.AddRange(pawn.apparel.WornApparel.Select(apparel => gearForSending(pawn, apparel)));
            }
            if (pawn.equipment != null)
            {
                protoColonist.Equipment.Clear();
                protoColonist.Equipment.AddRange(pawn.equipment.AllEquipmentListForReading.Select(equipment => gearForSending(pawn, equipment)));
            }
            if (pawn.inventory != null)
            {
                protoColonist.Inventory.Clear();
                protoColonist.Inventory.AddRange(pawn.inventory.innerContainer.Select(thing => gearForSending(pawn, thing)));
            }
        }

        /// <summary>
        /// Converts one piece of a colonist's gear with its full copy, noting whether it's biocoded to them.
        /// </summary>
        private static ProtoThing gearForSending(Pawn pawn, Verse.Thing thing)
        {
            ProtoThing protoThing = TradingThingConverter.ConvertThingForSending(thing);
            protoThing.BiocodedToCarrier = CompBiocodable.IsBiocodedFor(thing, pawn);
            return protoThing;
        }

        /// <summary>
        /// Creates a new colonist from a <see cref="ProtoColonist"/> belonging to the player's faction.
        /// Used for unloading a colonist after transport. Must be called on the main game thread.
        /// </summary>
        /// <param name="protoColonist">Colonist to convert</param>
        /// <returns>New colonist, not yet spawned on a map</returns>
        /// <exception cref="InvalidOperationException">The race or kind doesn't exist in this game (e.g. a missing mod)</exception>
        public static Pawn ConvertColonistFromProto(ProtoColonist protoColonist)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(protoColonist.DefName);
            if (raceDef?.race == null || !raceDef.race.Humanlike)
            {
                throw new InvalidOperationException(string.Format("Could not find a human-like race with def name '{0}'", protoColonist.DefName));
            }

            // Look up the pawn kind, falling back to any kind of the same race
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(protoColonist.KindDefName)
                                  ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(kind => kind.race == raceDef);
            if (kindDef == null)
            {
                throw new InvalidOperationException(string.Format("Could not find a pawn kind for race '{0}'", protoColonist.DefName));
            }

            // Old offers won't have a stage, which shows up as 0, so treat those as adults
            DevelopmentalStage stage = protoColonist.DevelopmentalStage != 0
                ? (DevelopmentalStage) protoColonist.DevelopmentalStage
                : DevelopmentalStage.Adult;

            // Generate a fresh, naked colonist of the right race, sex and age who already follows our ideology.
            // Everything else gets overwritten below.
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind: kindDef,
                faction: Faction.OfPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                allowPregnant: false,
                allowAddictions: false,
                fixedBiologicalAge: protoColonist.BiologicalAgeTicks / (float) GenDate.TicksPerYear,
                fixedChronologicalAge: protoColonist.ChronologicalAgeTicks / (float) GenDate.TicksPerYear,
                fixedGender: protoColonist.IsFemale ? Gender.Female : Gender.Male,
                fixedIdeo: Faction.OfPlayer.ideos?.PrimaryIdeo,
                developmentalStages: stage,
                forceNoGear: true,
                // Titles come from the honor that was sent, not from the generator
                forbidAnyTitle: true
            ));

            // Set the exact ages, since the request above only takes whole-ish years
            pawn.ageTracker.AgeBiologicalTicks = protoColonist.BiologicalAgeTicks;
            pawn.ageTracker.AgeChronologicalTicks = protoColonist.ChronologicalAgeTicks;

            applyName(pawn, protoColonist);

            // Genes go first because they can add traits, health conditions and change looks,
            // which the steps after this then overwrite with exactly what was sent
            applyGenes(pawn, protoColonist);
            applyStory(pawn, protoColonist);
            applyHonor(pawn, protoColonist);
            applySkills(pawn, protoColonist);
            HediffConverter.ApplyHediffs(pawn, protoColonist.Hediffs);
            HediffConverter.ApplyImmunities(pawn, protoColonist.Immunities);
            // After the health conditions, since psycasts go on top of the psylink
            PsycastConverter.ApplyPsycasts(pawn, protoColonist.Psycasts);
            applyGear(pawn, protoColonist);

            // Backstories, traits and genes decide which jobs a colonist refuses to do, so let the game recheck that
            pawn.Notify_DisabledWorkTypesChanged();
            pawn.skills?.Notify_SkillDisablesChanged();
            pawn.workSettings?.EnableAndInitialize();

            // Redraw them, since their looks changed after they were generated
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);

            return pawn;
        }

        /// <summary>
        /// Gives the colonist back their full name.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applyName(Pawn pawn, ProtoColonist protoColonist)
        {
            if (!string.IsNullOrEmpty(protoColonist.FirstName) || !string.IsNullOrEmpty(protoColonist.LastName))
            {
                // A nickname is required, so fall back to their first name like the game does
                string nick = string.IsNullOrEmpty(protoColonist.NickName) ? protoColonist.FirstName : protoColonist.NickName;
                pawn.Name = new NameTriple(protoColonist.FirstName, nick, protoColonist.LastName);
            }
            else if (!string.IsNullOrEmpty(protoColonist.NickName))
            {
                pawn.Name = new NameSingle(protoColonist.NickName);
            }
        }

        /// <summary>
        /// Replaces the colonist's genes and xenotype with the ones that were sent.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applyGenes(Pawn pawn, ProtoColonist protoColonist)
        {
            if (pawn.genes == null) return;

            // ToList() makes a copy, because removing genes while looping over the real list would break the loop
            foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
            {
                pawn.genes.RemoveGene(gene);
            }

            addGenes(pawn, protoColonist.Endogenes, false);
            addGenes(pawn, protoColonist.Xenogenes, true);

            // SetXenotypeDirect only changes the label and icon, without adding the xenotype's genes again
            XenotypeDef xenotypeDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(protoColonist.XenotypeDefName) ?? XenotypeDefOf.Baseliner;
            pawn.genes.SetXenotypeDirect(xenotypeDef);
            if (!string.IsNullOrEmpty(protoColonist.CustomXenotypeName))
            {
                pawn.genes.xenotypeName = protoColonist.CustomXenotypeName;
            }
        }

        /// <summary>
        /// Adds a list of genes by def name, skipping any this game doesn't have.
        /// </summary>
        /// <param name="pawn">Colonist to add genes to</param>
        /// <param name="geneDefNames">Gene def names</param>
        /// <param name="xenogene">Whether these are xenogenes (true) or endogenes (false)</param>
        private static void addGenes(Pawn pawn, IEnumerable<string> geneDefNames, bool xenogene)
        {
            foreach (string defName in geneDefNames)
            {
                GeneDef geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
                if (geneDef == null)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown gene '{0}' on traded colonist", defName), LogLevel.WARNING));
                    continue;
                }

                try
                {
                    pawn.genes.AddGene(geneDef, xenogene);
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Failed to add gene '{0}' to traded colonist: {1}", defName, e.Message), LogLevel.WARNING));
                }
            }
        }

        /// <summary>
        /// Finds the backstory that was sent, falling back to the one the generator gave the colonist.
        /// </summary>
        /// <remarks>
        /// A backstory from a mod this game doesn't have can't be looked up. Setting it to null instead would crash:
        /// the game's Childhood setter reads the backstory to check it suits an adult. So the generated one stays.
        /// </remarks>
        /// <param name="generated">Backstory the colonist was generated with</param>
        /// <param name="defName">Backstory that was sent, or empty for none</param>
        /// <param name="canClear">Whether an empty <paramref name="defName"/> clears the backstory</param>
        /// <returns>Backstory to give the colonist</returns>
        private static BackstoryDef backstoryOrKeep(BackstoryDef generated, string defName, bool canClear)
        {
            if (string.IsNullOrEmpty(defName)) return canClear ? null : generated;

            BackstoryDef backstory = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (backstory != null) return backstory;

            Client.Instance.Log(new LogEventArgs(string.Format("Unknown backstory '{0}' on traded colonist, keeping '{1}'", defName, generated?.defName), LogLevel.WARNING));
            return generated;
        }

        /// <summary>
        /// Gives the colonist back their backstories, traits and looks.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applyStory(Pawn pawn, ProtoColonist protoColonist)
        {
            if (pawn.story == null) return;

            // Only set what changed, so a kept backstory never goes through the setter (it would crash on a null one).
            // An adult's childhood can't be cleared, for the same reason
            BackstoryDef childhood = backstoryOrKeep(pawn.story.Childhood, protoColonist.ChildhoodDefName, !pawn.DevelopmentalStage.Adult());
            if (childhood != pawn.story.Childhood) pawn.story.Childhood = childhood;

            // Children don't have an adulthood yet, so an empty one is cleared rather than kept
            BackstoryDef adulthood = backstoryOrKeep(pawn.story.Adulthood, protoColonist.AdulthoodDefName, true);
            if (adulthood != pawn.story.Adulthood) pawn.story.Adulthood = adulthood;

            // Remove the generator's traits, keeping the ones our genes just added
            TraitSet traits = pawn.story.traits;
            foreach (Trait trait in traits.allTraits.Where(t => t.sourceGene == null).ToList())
            {
                traits.RemoveTrait(trait);
            }

            foreach (ProtoTrait protoTrait in protoColonist.Traits)
            {
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(protoTrait.DefName);
                if (traitDef == null)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown trait '{0}' on traded colonist", protoTrait.DefName), LogLevel.WARNING));
                    continue;
                }

                // A gene may have already given them this trait
                if (traits.HasTrait(traitDef, protoTrait.Degree)) continue;

                traits.GainTrait(new Trait(traitDef, protoTrait.Degree));
            }

            if (protoColonist.Appearance != null) applyAppearance(pawn, protoColonist.Appearance);
        }

        /// <summary>
        /// Adds up a colonist's honor with each faction that gives titles: the honor they have now, plus what each
        /// title up to their current one cost.
        /// </summary>
        /// <remarks>
        /// Sending the total instead of the titles themselves means the receiver can work the titles out again on its
        /// own title ladder, which mods may have changed. It's the same sum the game uses when an heir inherits a title.
        /// </remarks>
        /// <param name="pawn">Colonist being sent</param>
        /// <returns>Total honor per faction, leaving out factions they have none with</returns>
        private static IEnumerable<ProtoHonor> convertHonorFromVerse(Pawn pawn)
        {
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                int honor = pawn.royalty.GetFavor(faction);

                // Walk down from their current title, adding what each one cost. The step limit guards against a
                // modded ladder that loops back on itself
                RoyalTitleDef title = pawn.royalty.GetCurrentTitleInFaction(faction)?.def;
                for (int steps = 0; title != null && steps < 200; steps++)
                {
                    honor += title.favorCost;
                    title = title.GetPreviousTitle_IncludeNonRewardable(faction);
                }

                if (honor > 0) yield return new ProtoHonor { FactionDefName = faction.def.defName, TotalHonor = honor };
            }
        }

        /// <summary>
        /// Gives the colonist their honor back and spends it on titles in this world: climbing the faction's title
        /// ladder for as long as the honor covers the next title, with whatever's left over kept as honor.
        /// </summary>
        /// <remarks>
        /// No title rewards are handed out, since the colonist earned those where they came from (and trading a
        /// colonist back and forth mustn't hand them out again). Permits aren't carried over, so the permit points
        /// from their titles are free to spend again. Honor with a faction this world doesn't have is lost
        /// (<see cref="PawnCheck"/> warns about it).
        /// </remarks>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applyHonor(Pawn pawn, ProtoColonist protoColonist)
        {
            if (pawn.royalty == null || !ModsConfig.RoyaltyActive) return;

            // A world could have two factions of the same kind, which would send two totals for one def
            foreach (IGrouping<string, ProtoHonor> group in protoColonist.Honor.GroupBy(honor => honor.FactionDefName))
            {
                FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(group.Key);
                Faction faction = factionDef == null ? null : Find.FactionManager.FirstFactionOfDef(factionDef);
                if (faction == null)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping honor with '{0}' on traded colonist, that faction isn't in this world", group.Key), LogLevel.WARNING));
                    continue;
                }

                int honorLeft = group.Sum(honor => honor.TotalHonor);
                RoyalTitleDef title = null;
                foreach (RoyalTitleDef next in factionDef.RoyalTitlesAwardableInSeniorityOrderForReading)
                {
                    if (honorLeft < next.favorCost) break;
                    honorLeft -= next.favorCost;
                    title = next;
                }

                // The arrival letter covers it, so no "gained a title" letter while they're still on their way
                if (title != null) pawn.royalty.SetTitle(faction, title, grantRewards: false, sendLetter: false);
                pawn.royalty.SetFavor(faction, honorLeft, notifyOnFavorChanged: false);
            }
        }

        /// <summary>
        /// Gives the colonist back their skill levels, progress and passions.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applySkills(Pawn pawn, ProtoColonist protoColonist)
        {
            if (pawn.skills == null) return;

            foreach (ProtoSkill protoSkill in protoColonist.Skills)
            {
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(protoSkill.DefName);
                SkillRecord skill = skillDef != null ? pawn.skills.GetSkill(skillDef) : null;
                if (skill == null) continue;

                skill.Level = protoSkill.Level;
                skill.xpSinceLastLevel = protoSkill.XpSinceLastLevel;
                skill.passion = (Passion) protoSkill.Passion;
            }
        }

        /// <summary>
        /// Dresses the colonist and gives them back their weapon and inventory.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoColonist">Colonist data from the trade</param>
        private static void applyGear(Pawn pawn, ProtoColonist protoColonist)
        {
            foreach (Verse.Thing thing in convertGear(pawn, protoColonist.Apparel))
            {
                if (thing is Apparel apparel && pawn.apparel != null)
                {
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                }
                else
                {
                    pawn.inventory?.innerContainer.TryAdd(thing);
                }
            }

            foreach (Verse.Thing thing in convertGear(pawn, protoColonist.Equipment))
            {
                // Only one weapon can be held, so any extras go in their pockets
                if (thing is ThingWithComps equipment && pawn.equipment != null && pawn.equipment.Primary == null)
                {
                    pawn.equipment.AddEquipment(equipment);
                }
                else
                {
                    pawn.inventory?.innerContainer.TryAdd(thing);
                }
            }

            foreach (Verse.Thing thing in convertGear(pawn, protoColonist.Inventory))
            {
                pawn.inventory?.innerContainer.TryAdd(thing);
            }
        }

        /// <summary>
        /// Converts gear back into real things like any received item (full copy, art), skipping any this game doesn't
        /// have. Gear that was biocoded to this colonist is biocoded to them again.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoThings">Gear from the trade</param>
        /// <returns>Converted gear</returns>
        private static IEnumerable<Verse.Thing> convertGear(Pawn pawn, IEnumerable<ProtoThing> protoThings)
        {
            foreach (ProtoThing protoThing in protoThings)
            {
                Verse.Thing thing = null;
                try
                {
                    thing = TradingThingConverter.ConvertReceivedThing(protoThing);

                    // An item this game doesn't have comes back as an "unknown item", which is no use as gear
                    if (thing is UnknownItem)
                    {
                        Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown gear '{0}' on traded colonist", protoThing.DefName), LogLevel.WARNING));
                        thing = null;
                    }
                    else if (protoThing.BiocodedToCarrier)
                    {
                        // The receiving side cleared the biocoding, since the colonist it pointed at stayed behind. This is
                        // that colonist arriving, so it's theirs again
                        thing.TryGetComp<CompBiocodable>()?.CodeFor(pawn);
                    }
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping gear '{0}' on traded colonist: {1}", protoThing.DefName, e.Message), LogLevel.WARNING));
                }

                // C# doesn't allow "yield return" inside a try with a catch, so it goes out here
                if (thing != null) yield return thing;
            }
        }

        /// <summary>
        /// Records what the colonist looks like.
        /// </summary>
        /// <param name="pawn">Colonist to read from</param>
        /// <returns>Converted appearance</returns>
        private static ProtoAppearance convertAppearanceFromVerse(Pawn pawn)
        {
            ProtoAppearance appearance = new ProtoAppearance
            {
                BodyTypeDefName = pawn.story.bodyType?.defName ?? "",
                HeadTypeDefName = pawn.story.headType?.defName ?? "",
                HairDefName = pawn.story.hairDef?.defName ?? "",
                FurDefName = pawn.story.furDef?.defName ?? "",
                FavoriteColorDefName = pawn.story.favoriteColor?.defName ?? "",
                HairColor = convertColorFromVerse(pawn.story.HairColor)
            };

            // "x.HasValue" asks whether a nullable value (written "Color?") is actually set, like "x is not None"
            if (pawn.story.skinColorOverride.HasValue)
            {
                appearance.SkinColorOverride = convertColorFromVerse(pawn.story.skinColorOverride.Value);
            }

            if (pawn.style != null)
            {
                appearance.BeardDefName = pawn.style.beardDef?.defName ?? "";
                appearance.FaceTattooDefName = pawn.style.FaceTattoo?.defName ?? "";
                appearance.BodyTattooDefName = pawn.style.BodyTattoo?.defName ?? "";
            }

            return appearance;
        }

        /// <summary>
        /// Makes the colonist look the way they did before the trade.
        /// Anything this game doesn't have (e.g. a hairstyle from a missing mod) is left as the generator made it.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="appearance">Appearance from the trade</param>
        private static void applyAppearance(Pawn pawn, ProtoAppearance appearance)
        {
            pawn.story.bodyType = DefDatabase<BodyTypeDef>.GetNamedSilentFail(appearance.BodyTypeDefName) ?? pawn.story.bodyType;
            pawn.story.headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail(appearance.HeadTypeDefName) ?? pawn.story.headType;
            pawn.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail(appearance.HairDefName) ?? pawn.story.hairDef;
            pawn.story.furDef = DefDatabase<FurDef>.GetNamedSilentFail(appearance.FurDefName);
            pawn.story.favoriteColor = DefDatabase<ColorDef>.GetNamedSilentFail(appearance.FavoriteColorDefName) ?? pawn.story.favoriteColor;

            if (appearance.HairColor != null) pawn.story.HairColor = convertColorFromProto(appearance.HairColor);

            // "(Color?) null" means "no override", so the skin colour comes from their genes as normal
            pawn.story.skinColorOverride = appearance.SkinColorOverride != null
                ? convertColorFromProto(appearance.SkinColorOverride)
                : (Color?) null;

            if (pawn.style != null)
            {
                pawn.style.beardDef = DefDatabase<BeardDef>.GetNamedSilentFail(appearance.BeardDefName) ?? pawn.style.beardDef;
                pawn.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail(appearance.FaceTattooDefName) ?? pawn.style.FaceTattoo;
                pawn.style.BodyTattoo = DefDatabase<TattooDef>.GetNamedSilentFail(appearance.BodyTattooDefName) ?? pawn.style.BodyTattoo;
            }
        }

        private static ProtoColor convertColorFromVerse(Color color) => new ProtoColor { R = color.r, G = color.g, B = color.b, A = color.a };

        private static Color convertColorFromProto(ProtoColor color) => new Color(color.R, color.G, color.B, color.A);
    }
}
