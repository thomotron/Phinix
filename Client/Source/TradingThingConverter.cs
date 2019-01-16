using System;
using System.Linq;
using RimWorld;
using Trading;
using Verse;

namespace PhinixClient
{
    public static class TradingThingConverter
    {
        /// <summary>
        /// Converts a <c>Verse.Thing</c> into a <c>Trading.Thing</c>.
        /// Used for preparing a <c>Verse.Thing</c> for transport.
        /// </summary>
        /// <param name="verseThing">Thing to convert</param>
        /// <returns>Converted thing</returns>
        public static Trading.ProtoThing ConvertThingFromVerse(Verse.Thing verseThing)
        {
            // Try get the quality of the thing, failure defaulting to none
            Quality quality = verseThing.TryGetQuality(out QualityCategory gottenQuality) ? (Quality) gottenQuality : Quality.None;
            
            // Create a Trading.Thing with attributes from verseThing
            Trading.ProtoThing protoThing = new Trading.ProtoThing
            {
                DefName = verseThing.def.defName,
                StackCount = verseThing.stackCount,
                HitPoints = verseThing.HitPoints,
                Quality = quality
            };
            
            // Check if verseThing has stuff
            if (verseThing.Stuff?.defName != null)
            {
                // Set protoThing's stuff def
                protoThing.StuffDefName = verseThing.Stuff.defName;
            }

            // Check if verseThing is minified
            if (verseThing is MinifiedThing minifiedVerseThing)
            {
                // Set protoThing's inner thing
                protoThing.InnerProtoThing = ConvertThingFromVerse(minifiedVerseThing.InnerThing);
            }
            
            // Return constructed protoThing
            return protoThing;
        }
        
        /// <summary>
        /// Converts a <c>Trading.Thing</c> into a <c>Verse.Thing</c>.
        /// Used for unloading a <c>Trading.Thing</c> after transport.
        /// </summary>
        /// <param name="protoThing">Thing to convert</param>
        /// <returns>Converted thing</returns>
        /// <exception cref="InvalidOperationException">Could not find a single def that matches Trading.Thing def name</exception>
        /// <exception cref="InvalidOperationException">Could not find a single def that matches Trading.Thing stuff def name</exception>
        public static Verse.Thing ConvertThingFromProto(Trading.ProtoThing protoThing)
        {
            // Try to get the ThingDef for protoThing
            ThingDef thingDef;
            try
            {
                thingDef = DefDatabase<ThingDef>.AllDefs.Single(def => def.defName == protoThing.DefName);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(string.Format("Could not find a single def that matches def name '{0}'", protoThing.DefName), e);
            }

            // Try to get the ThingDef for stuff inside protoThing
            ThingDef stuffDef = null;
            try
            {
                // Check if we actually have stuff inside the thing
                if (!string.IsNullOrEmpty(protoThing.StuffDefName))
                {
                    stuffDef = DefDatabase<ThingDef>.AllDefs.Single(def => def.defName == protoThing.StuffDefName);
                }
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(string.Format("Could not find a single def that matches stuff def name '{0}'", protoThing.StuffDefName), e);
            }

            // Make our base verseThing and give it protoThing's stack count and hit points
            Verse.Thing verseThing = ThingMaker.MakeThing(thingDef, stuffDef);
            verseThing.stackCount = protoThing.StackCount;
            verseThing.HitPoints = protoThing.HitPoints;
            
            // Check if verseThing should have its quality set
            if (protoThing.Quality != Quality.None)
            {
                // Set verseThing's quality if it is capable of having a quality
                // Art generation should be that of an outsider given that this is a traded item
                verseThing.TryGetComp<CompQuality>()?.SetQuality((QualityCategory) protoThing.Quality, ArtGenerationContext.Outsider);
            }
            
            // Check if verseThing is minified
            if (verseThing is MinifiedThing minifiedVerseThing)
            {
                // Set verseThing's inner thing to protoThing's inner thing
                minifiedVerseThing.InnerThing = protoThing.InnerProtoThing != null ? ConvertThingFromProto(protoThing.InnerProtoThing) : null;
            }
            
            // Return the constructed Verse.Thing
            return verseThing;
        }

        /// <summary>
        /// Converts a <c>Trading.Thing</c> into a <c>Verse.Thing</c>, resorting to an unknown item if the conversion fails.
        /// Used for safely unloading a <c>Trading.Thing</c> after transport.
        /// </summary>
        /// <param name="protoThing">Thing to convert</param>
        /// <returns>Converted thing</returns>
        public static Verse.Thing ConvertThingFromProtoOrUnknown(ProtoThing protoThing)
        {
            try
            {
                // Try converting normally
                return ConvertThingFromProto(protoThing);
            }
            catch (InvalidOperationException)
            {
                // Normal conversion failed, crack out the unknown item def
                ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.Single(def => def.defName == "UnknownItem");
    
                // Make our base item and give it protoThing's stack count and hit points
                Verse.Thing verseThing = ThingMaker.MakeThing(thingDef);
                verseThing.stackCount = protoThing.StackCount;
                verseThing.HitPoints = protoThing.HitPoints;
                
                // Return the constructed Verse.Thing
                return verseThing;
            }
        }
    }
}