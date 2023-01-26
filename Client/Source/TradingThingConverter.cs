using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Trading;
using Utils;
using Verse;

namespace PhinixClient
{
    public static class TradingThingConverter
    {
        // Extension method signatures for the three conversions below
        /// <inheritdoc cref="ConvertThingFromVerse"/>
        public static ProtoThing ConvertToProto(this Verse.Thing verseThing) => ConvertThingFromVerse(verseThing);
        /// <inheritdoc cref="ConvertThingFromProto"/>
        public static Verse.Thing ConvertToVerse(this ProtoThing protoThing) => ConvertThingFromProto(protoThing);
        /// <inheritdoc cref="ConvertThingFromProtoOrUnknown"/>
        public static Verse.Thing ConvertToVerseOrUnknown(this ProtoThing protoThing) => ConvertThingFromProtoOrUnknown(protoThing);

        // Collection extension method signatures similar to the above
        /// <inheritdoc cref="ConvertThingFromVerse"/>
        public static IEnumerable<ProtoThing> ConvertToProto(this IEnumerable<Verse.Thing> verseThings) => verseThings.Select(ConvertThingFromVerse);
        /// <inheritdoc cref="ConvertThingFromProto"/>
        public static IEnumerable<Verse.Thing> ConvertToVerse(this IEnumerable<ProtoThing> protoThings) => protoThings.Select(ConvertThingFromProto);
        /// <inheritdoc cref="ConvertThingFromProtoOrUnknown"/>
        public static IEnumerable<Verse.Thing> ConvertToVerseOrUnknown(this IEnumerable<ProtoThing> protoThings) => protoThings.Select(ConvertThingFromProtoOrUnknown);

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
                UnknownItem verseThing = (UnknownItem) ThingMaker.MakeThing(thingDef);
                verseThing.stackCount = protoThing.StackCount;
                verseThing.HitPoints = protoThing.HitPoints;

                // Set the original label to protoThing's def name
                verseThing.OriginalLabel = getInnerDefName(protoThing);

                // Return the constructed Verse.Thing
                return verseThing;
            }
        }

        /// <summary>
        /// Converts a <see cref="Pawn"/> into a <see cref="ProtoPawn"/>.
        /// Used for preparing a <see cref="Pawn"/> for transport.
        /// </summary>
        /// <param name="pawn">Pawn to convert</param>
        /// <returns>Converted pawn</returns>
        public static ProtoPawn ConvertPawnFromVerse(Pawn pawn)
        {
            ProtoPawn protoPawn = new ProtoPawn()
            {
                DefName = pawn.kindDef.defName,
                Gender = (Trading.Gender) pawn.gender,
                Name = pawn.Name.ToStringFull,
                BiologicalTicks = pawn.ageTracker.AgeBiologicalTicks,
                ChronologicalTicks = pawn.ageTracker.AgeChronologicalTicks
            };

            return protoPawn;
        }

        /// <summary>
        /// Converts a <see cref="ProtoPawn"/> into a <see cref="Pawn"/>.
        /// Used for safely unloading a <see cref="ProtoPawn"/> after transport.
        /// </summary>
        /// <param name="protoPawn">Pawn to convert</param>
        /// <returns>Converted pawn</returns>
        /// <exception cref="InvalidOperationException">Could not find a single def that matches ProtoPawn def name</exception>
        public static Pawn ConvertPawnFromProto(ProtoPawn protoPawn)
        {
            // Try to get the ThingDef for protoThing
            PawnKindDef pawnDef;
            try
            {
                pawnDef = DefDatabase<PawnKindDef>.AllDefs.Single(def => def.defName == protoPawn.DefName);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(string.Format("Could not find a single def that matches def name '{0}'", protoPawn.DefName), e);
            }

            // Make our base pawn and give it protoPawn's def and associated details
            Pawn pawn = new Pawn
            {
                kindDef = pawnDef,
                Name = new NameSingle(protoPawn.Name),
                gender = (Verse.Gender) protoPawn.Gender
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn)
            {
                AgeBiologicalTicks = protoPawn.BiologicalTicks,
                AgeChronologicalTicks = protoPawn.ChronologicalTicks
            };

            return pawn;
        }

        /// <summary>
        /// Compares two <see cref="Thing"/>s for equality in a trading context. This method only checks the necessary
        /// fields which are handled as part of the trade serialisation/deserialisation process, and cannot guarantee
        /// that two <see cref="Thing"/>s are equal in a game context.
        /// </summary>
        /// <param name="thing">First thing to compare</param>
        /// <param name="other">Second thing to compare</param>
        /// <returns></returns>
        public static bool CompareThings(Verse.Thing thing, Verse.Thing other)
        {
            if (thing == null && other == null)
            {
                Client.Instance.Log(new LogEventArgs("Both things are null", LogLevel.DEBUG));
                return true;
            }
            if (thing == null || other == null)
            {
                if (thing == null)
                {
                    Client.Instance.Log(new LogEventArgs($"Left thing is null, right thing is {other.Label}", LogLevel.DEBUG));
                }
                else
                {
                    Client.Instance.Log(new LogEventArgs($"Right thing is null, left thing is {thing.Label}", LogLevel.DEBUG));
                }
                return false;
            }

            if (thing.def.defName != other.def.defName)
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on def name", LogLevel.DEBUG));
                return false;
            }

            if (thing.HitPoints != other.HitPoints)
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on hitpoints", LogLevel.DEBUG));
                return false;
            }

            if (thing.Stuff?.defName != other.Stuff?.defName)
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on stuff def", LogLevel.DEBUG));
                return false;
            }

            if (!thing.TryGetQuality(out QualityCategory q1))
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on quality category, can't get category of {thing.Label}", LogLevel.DEBUG));
                return false;
            }
            if (!other.TryGetQuality(out QualityCategory q2))
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on quality category, can't get category of {other.Label}", LogLevel.DEBUG));
                return false;
            }

            if (q1 != q2)
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on quality category", LogLevel.DEBUG));
                return false;
            }

            if (!CompareThings(thing.GetInnerIfMinified(), other.GetInnerIfMinified()))
            {
                Client.Instance.Log(new LogEventArgs($"{thing.Label} doesn't match {other.Label} on inner minified thing", LogLevel.DEBUG));
                return false;
            }

            Client.Instance.Log(new LogEventArgs($"{thing.Label} matches {other.Label}", LogLevel.DEBUG));
            return true;
        }

        /// <summary>
        /// Recursively gets the def name of the given <see cref="ProtoThing"/>.
        /// </summary>
        /// <param name="protoThing"><see cref="ProtoThing"/> to get the def name from</param>
        /// <returns>Def name</returns>
        private static string getInnerDefName(ProtoThing protoThing)
        {
            // Check if this protoThing contains stuff
            if (protoThing.InnerProtoThing != null)
            {
                // Recurse and get the def name of protoThing's stuff
                return getInnerDefName(protoThing.InnerProtoThing);
            }

            // Return protoThing's def name
            return protoThing.DefName;
        }
    }
}