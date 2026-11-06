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
        /// What one of a thing is worth in silver in this game, or 0 if it can't be worked out.
        /// </summary>
        public static float MarketValueOf(Verse.Thing thing)
        {
            // A modded item's value can depend on parts of it that aren't set up properly yet (a rebuilt item, say),
            // and a price isn't worth failing a trade over
            try
            {
                return thing.MarketValue;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

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

            // Name the mods it comes from, so a player who doesn't have them can be told which
            protoThing.Mods.AddRange(ModsOf(verseThing));

            // What one is worth here, so the other player can compare it with their own game's price
            protoThing.MarketValue = MarketValueOf(verseThing);

            // Name the pawn it was made from, so the original can come back if it's returned (see ParkedPawns)
            if (verseThing is Pawn originalPawn) protoThing.OriginId = ParkedPawns.OriginIdOf(originalPawn);

            // Check if verseThing is an animal
            if (AnimalConverter.IsAnimal(verseThing))
            {
                // Attach the animal's details (name, age, training, health)
                protoThing.Animal = AnimalConverter.ConvertAnimalFromVerse((Pawn) verseThing);
            }
            // Or a colonist
            else if (ColonistConverter.IsColonist(verseThing))
            {
                // Attach the colonist's details (name, story, skills, genes, health, looks, gear)
                protoThing.Colonist = ColonistConverter.ConvertColonistFromVerse((Pawn) verseThing);
            }

            // Return constructed protoThing
            return protoThing;
        }

        /// <summary>
        /// Converts a thing that's about to be sent, like <see cref="ConvertThingFromVerse"/> but also with a full copy
        /// of the item (<see cref="ItemSaveData"/>) so anything mods keep on it goes too. Must be called from the main
        /// thread, with the thing already off the map.
        /// </summary>
        /// <param name="verseThing">Thing being sent</param>
        /// <returns>Converted thing</returns>
        public static ProtoThing ConvertThingForSending(Verse.Thing verseThing)
        {
            ProtoThing protoThing = ConvertThingFromVerse(verseThing);

            if (verseThing is Pawn pawn)
            {
                // A colonist's gear gets the same full copies as anything else sent
                if (protoThing.Colonist != null) ColonistConverter.AddGearCopies(pawn, protoThing.Colonist);
                return protoThing;
            }

            protoThing.SaveData = ItemSaveData.Save(verseThing);
            addArt(verseThing, protoThing);
            return protoThing;
        }

        /// <summary>
        /// Adds the art of a thing and of anything packed inside it (a minified sculpture, say) to its converted form.
        /// </summary>
        private static void addArt(Verse.Thing verseThing, ProtoThing protoThing)
        {
            protoThing.Art = ItemArt.ConvertFromVerse(verseThing);
            if (verseThing is MinifiedThing minified && minified.InnerThing != null && protoThing.InnerProtoThing != null)
            {
                addArt(minified.InnerThing, protoThing.InnerProtoThing);
            }
        }

        /// <summary>
        /// Builds a thing that has arrived (or come back), from its full copy if it has one and this game can load it,
        /// otherwise like <see cref="ConvertThingFromProtoOrUnknown"/>. Then puts its art back and frees it from any
        /// biocoding. Must be called from the main thread.
        /// </summary>
        /// <param name="protoThing">Thing that arrived</param>
        /// <returns>The thing, or an unknown item if this game doesn't have it</returns>
        public static Verse.Thing ConvertReceivedThing(ProtoThing protoThing)
        {
            // One of our own animals or colonists coming home: the very same pawn, not a copy (see ParkedPawns)
            if (ParkedPawns.TryTake(protoThing.OriginId, out Pawn original)) return original;

            Verse.Thing thing = ItemSaveData.Load(protoThing) ?? ConvertThingFromProtoOrUnknown(protoThing);
            finishReceived(thing, protoThing);
            return thing;
        }

        /// <summary>
        /// Puts a received item's art back and undoes biocoding to someone who isn't here, for it and anything packed
        /// inside it. Animals and colonists are left alone; their own converters handle them.
        /// </summary>
        private static void finishReceived(Verse.Thing thing, ProtoThing protoThing)
        {
            if (thing == null || thing is Pawn) return;

            ItemArt.Apply(thing, protoThing.Art);

            // Whoever it was biocoded to stayed in the sender's world, which would leave it locked to nobody: no one could
            // ever use it. Biocoded items can't be offered on their own, so this only catches ones from older mods.
            // A colonist's own gear is biocoded to them again in ColonistConverter
            CompBiocodable biocodable = thing.TryGetComp<CompBiocodable>();
            if (biocodable != null && biocodable.Biocoded && biocodable.CodedPawn == null) biocodable.UnCode();

            if (thing is MinifiedThing minified && protoThing.InnerProtoThing != null)
            {
                finishReceived(minified.InnerThing, protoThing.InnerProtoThing);
            }
        }

        /// <summary>
        /// The mods a thing and its material come from, Core left out, each once.
        /// </summary>
        /// <param name="verseThing">Thing to check</param>
        /// <returns>Its mods</returns>
        public static IEnumerable<ProtoMod> ModsOf(Verse.Thing verseThing)
        {
            return new[] { verseThing.def?.modContentPack, verseThing.Stuff?.modContentPack }
                .Where(mod => mod != null && !mod.IsCoreMod && !string.IsNullOrEmpty(mod.PackageId))
                .GroupBy(mod => mod.PackageId.ToLowerInvariant())
                .Select(group => new ProtoMod { PackageId = group.Key, Name = group.First().Name ?? group.Key });
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
            // Animals are rebuilt from their own details rather than made like an item
            if (protoThing.Animal != null)
            {
                return AnimalConverter.ConvertAnimalFromProto(protoThing.Animal);
            }

            // So are colonists
            if (protoThing.Colonist != null)
            {
                return ColonistConverter.ConvertColonistFromProto(protoThing.Colonist);
            }

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

            // An animal or colonist that arrived without its Animal/Colonist details can't be rebuilt.
            // ThingMaker would hand back a "pawn" with no kind, no age and no body, and RimWorld crashes the moment it
            // tries to draw its name. This happens on servers running the original Phinix: they don't know about those
            // fields and quietly drop them when they pass the trade along. Throwing here makes
            // ConvertThingFromProtoOrUnknown show it as an unknown item instead (like raising an exception in Python
            // so the caller's "except" branch takes over).
            if (thingDef.race != null)
            {
                throw new InvalidOperationException(string.Format("'{0}' is a pawn but arrived without its details", protoThing.DefName));
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
        /// Whether the given offered thing is an animal or colonist that lost its details on the way through the server.
        /// </summary>
        /// <remarks>
        /// Servers running the original Phinix don't have the Animal and Colonist fields, so when they echo a trade back
        /// those fields are simply missing. All that's left is the race's def name (e.g. "Donkey").
        /// </remarks>
        /// <param name="protoThing">Offered thing</param>
        /// <returns>Whether it is a pawn with its details stripped</returns>
        public static bool IsStrippedPawn(ProtoThing protoThing)
        {
            if (protoThing.Animal != null || protoThing.Colonist != null) return false;

            // GetNamedSilentFail returns null rather than logging an error when the def doesn't exist (e.g. a missing mod)
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(protoThing.DefName);

            // Only pawn defs have race properties
            return def?.race != null;
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