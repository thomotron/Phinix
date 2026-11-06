using System;
using System.Collections.Generic;
using System.Linq;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Converts a pawn's health conditions (hediffs) to and from <see cref="ProtoHediff"/>s.
    /// Shared by <see cref="AnimalConverter"/> and <see cref="ColonistConverter"/>, since animals and colonists store
    /// injuries, implants and missing parts the same way.
    /// </summary>
    public static class HediffConverter
    {
        /// <summary>
        /// Records every health condition on a pawn along with the body part it's on.
        /// </summary>
        /// <param name="pawn">Pawn to read from</param>
        /// <returns>Converted health conditions</returns>
        public static IEnumerable<ProtoHediff> ConvertHediffsFromVerse(Pawn pawn)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                ProtoHediff protoHediff = new ProtoHediff
                {
                    DefName = hediff.def.defName,
                    Severity = hediff.Severity,
                    // Without this, scars come back as fresh wounds that bleed
                    Permanent = hediff.IsPermanent()
                };

                // A psylink keeps its level apart from its severity, and the level is what counts
                if (hediff is Hediff_Level levelled) protoHediff.Level = levelled.level;

                // Remember bandages so tended wounds don't start bleeding again
                HediffComp_TendDuration tendComp = hediff.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null && tendComp.IsTended)
                {
                    protoHediff.TendQuality = tendComp.tendQuality;
                    protoHediff.TendTicksLeft = tendComp.tendTicksLeft;
                }

                if (hediff.Part != null)
                {
                    protoHediff.BodyPartDefName = hediff.Part.def.defName;
                    protoHediff.BodyPartIndex = getBodyPartIndex(pawn, hediff.Part);
                }

                // "yield return" hands back one item at a time, just like Python's yield
                yield return protoHediff;
            }
        }

        /// <summary>
        /// Replaces a pawn's health conditions with the given ones.
        /// </summary>
        /// <param name="pawn">Newly-generated pawn</param>
        /// <param name="protoHediffs">Health conditions from the trade</param>
        public static void ApplyHediffs(Pawn pawn, IEnumerable<ProtoHediff> protoHediffs)
        {
            // Clear anything the generator added so we end up with exactly what was sent
            pawn.health.RemoveAllHediffs();

            foreach (ProtoHediff protoHediff in protoHediffs)
            {
                // Vanilla Psycasts Expanded's progress is rebuilt by PsycastConverter instead
                if (PsycastConverter.RestoredSeparately(protoHediff.DefName)) continue;

                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(protoHediff.DefName);
                if (hediffDef == null)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown hediff '{0}' on traded pawn", protoHediff.DefName), LogLevel.WARNING));
                    continue;
                }

                // Find the body part, if the condition is on one
                BodyPartRecord part = null;
                if (!string.IsNullOrEmpty(protoHediff.BodyPartDefName))
                {
                    part = findBodyPart(pawn, protoHediff.BodyPartDefName, protoHediff.BodyPartIndex);

                    // Skip conditions on parts this pawn doesn't have, or that are already gone
                    // (e.g. a scar on a leg that an earlier "missing leg" entry already removed)
                    if (part == null || pawn.health.hediffSet.PartIsMissing(part)) continue;
                }

                // One bad condition shouldn't stop the whole pawn from arriving
                try
                {
                    Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn, part);
                    hediff.Severity = protoHediff.Severity;

                    // Set the level before adding, since a psylink hands out psycasts for its level as it's added.
                    // Old offers have no level (0), so those keep the default of 1. The level is capped to this game's
                    // maximum, which mods can change (Vanilla Psycasts Expanded raises the psylink's from 6)
                    if (hediff is Hediff_Level levelled && protoHediff.Level > 0)
                    {
                        levelled.level = (int) Mathf.Clamp(protoHediff.Level, Math.Max(hediffDef.minSeverity, 1f), hediffDef.maxSeverity);
                    }

                    // No "psylink level gained" letter for a colonist who already had it
                    if (hediff is Hediff_Psylink psylink) psylink.suppressPostAddLetter = true;

                    // Mark scars as scars before adding, so they aren't merged into a fresh wound on the same part
                    if (protoHediff.Permanent)
                    {
                        HediffComp_GetsPermanent permanentComp = hediff.TryGetComp<HediffComp_GetsPermanent>();
                        if (permanentComp != null) permanentComp.IsPermanent = true;
                    }

                    pawn.health.AddHediff(hediff, part);

                    // Put the bandage back on
                    if (protoHediff.TendTicksLeft > 0)
                    {
                        HediffComp_TendDuration tendComp = hediff.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            tendComp.tendQuality = protoHediff.TendQuality;
                            tendComp.tendTicksLeft = protoHediff.TendTicksLeft;
                        }
                    }
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Failed to add hediff '{0}' to traded pawn: {1}", protoHediff.DefName, e.Message), LogLevel.WARNING));
                }
            }
        }

        /// <summary>
        /// Records how immune a pawn is to each disease they have or had recently. The game keeps this apart from the
        /// diseases themselves, so without it a sick pawn would arrive with the disease but none of their immunity.
        /// </summary>
        /// <param name="pawn">Pawn to read from</param>
        /// <returns>Converted immunities</returns>
        public static IEnumerable<ProtoImmunity> ConvertImmunitiesFromVerse(Pawn pawn)
        {
            if (pawn.health.immunity == null) yield break;

            foreach (ImmunityRecord record in pawn.health.immunity.ImmunityListForReading)
            {
                if (record.hediffDef == null) continue;

                yield return new ProtoImmunity
                {
                    DefName = record.hediffDef.defName,
                    SourceDefName = record.source?.defName ?? "",
                    Immunity = record.immunity
                };
            }
        }

        /// <summary>
        /// Gives a newly-generated pawn the immunities that were sent. Call after <see cref="ApplyHediffs"/>.
        /// </summary>
        /// <param name="pawn">Newly-generated pawn</param>
        /// <param name="protoImmunities">Immunities from the trade</param>
        public static void ApplyImmunities(Pawn pawn, IEnumerable<ProtoImmunity> protoImmunities)
        {
            if (pawn.health.immunity == null) return;

            foreach (ProtoImmunity protoImmunity in protoImmunities)
            {
                // A disease from a mod this game doesn't have can't be caught here anyway
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(protoImmunity.DefName);
                if (hediffDef == null) continue;
                HediffDef sourceDef = DefDatabase<HediffDef>.GetNamedSilentFail(protoImmunity.SourceDefName) ?? hediffDef;

                try
                {
                    // Adds a record at 0 if there isn't one yet (only for things you can become immune to)
                    pawn.health.immunity.TryAddImmunityRecord(hediffDef, sourceDef);

                    // Set it on the real record. GetImmunityRecord can hand back a made-up one for pawns that are
                    // immune through an implant or a gene, so look in the list instead
                    ImmunityRecord record = pawn.health.immunity.ImmunityListForReading.FirstOrDefault(r => r.hediffDef == hediffDef);
                    if (record != null) record.immunity = Mathf.Clamp01(protoImmunity.Immunity);
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Failed to restore immunity to '{0}' on traded pawn: {1}", protoImmunity.DefName, e.Message), LogLevel.WARNING));
                }
            }
        }

        /// <summary>
        /// Works out which of the body parts sharing a def this is, e.g. 0 for the first leg and 1 for the second.
        /// </summary>
        /// <param name="pawn">Pawn the part belongs to</param>
        /// <param name="part">Body part to find the index of</param>
        /// <returns>Index among parts with the same def</returns>
        private static int getBodyPartIndex(Pawn pawn, BodyPartRecord part)
        {
            return pawn.RaceProps.body.AllParts
                .Where(p => p.def == part.def)
                .ToList()
                .IndexOf(part);
        }

        /// <summary>
        /// Finds a body part by def name and index, the reverse of <see cref="getBodyPartIndex"/>.
        /// </summary>
        /// <param name="pawn">Pawn to search</param>
        /// <param name="defName">Body part def name</param>
        /// <param name="index">Index among parts with the same def</param>
        /// <returns>Matching body part, or null if there isn't one</returns>
        private static BodyPartRecord findBodyPart(Pawn pawn, string defName, int index)
        {
            return pawn.RaceProps.body.AllParts
                .Where(p => p.def.defName == defName)
                .ElementAtOrDefault(index);
        }
    }
}
