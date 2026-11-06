using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Trading;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Converts tame animals to and from <see cref="ProtoAnimal"/>s so they can be traded.
    /// Used by <see cref="TradingThingConverter"/> whenever a traded thing is an animal.
    /// </summary>
    public static class AnimalConverter
    {
        /// <summary>
        /// RimWorld keeps <c>Pawn_TrainingTracker.GetSteps</c> private, so we look it up once with Harmony's
        /// AccessTools and call it through reflection.
        /// </summary>
        private static readonly MethodInfo getStepsMethod = AccessTools.Method(typeof(Pawn_TrainingTracker), "GetSteps");

        /// <summary>
        /// Whether the given thing is an animal that this converter can handle.
        /// </summary>
        /// <param name="thing">Thing to check</param>
        /// <returns>Whether the thing is an animal</returns>
        public static bool IsAnimal(Verse.Thing thing)
        {
            // "is Pawn pawn" both checks the type and gives us a Pawn-typed variable to use
            return thing is Pawn pawn && pawn.RaceProps.Animal;
        }

        /// <summary>
        /// Converts a live animal into a <see cref="ProtoAnimal"/>.
        /// Used for preparing an animal for transport.
        /// </summary>
        /// <param name="pawn">Animal to convert</param>
        /// <returns>Converted animal</returns>
        public static ProtoAnimal ConvertAnimalFromVerse(Pawn pawn)
        {
            ProtoAnimal protoAnimal = new ProtoAnimal
            {
                DefName = pawn.def.defName,
                KindDefName = pawn.kindDef.defName,
                BiologicalAgeTicks = pawn.ageTracker.AgeBiologicalTicks,
                ChronologicalAgeTicks = pawn.ageTracker.AgeChronologicalTicks,
                IsFemale = pawn.gender == Gender.Female
            };

            // Only send real nicknames, not auto-generated numbered ones like "Muffalo 3"
            if (pawn.Name is NameSingle name && !name.Numerical)
            {
                protoAnimal.CustomName = name.Name;
            }

            // Record how many steps of each trick the animal has learned
            // training is null for animals that can't be trained at all
            if (pawn.training != null)
            {
                foreach (TrainableDef trainableDef in DefDatabase<TrainableDef>.AllDefsListForReading)
                {
                    int steps = (int) getStepsMethod.Invoke(pawn.training, new object[] { trainableDef });
                    if (steps > 0)
                    {
                        protoAnimal.TrainingSteps.Add(trainableDef.defName, steps);
                    }
                }
            }

            // Record every health condition along with the body part it's on
            protoAnimal.Hediffs.AddRange(HediffConverter.ConvertHediffsFromVerse(pawn));
            protoAnimal.Immunities.AddRange(HediffConverter.ConvertImmunitiesFromVerse(pawn));

            // Note who the animal was bonded to, if anyone
            // "?." stops and gives null if the thing on its left is null, like Python's "x and x.y"
            Pawn bondedPawn = pawn.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            if (bondedPawn != null)
            {
                protoAnimal.BondedPawnName = bondedPawn.LabelShort;
            }

            return protoAnimal;
        }

        /// <summary>
        /// Creates a new animal from a <see cref="ProtoAnimal"/> belonging to the player's faction.
        /// Used for unloading an animal after transport.
        /// </summary>
        /// <param name="protoAnimal">Animal to convert</param>
        /// <returns>New animal, not yet spawned on a map</returns>
        /// <exception cref="InvalidOperationException">The species or kind doesn't exist in this game (e.g. a missing mod)</exception>
        public static Pawn ConvertAnimalFromProto(ProtoAnimal protoAnimal)
        {
            // Look up the species. GetNamedSilentFail returns null instead of throwing if it doesn't exist.
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(protoAnimal.DefName);
            if (raceDef?.race == null || !raceDef.race.Animal)
            {
                throw new InvalidOperationException(string.Format("Could not find an animal species with def name '{0}'", protoAnimal.DefName));
            }

            // Look up the pawn kind, falling back to any kind of the same species
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(protoAnimal.KindDefName)
                                  ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(kind => kind.race == raceDef);
            if (kindDef == null)
            {
                throw new InvalidOperationException(string.Format("Could not find a pawn kind for animal species '{0}'", protoAnimal.DefName));
            }

            // Generate a fresh animal of the right species, sex and rough age, already belonging to the player
            // These are "named arguments": only the options we care about are given, the rest keep their defaults
            float ageYears = protoAnimal.BiologicalAgeTicks / (float) GenDate.TicksPerYear;
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind: kindDef,
                faction: Faction.OfPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                allowPregnant: false,
                fixedBiologicalAge: ageYears,
                fixedChronologicalAge: protoAnimal.ChronologicalAgeTicks / (float) GenDate.TicksPerYear,
                fixedGender: protoAnimal.IsFemale ? Gender.Female : Gender.Male
            ));

            // Set the exact ages, since the request above only takes whole-ish years
            pawn.ageTracker.AgeBiologicalTicks = protoAnimal.BiologicalAgeTicks;
            pawn.ageTracker.AgeChronologicalTicks = protoAnimal.ChronologicalAgeTicks;

            // Give back its nickname
            if (!string.IsNullOrEmpty(protoAnimal.CustomName))
            {
                pawn.Name = new NameSingle(protoAnimal.CustomName);
            }

            applyTraining(pawn, protoAnimal);
            HediffConverter.ApplyHediffs(pawn, protoAnimal.Hediffs);
            HediffConverter.ApplyImmunities(pawn, protoAnimal.Immunities);

            // The bonded colonist stays behind, so there's nobody to link the bond to.
            // BondedPawnName is kept in the proto so the trade window can mention it later.

            return pawn;
        }

        /// <summary>
        /// Teaches the animal the same trick steps it had before the trade.
        /// </summary>
        /// <param name="pawn">Newly-generated animal</param>
        /// <param name="protoAnimal">Animal data from the trade</param>
        private static void applyTraining(Pawn pawn, ProtoAnimal protoAnimal)
        {
            if (pawn.training == null) return;

            // A proto map works like a Python dict: each entry has a Key (trick name) and Value (steps)
            foreach (KeyValuePair<string, int> entry in protoAnimal.TrainingSteps)
            {
                TrainableDef trainableDef = DefDatabase<TrainableDef>.GetNamedSilentFail(entry.Key);
                if (trainableDef == null)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown trainable '{0}' on traded animal", entry.Key), LogLevel.WARNING));
                    continue;
                }

                // Each Train() call adds one step, and the trick counts as learned once all its steps are done
                int steps = Math.Min(entry.Value, trainableDef.steps);
                for (int i = 0; i < steps; i++)
                {
                    pawn.training.Train(trainableDef, null);
                }

                // Keep fully-learned tricks switched on so the new owner's handlers maintain them
                if (pawn.training.HasLearned(trainableDef))
                {
                    pawn.training.SetWantedRecursive(trainableDef, true);
                }
            }
        }
    }
}
