using HarmonyLib;
using RimWorld;
using Verse;

namespace PhinixClient.Patches
{
    /// <summary>
    /// Shows the art description that arrived with a traded item, instead of the one the game would make up from a tale
    /// it doesn't have (see <see cref="ItemArt"/>).
    /// </summary>
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.GenerateImageDescription))]
    internal class CompArtDescriptionPatch
    {
        // A prefix that returns false skips the game's own method, and __result becomes what it returns
        [HarmonyPrefix]
        private static bool Prefix(CompArt __instance, ref TaggedString __result)
        {
            string description = Current.Game?.GetComponent<ArtDescriptionsGameComponent>()?.Get(__instance.parent);
            if (description == null) return true;

            __result = description;
            return false;
        }
    }
}
