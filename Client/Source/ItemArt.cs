using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Trading;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Carries an item's art (title, author and description) to the other player as plain text, so it arrives exactly as
    /// the sender saw it.
    /// </summary>
    /// <remarks>
    /// The full copy of an item (<see cref="ItemSaveData"/>) already brings the title and author, but not always the
    /// description: that's generated from a "tale", an event from the sender's world like a raid or a wedding, and the
    /// tale stays behind. And when the copy can't be loaded at all, the item is rebuilt from the basic fields with
    /// brand-new art. So the art travels as text too (<see cref="ProtoArt"/>) and is put back on whatever arrives.
    /// <para>
    /// The game doesn't keep a description on the item; it writes one from the tale each time it's shown. So the text
    /// is kept in <see cref="ArtDescriptionsGameComponent"/>, and <see cref="Patches.CompArtDescriptionPatch"/> shows it
    /// instead of a made-up one.
    /// </para>
    /// </remarks>
    public static class ItemArt
    {
        /// <summary>
        /// Longest title and author name we keep, in characters. Real ones are a few words.
        /// </summary>
        private const int MAX_NAME_LENGTH = 200;

        /// <summary>
        /// Longest description we keep. Real ones are a few sentences.
        /// </summary>
        private const int MAX_DESCRIPTION_LENGTH = 2000;

        // CompArt keeps these private, so they're set through reflection (see PsycastConverter for how that works)
        private static readonly FieldInfo titleField = AccessTools.Field(typeof(CompArt), "titleInt");
        private static readonly FieldInfo authorField = AccessTools.Field(typeof(CompArt), "authorNameInt");
        private static readonly FieldInfo taleRefField = AccessTools.Field(typeof(CompArt), "taleRef");

        /// <summary>
        /// Records a thing's art. Must be called from the main thread, since writing the description uses the game's
        /// random numbers.
        /// </summary>
        /// <param name="thing">Thing being sent</param>
        /// <returns>Its art, or null if it has none</returns>
        public static ProtoArt ConvertFromVerse(Thing thing)
        {
            CompArt art = thing.TryGetComp<CompArt>();
            if (art == null || !art.Active) return null;

            try
            {
                return new ProtoArt
                {
                    Title = ((TaggedString) titleField.GetValue(art)).RawText ?? "",
                    Author = ((TaggedString) authorField.GetValue(art)).RawText ?? "",
                    Description = art.GenerateImageDescription().Resolve() ?? ""
                };
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Couldn't read the art on {0}, its art won't come along: {1}", thing.LabelShort, e.Message), LogLevel.WARNING));
                return null;
            }
        }

        /// <summary>
        /// Puts the sent art on a thing that has arrived, however it was built.
        /// </summary>
        /// <param name="thing">Thing that arrived</param>
        /// <param name="protoArt">Its art from the trade, or null if it had none</param>
        public static void Apply(Thing thing, ProtoArt protoArt)
        {
            if (protoArt == null) return;

            CompArt art = thing.TryGetComp<CompArt>();
            if (art == null) return;

            try
            {
                // An item rebuilt from the basic fields may not have art yet. A tale-less reference is what the game
                // gives art that isn't about any event
                if (!art.Active) taleRefField.SetValue(art, TaleReference.Taleless);

                // TaggedString is the game's text type for things shown in the UI; it's made from a plain string
                if (!string.IsNullOrEmpty(protoArt.Title)) titleField.SetValue(art, new TaggedString(clean(protoArt.Title, MAX_NAME_LENGTH)));
                if (!string.IsNullOrEmpty(protoArt.Author)) authorField.SetValue(art, new TaggedString(clean(protoArt.Author, MAX_NAME_LENGTH)));
                if (!string.IsNullOrEmpty(protoArt.Description))
                {
                    Current.Game?.GetComponent<ArtDescriptionsGameComponent>()?.Set(thing, clean(protoArt.Description, MAX_DESCRIPTION_LENGTH));
                }
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Couldn't put the sent art back on {0}: {1}", thing.LabelShort, e.Message), LogLevel.WARNING));
            }
        }

        /// <summary>
        /// Trims text from another player and strips size tags from it, like chat messages.
        /// </summary>
        private static string clean(string text, int maxLength)
        {
            if (text.Length > maxLength) text = text.Substring(0, maxLength);
            return TextHelper.SanitiseRichText(text);
        }
    }

    /// <summary>
    /// Remembers the art descriptions that arrived with traded items, by thing ID, inside the save file.
    /// </summary>
    /// <remarks>
    /// RimWorld creates one of every GameComponent subclass for each game automatically (new or loaded), so just
    /// defining this class is enough to hook it in, the same way as <see cref="EscrowGameComponent"/>.
    /// </remarks>
    public class ArtDescriptionsGameComponent : GameComponent
    {
        private Dictionary<string, string> descriptions = new Dictionary<string, string>();

        // RimWorld needs this exact constructor to create the component
        public ArtDescriptionsGameComponent(Game game)
        {
        }

        public void Set(Thing thing, string description) => descriptions[thing.ThingID] = description;

        /// <summary>
        /// The description that arrived with a thing, or null if it didn't come with one.
        /// </summary>
        public string Get(Thing thing) => thing != null && descriptions.TryGetValue(thing.ThingID, out string description) ? description : null;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref descriptions, "phimeArtDescriptions", LookMode.Value, LookMode.Value);

            // Loading a save from before this existed gives null, so start an empty one instead
            if (descriptions == null) descriptions = new Dictionary<string, string>();
        }
    }
}
