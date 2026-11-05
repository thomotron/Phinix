using System;
using System.Collections.Generic;
using RimWorld;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Draws colonists the way the colonist bar does (head and shoulders), and animals as themselves, so players can
    /// tell who's who in a trade.
    /// </summary>
    /// <remarks>
    /// A colonist or animal on offer only exists as its trade data, so a temporary copy is built to draw it, once, and
    /// thrown away again by <see cref="Clear"/> when the window closes (the same as the pawn inspector does).
    /// </remarks>
    public class PawnPortraits
    {
        private readonly Dictionary<string, Pawn> built = new Dictionary<string, Pawn>();

        /// <summary>Whether a copy has been built this frame already, so opening a big trade doesn't hitch</summary>
        private int lastBuildFrame = -1;

        /// <summary>
        /// Draws a row's icon: a portrait for a colonist, the animal itself for an animal.
        /// </summary>
        /// <returns>Whether it drew anything (false for a row of items)</returns>
        public bool Draw(Rect rect, StackedThings stack)
        {
            Pawn pawn = null;
            if (stack.DisplayPawn != null) pawn = copyOf(stack.DisplayPawn);
            else if (stack.Things.Count > 0) pawn = stack.Things[0] as Pawn;
            if (pawn == null) return false;

            if (pawn.RaceProps.Humanlike)
            {
                RenderTexture portrait = PortraitsCache.Get(pawn, ColonistBarColonistDrawer.PawnTextureSize, Rot4.South, ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.28205f);
                // The colonist bar's portraits are taller than wide; fit one into the row's square
                float width = rect.height * ColonistBarColonistDrawer.PawnTextureSize.x / ColonistBarColonistDrawer.PawnTextureSize.y;
                UnityEngine.GUI.DrawTexture(new Rect(rect.center.x - width / 2f, rect.y, width, rect.height), portrait);
            }
            else
            {
                Widgets.ThingIcon(rect, pawn);
            }
            return true;
        }

        /// <summary>
        /// Throws away every copy built for drawing.
        /// </summary>
        public void Clear()
        {
            foreach (Pawn pawn in built.Values)
            {
                if (pawn != null && !pawn.Destroyed && !pawn.Discarded)
                {
                    Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                }
            }
            built.Clear();
        }

        private Pawn copyOf(ProtoThing protoThing)
        {
            string key = TradeRequests.KeyOf(protoThing);
            if (built.TryGetValue(key, out Pawn pawn)) return pawn;
            if (lastBuildFrame == Time.frameCount) return null;
            lastBuildFrame = Time.frameCount;

            try
            {
                pawn = TradingThingConverter.ConvertThingFromProto(protoThing) as Pawn;
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs("Couldn't build a portrait of an offered pawn: " + e.Message, LogLevel.DEBUG));
                pawn = null;
            }
            // Remembered even when it failed, so it isn't tried again every frame
            built[key] = pawn;
            return pawn;
        }
    }
}
