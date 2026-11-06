using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Converts a colonist's learned psycasts to and from <see cref="ProtoPsycasts"/>, both Royalty's own and
    /// Vanilla Psycasts Expanded's.
    /// </summary>
    /// <remarks>
    /// The psylink itself travels with the other health conditions, level included (see <see cref="HediffConverter"/>).
    /// This class puts back what the colonist learned on top of it. Neural heat isn't carried over, and psyfocus
    /// arrives full, like a colonist who meditated before setting off.
    ///
    /// Vanilla Psycasts Expanded (VPE) is optional, so Phime can't refer to its classes directly: it wouldn't build or
    /// load without it. Instead <see cref="Vpe"/> looks them up by name while the game runs, and everything VPE-related is
    /// skipped when it isn't there.
    ///
    /// When the two games don't have the same mods, the colonist keeps as much as this game can hold:
    /// - A VPE psycast, path or meditation type this game doesn't have gives back the point it cost.
    /// - A Royalty psycast this game doesn't have can leave a psylink level with no psycast. That level gets a random
    ///   one, just like levelling up the psylink would give.
    /// - VPE psycasts arriving in a game without VPE: the psylink level stays and every level gets a random Royalty psycast.
    /// - Royalty psycasts arriving in a game with VPE: VPE turns the psylink level into points to spend.
    /// <see cref="CheckPsycasts"/> warns about all of these before the trade.
    /// </remarks>
    public static class PsycastConverter
    {
        /// <summary>
        /// VPE's hidden health condition that holds its psycast progress. It's rebuilt from <see cref="ProtoVpePsycasts"/>
        /// instead of being copied like the others.
        /// </summary>
        private const string VpeHediffDefName = "VPE_PsycastAbilityImplant";

        /// <summary>
        /// Whether a health condition is put back by this class, so <see cref="HediffConverter"/> should leave it out.
        /// </summary>
        /// <param name="hediffDefName">HediffDef name</param>
        /// <returns>Whether to leave it out</returns>
        public static bool RestoredSeparately(string hediffDefName) => hediffDefName == VpeHediffDefName;

        /// <summary>
        /// Records a colonist's learned psycasts.
        /// </summary>
        /// <param name="pawn">Colonist to read from</param>
        /// <returns>Their psycasts, or null if they have none</returns>
        public static ProtoPsycasts ConvertPsycastsFromVerse(Pawn pawn)
        {
            try
            {
                ProtoPsycasts psycasts = new ProtoPsycasts();

                if (pawn.abilities != null)
                {
                    psycasts.VanillaPsycasts.AddRange(pawn.abilities.abilities.Where(ability => ability.def.IsPsycast).Select(ability => ability.def.defName));
                }

                // "?." stops at a null and gives null, so this is null when VPE isn't loaded
                psycasts.Vpe = Vpe.Instance?.ConvertFromVerse(pawn);

                return psycasts.VanillaPsycasts.Count == 0 && psycasts.Vpe == null ? null : psycasts;
            }
            catch (Exception e)
            {
                // Better to send the colonist without psycasts than not at all
                Client.Instance.Log(new LogEventArgs(string.Format("Failed to read the psycasts of {0}, sending them without: {1}", pawn.LabelShort, e), LogLevel.WARNING));
                return null;
            }
        }

        /// <summary>
        /// Gives a newly-generated colonist their psycasts back. Their health conditions, psylink included, must
        /// already be in place.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoPsycasts">Psycasts from the trade, or null if they had none (or came from an older Phime)</param>
        public static void ApplyPsycasts(Pawn pawn, ProtoPsycasts protoPsycasts)
        {
            // A problem here should only cost the colonist their psycasts, not the whole colonist
            try
            {
                // Start from nothing: the generator may have made them a psycaster, and adding the psylink handed out a
                // random psycast
                removeVanillaPsycasts(pawn);
                Vpe.Instance?.RemovePsycasts(pawn);

                if (Vpe.Loaded)
                {
                    // With VPE, Royalty's psycasts aren't used at all. A colonist from a game without VPE keeps the
                    // points VPE gave them for their psylink level when it was added
                    if (protoPsycasts?.Vpe != null) Vpe.Instance?.Apply(pawn, protoPsycasts.Vpe);
                }
                else
                {
                    applyVanillaPsycasts(pawn, protoPsycasts);
                }
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Failed to restore the psycasts of {0}: {1}", pawn.LabelShort, e), LogLevel.WARNING));
            }

            // A newly-generated pawn's psyfocus starts unset and drops to empty on the first tick. Arriving
            // with a full bar is what you'd expect after the trip
            if (pawn.HasPsylink) pawn.psychicEntropy?.RechargePsyfocus();
        }

        /// <summary>
        /// Adds a line to <paramref name="missing"/> for each psycast, path or meditation type this game can't give back,
        /// and for psycasts that will change because only one of the two games uses VPE. Used by <see cref="PawnCheck"/>.
        /// </summary>
        /// <param name="colonist">Offered colonist</param>
        /// <param name="missing">Lines to add to</param>
        public static void CheckPsycasts(ProtoColonist colonist, List<string> missing)
        {
            ProtoPsycasts psycasts = colonist.Psycasts;
            if (psycasts == null) return;

            if (!Vpe.Loaded)
            {
                if (psycasts.Vpe != null) missing.Add("Phinix_pawnCheck_vpeMissing".Translate());

                foreach (string defName in psycasts.VanillaPsycasts.Where(name => DefDatabase<AbilityDef>.GetNamedSilentFail(name) == null))
                    missing.Add("Phinix_pawnCheck_psycast".Translate(defName));
                return;
            }

            if (psycasts.Vpe == null)
            {
                if (psycasts.VanillaPsycasts.Count > 0) missing.Add("Phinix_pawnCheck_vpeConvert".Translate());
                return;
            }

            Vpe vpe = Vpe.Instance;
            if (vpe == null)
            {
                missing.Add("Phinix_pawnCheck_vpeUnknownVersion".Translate());
                return;
            }

            foreach (string defName in psycasts.Vpe.Paths.Concat(psycasts.Vpe.LockedPaths).Where(name => !vpe.HasPath(name)))
                missing.Add("Phinix_pawnCheck_psycastPath".Translate(defName));
            foreach (string defName in psycasts.Vpe.MeditationFoci.Where(name => DefDatabase<MeditationFocusDef>.GetNamedSilentFail(name) == null))
                missing.Add("Phinix_pawnCheck_meditationFocus".Translate(defName));
            foreach (string defName in psycasts.Vpe.Abilities.Where(name => !vpe.HasPsycast(name)))
                missing.Add("Phinix_pawnCheck_psycastRefund".Translate(defName));
        }

        /// <summary>
        /// Takes away all of Royalty's psycasts.
        /// </summary>
        /// <param name="pawn">Colonist to clear</param>
        private static void removeVanillaPsycasts(Pawn pawn)
        {
            if (pawn.abilities == null) return;

            // ToList() makes a copy, because removing abilities while looping over the real list would break the loop
            foreach (RimWorld.Ability ability in pawn.abilities.abilities.Where(a => a.def.IsPsycast).ToList())
            {
                pawn.abilities.RemoveAbility(ability.def);
            }
        }

        /// <summary>
        /// Gives back the Royalty psycasts that were sent, then makes sure every psylink level has at least one.
        /// </summary>
        /// <param name="pawn">Newly-generated colonist</param>
        /// <param name="protoPsycasts">Psycasts from the trade, or null</param>
        private static void applyVanillaPsycasts(Pawn pawn, ProtoPsycasts protoPsycasts)
        {
            if (pawn.abilities == null) return;

            // Psycasts need a psylink to cast them with. A game without Royalty won't have given them one
            int psylinkLevel = pawn.GetPsylinkLevel();
            if (psylinkLevel == 0) return;

            if (protoPsycasts != null)
            {
                foreach (string defName in protoPsycasts.VanillaPsycasts)
                {
                    AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
                    if (abilityDef == null || !abilityDef.IsPsycast)
                    {
                        Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown psycast '{0}' on traded colonist", defName), LogLevel.WARNING));
                        continue;
                    }

                    pawn.abilities.GainAbility(abilityDef);
                }
            }

            // The game gives one psycast for each psylink level gained. Fill any level left empty, by a psycast from a
            // missing mod or by psycasts that came from VPE, with a random one of that level, like levelling up does
            for (int level = 1; level <= psylinkLevel; level++)
            {
                if (pawn.abilities.abilities.Any(ability => ability.def.IsPsycast && ability.def.level == level)) continue;

                if (DefDatabase<AbilityDef>.AllDefs.Where(def => def.IsPsycast && def.level == level).TryRandomElement(out AbilityDef randomDef))
                {
                    pawn.abilities.GainAbility(randomDef);
                }
            }
        }

        /// <summary>
        /// Vanilla Psycasts Expanded's classes, fields and methods, found by name while the game runs.
        /// </summary>
        /// <remarks>
        /// VPE keeps a colonist's progress (level, experience, points, unlocked paths and meditation types, psysets) in a
        /// hidden health condition, <c>Hediff_PsycastAbilities</c>. The psycasts themselves are in the Vanilla Expanded
        /// Framework's ability list, a <c>CompAbilities</c> on the pawn, alongside other VEF abilities. A psycast is one
        /// whose def has VPE's <c>AbilityExtension_Psycast</c>, the same test VPE uses when it resets someone's psycasts.
        ///
        /// Reflection (<see cref="FieldInfo"/>, <see cref="MethodInfo"/>) reads and calls members of a class we only know
        /// by name. It's slower than normal code and the compiler can't check it, so every member is looked up once and
        /// checked up front: if a VPE update renamed one, <see cref="Instance"/> is null and psycasts are skipped rather
        /// than half restored.
        /// </remarks>
        private class Vpe
        {
            private static Vpe instance;
            private static Type hediffType;
            private static bool lookedUp;

            /// <summary>
            /// Whether VPE is loaded, even if it's a version this class doesn't understand.
            /// </summary>
            public static bool Loaded
            {
                get
                {
                    lookUp();
                    return hediffType != null;
                }
            }

            /// <summary>
            /// The lookups, or null if VPE isn't loaded or isn't a version this class understands.
            /// </summary>
            public static Vpe Instance
            {
                get
                {
                    lookUp();
                    return instance;
                }
            }

            /// <summary>
            /// Looks everything up the first time it's needed. By then every mod has loaded.
            /// </summary>
            private static void lookUp()
            {
                if (lookedUp) return;
                lookedUp = true;

                hediffType = GenTypes.GetTypeInAnyAssembly("VanillaPsycastsExpanded.Hediff_PsycastAbilities");
                instance = create();
            }

            // Hediff_PsycastAbilities
            private Type implantType;
            private FieldInfo experience;
            private FieldInfo points;
            private FieldInfo statPoints;
            private FieldInfo maxLevelFromTitles;
            private FieldInfo unlockedPaths;
            private FieldInfo previousUnlockedPaths;
            private FieldInfo unlockedMeditationFoci;
            private FieldInfo psysets;
            private FieldInfo psysetIndex;
            private MethodInfo improveStats;
            private MethodInfo unlockPath;
            private MethodInfo unlockMeditationFocus;

            // PsycasterPathDef, AbilityExtension_Psycast, PsycastUtility.RecheckPaths
            private Type pathDefType;
            private Type psycastExtensionType;
            private MethodInfo recheckPaths;

            // PsySet
            private Type psySetType;
            private FieldInfo psySetName;
            private FieldInfo psySetAbilities;

            // VEF's CompAbilities, Ability and AbilityDef
            private Type compType;
            private FieldInfo learnedAbilities;
            private FieldInfo abilitiesToTick;
            private FieldInfo abilitiesToTickInterval;
            private MethodInfo giveAbility;
            private FieldInfo abilityDef;
            private Type abilityDefType;

            private static Vpe create()
            {
                Type implantType = hediffType;
                if (implantType == null) return null;

                try
                {
                    Vpe vpe = new Vpe
                    {
                        implantType = implantType,
                        experience = AccessTools.Field(implantType, "experience"),
                        points = AccessTools.Field(implantType, "points"),
                        statPoints = AccessTools.Field(implantType, "statPoints"),
                        maxLevelFromTitles = AccessTools.Field(implantType, "maxLevelFromTitles"),
                        unlockedPaths = AccessTools.Field(implantType, "unlockedPaths"),
                        previousUnlockedPaths = AccessTools.Field(implantType, "previousUnlockedPaths"),
                        unlockedMeditationFoci = AccessTools.Field(implantType, "unlockedMeditationFoci"),
                        psysets = AccessTools.Field(implantType, "psysets"),
                        psysetIndex = AccessTools.Field(implantType, "psysetIndex"),
                        improveStats = AccessTools.Method(implantType, "ImproveStats"),
                        unlockPath = AccessTools.Method(implantType, "UnlockPath"),
                        unlockMeditationFocus = AccessTools.Method(implantType, "UnlockMeditationFocus"),
                        psycastExtensionType = GenTypes.GetTypeInAnyAssembly("VanillaPsycastsExpanded.AbilityExtension_Psycast"),
                        recheckPaths = AccessTools.Method(GenTypes.GetTypeInAnyAssembly("VanillaPsycastsExpanded.PsycastUtility"), "RecheckPaths"),
                        // VEF has moved namespace before (VFECore.Abilities in older versions), so find its
                        // CompAbilities next to the class VPE's condition is built on, Hediff_Abilities
                        compType = GenTypes.GetTypeInAnyAssembly(implantType.BaseType?.Namespace + ".CompAbilities")
                    };

                    // The element types of the lists, e.g. PsycasterPathDef from List<PsycasterPathDef>
                    vpe.pathDefType = vpe.unlockedPaths?.FieldType.GetGenericArguments().FirstOrDefault();
                    vpe.psySetType = vpe.psysets?.FieldType.GetGenericArguments().FirstOrDefault();
                    if (vpe.psySetType != null)
                    {
                        vpe.psySetName = AccessTools.Field(vpe.psySetType, "Name");
                        vpe.psySetAbilities = AccessTools.Field(vpe.psySetType, "Abilities");
                    }

                    if (vpe.compType != null)
                    {
                        vpe.learnedAbilities = AccessTools.Field(vpe.compType, "learnedAbilities");
                        vpe.giveAbility = AccessTools.Method(vpe.compType, "GiveAbility");
                        // Only in newer VEF versions, so these two may be missing
                        vpe.abilitiesToTick = AccessTools.Field(vpe.compType, "abilitiesToTick");
                        vpe.abilitiesToTickInterval = AccessTools.Field(vpe.compType, "abilitiesToTickInterval");

                        Type abilityType = vpe.learnedAbilities?.FieldType.GetGenericArguments().FirstOrDefault();
                        if (abilityType != null) vpe.abilityDef = AccessTools.Field(abilityType, "def");
                        vpe.abilityDefType = vpe.abilityDef?.FieldType;
                    }

                    object[] required =
                    {
                        vpe.experience, vpe.points, vpe.statPoints, vpe.maxLevelFromTitles, vpe.unlockedPaths,
                        vpe.previousUnlockedPaths, vpe.unlockedMeditationFoci, vpe.psysets, vpe.psysetIndex,
                        vpe.improveStats, vpe.unlockPath, vpe.unlockMeditationFocus, vpe.psycastExtensionType,
                        vpe.recheckPaths, vpe.pathDefType, vpe.psySetType, vpe.psySetName, vpe.psySetAbilities,
                        vpe.compType, vpe.learnedAbilities, vpe.giveAbility, vpe.abilityDef, vpe.abilityDefType
                    };
                    if (required.Any(member => member == null))
                    {
                        throw new MissingMemberException("Some of the classes, fields or methods Phime uses have been renamed or removed");
                    }

                    return vpe;
                }
                catch (Exception e)
                {
                    Client.Instance.Log(new LogEventArgs(string.Format("Vanilla Psycasts Expanded is loaded, but not a version Phime understands, so traded colonists won't keep its psycasts: {0}", e.Message), LogLevel.WARNING));
                    return null;
                }
            }

            /// <summary>
            /// Whether this game has a psycaster path with the given def name.
            /// </summary>
            public bool HasPath(string defName) => GenDefDatabase.GetDefSilentFail(pathDefType, defName) != null;

            /// <summary>
            /// Whether this game has a VPE psycast with the given def name.
            /// </summary>
            public bool HasPsycast(string defName) => isPsycast(GenDefDatabase.GetDefSilentFail(abilityDefType, defName));

            /// <summary>
            /// Records a colonist's VPE progress and psycasts.
            /// </summary>
            /// <param name="pawn">Colonist to read from</param>
            /// <returns>Their progress, or null if they aren't a VPE psycaster</returns>
            public ProtoVpePsycasts ConvertFromVerse(Pawn pawn)
            {
                Hediff implant = findImplant(pawn);
                if (implant == null) return null;

                // GetValue hands back a plain "object", so it's cast to the type we know the field has
                ProtoVpePsycasts proto = new ProtoVpePsycasts
                {
                    Level = ((Hediff_Level) implant).level,
                    Experience = (float) experience.GetValue(implant),
                    Points = (int) points.GetValue(implant),
                    StatPoints = (int) statPoints.GetValue(implant),
                    PsySetIndex = (int) psysetIndex.GetValue(implant),
                    MaxLevelFromTitles = (int) maxLevelFromTitles.GetValue(implant)
                };

                proto.Paths.AddRange(defNames(unlockedPaths.GetValue(implant)));
                proto.LockedPaths.AddRange(defNames(previousUnlockedPaths.GetValue(implant)));
                proto.MeditationFoci.AddRange(defNames(unlockedMeditationFoci.GetValue(implant)));

                ThingComp comp = findComp(pawn);
                if (comp != null)
                {
                    proto.Abilities.AddRange(defNames(abilityDefs((IList) learnedAbilities.GetValue(comp)).Where(isPsycast)));
                }

                if (psysets.GetValue(implant) is IEnumerable psySetList)
                {
                    foreach (object psySet in psySetList)
                    {
                        ProtoPsySet protoPsySet = new ProtoPsySet { Name = (string) psySetName.GetValue(psySet) ?? "" };
                        protoPsySet.Abilities.AddRange(defNames(psySetAbilities.GetValue(psySet)));
                        proto.PsySets.Add(protoPsySet);
                    }
                }

                return proto;
            }

            /// <summary>
            /// Takes away every VPE psycast.
            /// </summary>
            /// <param name="pawn">Colonist to clear</param>
            public void RemovePsycasts(Pawn pawn)
            {
                ThingComp comp = findComp(pawn);
                if (comp == null) return;

                // VEF keeps the abilities that need updating every tick in lists of their own, so clear those too
                removePsycastsFrom(learnedAbilities.GetValue(comp) as IList);
                removePsycastsFrom(abilitiesToTick?.GetValue(comp) as IList);
                removePsycastsFrom(abilitiesToTickInterval?.GetValue(comp) as IList);
            }

            /// <summary>
            /// Puts back a colonist's VPE progress and psycasts. VPE adds its condition when the psylink is added, so
            /// that has to have happened already.
            /// </summary>
            /// <param name="pawn">Newly-generated colonist</param>
            /// <param name="sent">Progress from the trade</param>
            public void Apply(Pawn pawn, ProtoVpePsycasts sent)
            {
                Hediff implant = findImplant(pawn);
                if (implant == null)
                {
                    // The psylink didn't make it (PawnCheck warns about that), so there's nothing to put progress on
                    Client.Instance.Log(new LogEventArgs(string.Format("{0} arrived without a psylink, so their psycasts were left behind", pawn.LabelShort), LogLevel.WARNING));
                    return;
                }

                // Every psycast, path and meditation type cost a point, so each one this game doesn't have gives it back
                int refunds = 0;

                foreach (string defName in sent.Paths)
                {
                    Def path = GenDefDatabase.GetDefSilentFail(pathDefType, defName);
                    if (path == null) refunds += skipMissing("psycast path", defName);
                    else unlockPath.Invoke(implant, new object[] { path });
                }

                // Paths they unlocked but can't use right now. VPE gives them back once they qualify again
                IList lockedPaths = previousUnlockedPaths.GetValue(implant) as IList;
                foreach (string defName in sent.LockedPaths)
                {
                    Def path = GenDefDatabase.GetDefSilentFail(pathDefType, defName);
                    if (path == null || lockedPaths == null) refunds += skipMissing("psycast path", defName);
                    else lockedPaths.Add(path);
                }

                foreach (string defName in sent.MeditationFoci)
                {
                    MeditationFocusDef focus = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(defName);
                    if (focus == null) refunds += skipMissing("meditation type", defName);
                    else unlockMeditationFocus.Invoke(implant, new object[] { focus });
                }

                ThingComp comp = findComp(pawn);
                foreach (string defName in sent.Abilities)
                {
                    Def psycast = GenDefDatabase.GetDefSilentFail(abilityDefType, defName);
                    if (comp == null || !isPsycast(psycast)) refunds += skipMissing("psycast", defName);
                    else giveAbility.Invoke(comp, new object[] { psycast });
                }

                experience.SetValue(implant, sent.Experience);
                maxLevelFromTitles.SetValue(implant, sent.MaxLevelFromTitles);
                if (sent.StatPoints > 0) improveStats.Invoke(implant, new object[] { sent.StatPoints });

                // VPE's level comes from the psylink's, which HediffConverter capped to this game's maximum. Each level
                // that took away also takes away its point
                int levelsLost = Math.Max(0, sent.Level - ((Hediff_Level) implant).level);
                points.SetValue(implant, Math.Max(0, sent.Points + refunds - levelsLost));

                applyPsySets(implant, sent);

                // Lock any path they no longer qualify for, e.g. one that needs a meme: traded colonists take on our ideology
                recheckPaths.Invoke(null, new object[] { pawn });
            }

            /// <summary>
            /// Puts back the named groups of psycasts, leaving out any psycast this game doesn't have.
            /// </summary>
            private void applyPsySets(Hediff implant, ProtoVpePsycasts sent)
            {
                if (!(psysets.GetValue(implant) is IList psySetList)) return;

                foreach (ProtoPsySet protoPsySet in sent.PsySets)
                {
                    // Activator.CreateInstance is reflection's "new PsySet()"
                    object psySet = Activator.CreateInstance(psySetType);
                    psySetName.SetValue(psySet, protoPsySet.Name);

                    // A HashSet has no non-generic interface to add through, so its Add method is called by name
                    object abilities = psySetAbilities.GetValue(psySet);
                    if (abilities == null)
                    {
                        abilities = Activator.CreateInstance(psySetAbilities.FieldType);
                        psySetAbilities.SetValue(psySet, abilities);
                    }
                    MethodInfo add = abilities.GetType().GetMethod("Add");

                    foreach (string defName in protoPsySet.Abilities)
                    {
                        Def psycast = GenDefDatabase.GetDefSilentFail(abilityDefType, defName);
                        if (psycast != null) add.Invoke(abilities, new object[] { psycast });
                    }

                    psySetList.Add(psySet);
                }

                // An index equal to the number of psysets means "show all psycasts"
                psysetIndex.SetValue(implant, Mathf.Clamp(sent.PsySetIndex, 0, psySetList.Count));
            }

            private Hediff findImplant(Pawn pawn) => pawn.health?.hediffSet?.hediffs.FirstOrDefault(hediff => implantType.IsInstanceOfType(hediff));

            private ThingComp findComp(Pawn pawn) => pawn.AllComps?.FirstOrDefault(comp => compType.IsInstanceOfType(comp));

            /// <summary>
            /// Whether a VEF ability def is one of VPE's psycasts.
            /// </summary>
            private bool isPsycast(Def def) => def?.modExtensions != null && def.modExtensions.Any(extension => psycastExtensionType.IsInstanceOfType(extension));

            /// <summary>
            /// The defs of a list of VEF abilities.
            /// </summary>
            private IEnumerable<Def> abilityDefs(IList abilities)
            {
                if (abilities == null) return Enumerable.Empty<Def>();
                return abilities.Cast<object>().Select(ability => abilityDef.GetValue(ability) as Def).Where(def => def != null);
            }

            private void removePsycastsFrom(IList abilities)
            {
                if (abilities == null) return;

                // Counting down means removing one doesn't shift the ones still to check
                for (int i = abilities.Count - 1; i >= 0; i--)
                {
                    if (isPsycast(abilityDef.GetValue(abilities[i]) as Def)) abilities.RemoveAt(i);
                }
            }

            /// <summary>
            /// Logs something this game doesn't have.
            /// </summary>
            /// <returns>The point it gives back</returns>
            private static int skipMissing(string kind, string defName)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Skipping unknown {0} '{1}' on traded colonist, giving its point back", kind, defName), LogLevel.WARNING));
                return 1;
            }

            /// <summary>
            /// The def names in a list (or set) of defs.
            /// </summary>
            private static IEnumerable<string> defNames(object defs)
            {
                if (!(defs is IEnumerable list)) return Enumerable.Empty<string>();
                return list.OfType<Def>().Select(def => def.defName);
            }

            private static IEnumerable<string> defNames(IEnumerable<Def> defs) => defs.Select(def => def.defName);
        }
    }
}
