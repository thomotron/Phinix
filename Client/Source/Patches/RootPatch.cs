using HarmonyLib;
using Verse;

namespace PhinixClient.Patches
{
    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    internal class RootPatch
    {
        [HarmonyPostfix]
        private static void Update()
        {
            Client.Instance?.Update();
        }
    }
}
