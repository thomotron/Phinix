using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// The trade drop spot. There's only ever one per game: placing a new one, on any map, removes the old one.
    /// </summary>
    public class DropSpot : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad) return;

            List<Thing> others = Find.Maps
                .SelectMany(other => other.listerThings.ThingsOfDef(def))
                .Where(thing => thing != this && thing.Spawned)
                .ToList();
            foreach (Thing other in others) other.Destroy(DestroyMode.Vanish);
        }
    }
}
