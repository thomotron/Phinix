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
    /// A card describing an animal or colonist someone is offering (in a trade or a market), so the player can see who
    /// or what they'd be getting before they accept: portrait, age, backstory, traits, skills, health, genes and gear for
    /// a colonist; training, health and bond for an animal.
    /// </summary>
    /// <remarks>
    /// Everything shown comes from the <see cref="ProtoThing"/> that was sent. For the portrait (and nicer health labels)
    /// the window also builds a temporary copy of the pawn once, the same way a finished trade would, and throws it away
    /// when the window closes. If that copy can't be built (e.g. the race comes from a mod we don't have), the card still
    /// shows everything else.
    /// </remarks>
    public class PawnInspectWindow : Window
    {
        private const float SPACING = 10f;
        private const float PORTRAIT_WIDTH = 150f;
        private const float PORTRAIT_HEIGHT = 190f;
        private const float HEADING_HEIGHT = 26f;
        private const float LINE_HEIGHT = 22f;
        private const float SKILL_LABEL_WIDTH = 110f;
        private const float SKILL_BAR_WIDTH = 150f;
        private const float SCROLLBAR_WIDTH = 16f;

        public override Vector2 InitialSize => new Vector2(660f, 680f);

        private readonly ProtoThing protoThing;

        /// <summary>
        /// The temporary copy of the pawn, built on first draw. Null until then, or if building it failed.
        /// </summary>
        private Pawn preview;
        private bool previewAttempted;

        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Height of everything below the header, measured on the last frame so the scroll view knows how far to go.
        /// </summary>
        private float contentHeight = 600f;

        /// <summary>
        /// Parts of this pawn our game doesn't have (see <see cref="PawnCheck"/>), worked out once when the window opens.
        /// </summary>
        private readonly List<string> missingParts;

        private static readonly Texture2D PassionMinorIcon = ContentFinder<Texture2D>.Get("UI/Icons/PassionMinor");
        private static readonly Texture2D PassionMajorIcon = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor");

        /// <param name="protoThing">The animal or colonist to describe (its Animal or Colonist must be set)</param>
        public PawnInspectWindow(ProtoThing protoThing)
        {
            this.protoThing = protoThing;
            missingParts = PawnCheck.MissingParts(protoThing);

            doCloseX = true;
            doCloseButton = false;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            draggable = true;
        }

        /// <summary>
        /// Builds the temporary pawn the first time it's needed. Has to happen on the main thread, which drawing is.
        /// </summary>
        private void buildPreview()
        {
            if (previewAttempted) return;
            previewAttempted = true;

            try
            {
                preview = TradingThingConverter.ConvertThingFromProto(protoThing) as Pawn;
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs("Couldn't build a preview of an offered pawn: " + e.Message, LogLevel.DEBUG));
                preview = null;
            }
        }

        public override void PostClose()
        {
            base.PostClose();

            // Throw the temporary copy away the same way a traded-away pawn is (see ItemEscrow.letGo), so nothing is
            // left pointing at it
            if (preview != null && !preview.Destroyed && !preview.Discarded)
            {
                Find.WorldPawns.PassToWorld(preview, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }
            preview = null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            buildPreview();

            ProtoColonist colonist = protoThing.Colonist;
            ProtoAnimal animal = protoThing.Animal;
            if (colonist == null && animal == null)
            {
                Widgets.NoneLabelCenteredVertically(inRect, "Phinix_pawn_notAPawn".Translate());
                return;
            }

            // Header: portrait on the left, name and the basics beside it
            Rect portraitRect = new Rect(inRect.x, inRect.y, PORTRAIT_WIDTH, PORTRAIT_HEIGHT);
            drawPortrait(portraitRect);

            float x = portraitRect.xMax + SPACING;
            float width = inRect.xMax - x;
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(x, y, width, 34f), colonist != null ? fullName(colonist) : StackedThings.PawnLabel(protoThing));
            Text.Font = GameFont.Small;
            y += 36f;

            foreach (string line in colonist != null ? colonistBasics(colonist) : animalBasics(animal))
            {
                Widgets.Label(new Rect(x, y, width, LINE_HEIGHT), line);
                y += LINE_HEIGHT;
            }

            if (!ShopItems.IsIdentifiable(protoThing))
            {
                UnityEngine.GUI.color = ColorLibrary.RedReadable;
                Widgets.Label(new Rect(x, y, width, LINE_HEIGHT * 2), "Phinix_pawn_unidentified".Translate());
                UnityEngine.GUI.color = Color.white;
            }

            // Everything else scrolls underneath
            float top = Math.Max(portraitRect.yMax, y) + SPACING;
            Rect outRect = new Rect(inRect.x, top, inRect.width, inRect.yMax - top);
            Rect viewRect = new Rect(0f, 0f, outRect.width - SCROLLBAR_WIDTH, contentHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float contentY = 0f;
            drawMissingParts(viewRect.width, ref contentY);
            if (colonist != null) drawColonist(viewRect.width, ref contentY, colonist);
            else drawAnimal(viewRect.width, ref contentY, animal);
            Widgets.EndScrollView();
            contentHeight = contentY + SPACING;
        }

        #region Header

        /// <summary>
        /// Draws the pawn's portrait if we could build a preview, or its race's icon if not.
        /// </summary>
        private void drawPortrait(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(4f);

            if (preview != null && preview.RaceProps.Humanlike)
            {
                RenderTexture portrait = PortraitsCache.Get(preview, new Vector2(inner.width, inner.height), Rot4.South);
                UnityEngine.GUI.DrawTexture(inner, portrait);
                return;
            }

            if (preview != null)
            {
                Widgets.ThingIcon(inner, preview);
                return;
            }

            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(StackedThings.PawnDefName(protoThing));
            if (raceDef != null) Widgets.ThingIcon(inner, raceDef);
            else Widgets.NoneLabelCenteredVertically(inner, "?");
        }

        /// <summary>
        /// "First 'Nick' Last", like the game's character card.
        /// </summary>
        private static string fullName(ProtoColonist colonist)
        {
            if (string.IsNullOrEmpty(colonist.FirstName) && string.IsNullOrEmpty(colonist.LastName)) return colonist.NickName;

            string nick = string.IsNullOrEmpty(colonist.NickName) || colonist.NickName == colonist.FirstName ? "" : " '" + colonist.NickName + "'";
            return (colonist.FirstName + nick + " " + colonist.LastName).Trim();
        }

        /// <summary>
        /// The colonist's one-line facts: sex, race and xenotype, age, and what they're worth.
        /// </summary>
        private IEnumerable<string> colonistBasics(ProtoColonist colonist)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(colonist.DefName);
            string race = raceDef?.label ?? colonist.DefName;
            string xenotype = xenotypeLabel(colonist);
            string gender = (colonist.IsFemale ? Gender.Female : Gender.Male).GetLabel();

            yield return (string.IsNullOrEmpty(xenotype) ? gender + " " + race : gender + " " + xenotype + " " + race).CapitalizeFirst();
            yield return ageLine(colonist.BiologicalAgeTicks, colonist.ChronologicalAgeTicks);
            if (preview != null) yield return "Phinix_pawn_value".Translate(Mathf.RoundToInt(preview.MarketValue));
        }

        /// <summary>
        /// The animal's one-line facts: sex and species, age, bond, and what it's worth.
        /// </summary>
        private IEnumerable<string> animalBasics(ProtoAnimal animal)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(animal.DefName);
            string species = raceDef?.label ?? animal.DefName;
            string gender = (animal.IsFemale ? Gender.Female : Gender.Male).GetLabel();

            yield return (gender + " " + species).CapitalizeFirst();
            yield return ageLine(animal.BiologicalAgeTicks, animal.ChronologicalAgeTicks);
            if (!string.IsNullOrEmpty(animal.BondedPawnName)) yield return "Phinix_pawn_bondedTo".Translate(animal.BondedPawnName);
            if (preview != null) yield return "Phinix_pawn_value".Translate(Mathf.RoundToInt(preview.MarketValue));
        }

        /// <summary>
        /// "Age 34 (born 51 years ago)", or just "Age 34" when the two match.
        /// </summary>
        private static string ageLine(long biologicalTicks, long chronologicalTicks)
        {
            int biological = (int) (biologicalTicks / GenDate.TicksPerYear);
            int chronological = (int) (chronologicalTicks / GenDate.TicksPerYear);
            return chronological > biological
                ? "Phinix_pawn_ageBoth".Translate(biological, chronological)
                : "Phinix_pawn_age".Translate(biological);
        }

        private static string xenotypeLabel(ProtoColonist colonist)
        {
            if (!string.IsNullOrEmpty(colonist.CustomXenotypeName)) return colonist.CustomXenotypeName;
            XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(colonist.XenotypeDefName);
            return xenotype == null || xenotype == XenotypeDefOf.Baseliner ? "" : xenotype.label;
        }

        #endregion

        /// <summary>
        /// A red section listing what won't arrive with this pawn because our game doesn't have it. Drawn first, so
        /// nobody misses it.
        /// </summary>
        private void drawMissingParts(float width, ref float y)
        {
            if (missingParts.Count == 0) return;

            heading(width, ref y, "Phinix_pawnCheck_heading");
            UnityEngine.GUI.color = ColorLibrary.RedReadable;
            paragraph(width, ref y, "Phinix_pawnCheck_explanation".Translate());
            foreach (string part in missingParts) line(width, ref y, part);
            UnityEngine.GUI.color = Color.white;
        }

        #region Colonist

        private void drawColonist(float width, ref float y, ProtoColonist colonist)
        {
            Gender gender = colonist.IsFemale ? Gender.Female : Gender.Male;

            // Backstory
            heading(width, ref y, "Phinix_pawn_backstory");
            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail(colonist.ChildhoodDefName);
            BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail(colonist.AdulthoodDefName);
            backstoryLine(width, ref y, "Phinix_pawn_childhood", childhood, gender);
            backstoryLine(width, ref y, "Phinix_pawn_adulthood", adulthood, gender);
            if (childhood == null && adulthood == null) line(width, ref y, "Phinix_pawn_none".Translate(), gray: true);

            // Traits, wrapped onto as many lines as they need, each with its description on hover
            heading(width, ref y, "Phinix_pawn_traits");
            List<(string Label, string Tip)> traits = colonist.Traits.Select(traitLabel).ToList();
            if (traits.Count == 0) line(width, ref y, "Phinix_pawn_none".Translate(), gray: true);
            else chips(width, ref y, traits);

            // Skills, with a bar and passion flames like the character card
            heading(width, ref y, "Phinix_pawn_skills");
            foreach (ProtoSkill skill in colonist.Skills
                         .Select(s => (Skill: s, Def: DefDatabase<SkillDef>.GetNamedSilentFail(s.DefName)))
                         .Where(pair => pair.Def != null)
                         .OrderByDescending(pair => pair.Def.listOrder)
                         .Select(pair => pair.Skill))
            {
                skillRow(width, ref y, skill);
            }

            // Health
            heading(width, ref y, "Phinix_pawn_health");
            healthLines(width, ref y, colonist.Hediffs);

            // Genes (Biotech)
            if (colonist.Xenogenes.Count > 0 || colonist.Endogenes.Count > 0)
            {
                heading(width, ref y, "Phinix_pawn_genes");
                string xenotype = xenotypeLabel(colonist);
                if (!string.IsNullOrEmpty(xenotype)) line(width, ref y, "Phinix_pawn_xenotype".Translate(xenotype.CapitalizeFirst()));
                paragraph(width, ref y, string.Join(", ", colonist.Xenogenes.Concat(colonist.Endogenes)
                    .Select(defName => DefDatabase<GeneDef>.GetNamedSilentFail(defName)?.LabelCap.ToString() ?? defName)
                    .Distinct()));
            }

            // Gear they bring with them
            List<ProtoThing> gear = colonist.Equipment.Concat(colonist.Apparel).Concat(colonist.Inventory).ToList();
            heading(width, ref y, "Phinix_pawn_gear");
            if (gear.Count == 0) line(width, ref y, "Phinix_pawn_none".Translate(), gray: true);
            foreach (ProtoThing item in gear)
            {
                line(width, ref y, (item.StackCount > 1 ? item.StackCount + "x " : "") + ShopItems.Label(item));
            }
        }

        private void backstoryLine(float width, ref float y, string key, BackstoryDef backstory, Gender gender)
        {
            if (backstory == null) return;

            Rect rect = new Rect(0f, y, width, LINE_HEIGHT);
            Widgets.Label(rect, key.Translate(backstory.TitleCapFor(gender)));
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, preview != null ? backstory.FullDescriptionFor(preview).Resolve() : backstory.TitleCapFor(gender).ToString());
            }
            y += LINE_HEIGHT;
        }

        /// <summary>
        /// A trait's name, and its description from the preview pawn if we have one.
        /// </summary>
        private (string Label, string Tip) traitLabel(ProtoTrait protoTrait)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(protoTrait.DefName);
            if (def == null) return (protoTrait.DefName, "Phinix_pawn_unknownDef".Translate());

            Trait onPreview = preview?.story?.traits?.allTraits.FirstOrDefault(trait => trait.def == def && trait.Degree == protoTrait.Degree);
            if (onPreview != null) return (onPreview.LabelCap, onPreview.TipString(preview));

            TraitDegreeData degree = def.DataAtDegree(protoTrait.Degree);
            return (degree.label.CapitalizeFirst(), degree.description ?? "");
        }

        private void skillRow(float width, ref float y, ProtoSkill skill)
        {
            SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(skill.DefName);
            SkillRecord onPreview = def != null ? preview?.skills?.GetSkill(def) : null;
            bool disabled = onPreview != null && onPreview.TotallyDisabled;

            Rect row = new Rect(0f, y, width, LINE_HEIGHT);
            Rect labelRect = new Rect(row.x, row.y, SKILL_LABEL_WIDTH, row.height);
            Rect passionRect = new Rect(labelRect.xMax, row.y + 2f, LINE_HEIGHT - 4f, LINE_HEIGHT - 4f);
            Rect barRect = new Rect(passionRect.xMax + 6f, row.y + 3f, SKILL_BAR_WIDTH, row.height - 6f);
            Rect levelRect = new Rect(barRect.xMax + 6f, row.y, 60f, row.height);

            Widgets.Label(labelRect, def?.skillLabel.CapitalizeFirst() ?? skill.DefName);

            // 1 = minor passion, 2 = major. Modded passions use other numbers, which get a plain "+"
            if (skill.Passion == 1) UnityEngine.GUI.DrawTexture(passionRect, PassionMinorIcon);
            else if (skill.Passion == 2) UnityEngine.GUI.DrawTexture(passionRect, PassionMajorIcon);
            else if (skill.Passion > 2) Widgets.Label(passionRect, "+");

            if (disabled)
            {
                UnityEngine.GUI.color = Color.gray;
                Widgets.Label(new Rect(barRect.x, row.y, barRect.width + levelRect.width, row.height), "Phinix_pawn_incapable".Translate());
                UnityEngine.GUI.color = Color.white;
            }
            else
            {
                Widgets.FillableBar(barRect, Mathf.Clamp01(skill.Level / (float) SkillRecord.MaxLevel));
                Widgets.Label(levelRect, skill.Level.ToString());
            }

            y += LINE_HEIGHT;
        }

        #endregion

        #region Animal

        private void drawAnimal(float width, ref float y, ProtoAnimal animal)
        {
            // Training: each trick with how many of its steps are learned
            heading(width, ref y, "Phinix_pawn_training");
            List<string> tricks = animal.TrainingSteps
                .Select(entry => (Def: DefDatabase<TrainableDef>.GetNamedSilentFail(entry.Key), Steps: entry.Value, Name: entry.Key))
                .OrderByDescending(trick => trick.Def?.listPriority ?? 0)
                .Select(trick => trick.Def != null
                    ? "Phinix_pawn_trick".Translate(trick.Def.LabelCap, Math.Min(trick.Steps, trick.Def.steps), trick.Def.steps).ToString()
                    : trick.Name + " " + trick.Steps)
                .ToList();
            if (tricks.Count == 0) line(width, ref y, "Phinix_pawn_untrained".Translate(), gray: true);
            foreach (string trick in tricks) line(width, ref y, trick);

            heading(width, ref y, "Phinix_pawn_health");
            healthLines(width, ref y, animal.Hediffs);
        }

        #endregion

        #region Shared drawing

        /// <summary>
        /// Health conditions. Uses the preview pawn's own labels when we have it (they know which side a body part is
        /// on); otherwise builds them from the sent data.
        /// </summary>
        private void healthLines(float width, ref float y, IList<ProtoHediff> hediffs)
        {
            List<(string Label, bool Bad)> lines;
            if (preview?.health?.hediffSet != null)
            {
                lines = preview.health.hediffSet.hediffs
                    .Where(hediff => hediff.Visible)
                    .Select(hediff => ((hediff.Part != null ? hediff.Part.LabelCap + ": " : "") + hediff.LabelCap, hediff.def.isBad))
                    .ToList();
            }
            else
            {
                lines = hediffs.Select(protoHediff =>
                {
                    HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(protoHediff.DefName);
                    BodyPartDef part = DefDatabase<BodyPartDef>.GetNamedSilentFail(protoHediff.BodyPartDefName);
                    string label = (part != null ? part.LabelCap.ToString() + ": " : "") + (def?.LabelCap.ToString() ?? protoHediff.DefName);
                    return (label, def?.isBad ?? true);
                }).ToList();
            }

            if (lines.Count == 0)
            {
                line(width, ref y, "Phinix_pawn_healthy".Translate(), gray: true);
                return;
            }

            foreach ((string label, bool bad) in lines)
            {
                if (bad) UnityEngine.GUI.color = new Color(1f, 0.75f, 0.6f);
                line(width, ref y, label);
                UnityEngine.GUI.color = Color.white;
            }
        }

        private static void heading(float width, ref float y, string key)
        {
            y += 4f;
            Rect rect = new Rect(0f, y, width, HEADING_HEIGHT);
            Text.Font = GameFont.Small;
            UnityEngine.GUI.color = new Color(0.85f, 0.85f, 0.6f);
            Widgets.Label(rect, key.Translate());
            UnityEngine.GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 3f, width);
            y += HEADING_HEIGHT;
        }

        private static void line(float width, ref float y, string text, bool gray = false)
        {
            if (gray) UnityEngine.GUI.color = Color.gray;
            Widgets.Label(new Rect(8f, y, width - 8f, LINE_HEIGHT), text);
            if (gray) UnityEngine.GUI.color = Color.white;
            y += LINE_HEIGHT;
        }

        private static void paragraph(float width, ref float y, string text)
        {
            float height = Text.CalcHeight(text, width - 8f);
            Widgets.Label(new Rect(8f, y, width - 8f, height), text);
            y += height;
        }

        /// <summary>
        /// Small boxes laid out left to right, wrapping onto new lines, each with a tooltip. Like the traits on the
        /// game's character card.
        /// </summary>
        private static void chips(float width, ref float y, List<(string Label, string Tip)> items)
        {
            float x = 8f;
            foreach ((string label, string tip) in items)
            {
                float chipWidth = Text.CalcSize(label).x + 12f;
                if (x + chipWidth > width && x > 8f)
                {
                    x = 8f;
                    y += LINE_HEIGHT + 4f;
                }

                Rect chip = new Rect(x, y, chipWidth, LINE_HEIGHT);
                Widgets.DrawBoxSolid(chip, new Color(1f, 1f, 1f, 0.08f));
                if (Mouse.IsOver(chip)) Widgets.DrawHighlight(chip);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(chip, label);
                Text.Anchor = TextAnchor.UpperLeft;
                if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(chip, tip);

                x += chipWidth + 4f;
            }
            y += LINE_HEIGHT + 4f;
        }

        #endregion
    }
}
