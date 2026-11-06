using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using Trading;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Sends an item as a full copy of how the game saves it (<see cref="ProtoThing.SaveData"/>), so whatever mods keep
    /// on it arrives too: a sculpture's art, a weapon's upgrades, how fresh food is, a biocoded owner's name and so on.
    /// </summary>
    /// <remarks>
    /// This is the same XML RimWorld writes into a save file for the item, turned back into an item on the other side by
    /// the game's own loader. It's an extra on top of the usual fields (def, stuff, quality, hit points), and anything
    /// that goes wrong here returns null so the caller falls back on those, the way trades always worked.
    /// <para>
    /// Animals, colonists, corpses and anything holding a pawn are never sent this way. A pawn's save is full of links
    /// to the sender's world (their faction, relations, ideology), so they have their own converters instead.
    /// </para>
    /// <para>
    /// The XML comes from another player, so before loading it we check what it asks the game to create
    /// (see <see cref="whyUnsafe"/>) and strip anything that would tie the item to the sender's world: its map, position and
    /// faction. It gets fresh IDs afterwards so it can't clash with things already in this game.
    /// </para>
    /// </remarks>
    public static class ItemSaveData
    {
        /// <summary>
        /// Longest save we send or load, in characters. An ordinary item is a few hundred to a few thousand; this leaves
        /// plenty of room for mods while keeping trades small.
        /// </summary>
        private const int MAX_LENGTH = 32 * 1024;

        /// <summary>
        /// The element name <c>ScribeSaver.DebugOutputFor</c> puts the item under.
        /// </summary>
        private const string ROOT_NODE = "saveable";

        /// <summary>
        /// Things the XML must never ask the game to create: whole worlds, maps and games, and pawns (see the remarks).
        /// </summary>
        private static readonly Type[] forbiddenTypes = { typeof(Game), typeof(World), typeof(Map), typeof(Pawn), typeof(Corpse) };

        /// <summary>
        /// Children of a saved thing that belong to the sender's world rather than the item: where it was on their map,
        /// and their faction (a faction is saved as an ID, which would point at whichever faction has that ID here).
        /// </summary>
        private static readonly string[] worldNodes = { "map", "pos", "faction", "questTags" };

        /// <summary>
        /// Saves a thing as XML for <see cref="ProtoThing.SaveData"/>. Must be called from the main thread.
        /// </summary>
        /// <param name="thing">Thing to save, already off the map</param>
        /// <returns>The XML, or an empty string if this thing isn't sent this way or saving failed</returns>
        public static string Save(Thing thing)
        {
            if (thing is Pawn || thing is Corpse || holdsPawn(thing)) return "";

            // The saver is shared with the game's own saving, so only use it when nothing else is
            if (Scribe.mode != LoadSaveMode.Inactive) return skipSave(thing, "the game was busy saving or loading (" + Scribe.mode + ")");

            try
            {
                string xml = Scribe.saver.DebugOutputFor(thing);
                if (xml == null) return skipSave(thing, "the game's saver gave nothing back");
                if (xml.Length > MAX_LENGTH) return skipSave(thing, string.Format("its save is {0} characters, over the {1} limit", xml.Length, MAX_LENGTH));
                return xml;
            }
            catch (Exception e)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Couldn't save {0} for a trade, sending it the usual way: {1}", thing.LabelShort, e), LogLevel.WARNING));
                return "";
            }
        }

        /// <summary>
        /// Rebuilds a thing from <see cref="ProtoThing.SaveData"/>. Must be called from the main thread.
        /// </summary>
        /// <param name="protoThing">Received thing</param>
        /// <returns>The thing, with the stack size from <paramref name="protoThing"/>, or null if there's no save data or
        /// it couldn't be loaded</returns>
        public static Thing Load(ProtoThing protoThing)
        {
            string data = protoThing.SaveData;

            // No copy at all is normal: pawns, older clients, or the sender couldn't save it (their log says why)
            if (string.IsNullOrEmpty(data)) return null;
            if (data.Length > MAX_LENGTH) return refuse(protoThing, string.Format("its save is {0} characters, over the {1} limit", data.Length, MAX_LENGTH));
            if (Scribe.mode != LoadSaveMode.Inactive) return refuse(protoThing, "the game was busy saving or loading (" + Scribe.mode + ")");

            XmlDocument document = parse(data, out string parseProblem);
            if (document == null) return refuse(protoThing, parseProblem);

            // An item (or anything packed inside it) from a mod this game doesn't have can't be loaded: the game's loader
            // logs a pile of errors over the missing def before giving up. Built the usual way, it becomes an unknown item
            string missingDef = whyDefsMissing(document);
            if (missingDef != null) return refuse(protoThing, missingDef);

            // A weapon's verbs (how it shoots or hits) aren't really item data: the game builds them from the def, and
            // mods like Combat Extended swap in their own classes. Leaving them out gives the weapon this game's own
            // verbs, built the first time they're needed, just like a freshly made weapon
            foreach (XmlNode verbs in document.SelectNodes("//verbTracker/verbs").Cast<XmlNode>().ToList())
            {
                verbs.ParentNode.RemoveChild(verbs);
            }

            // A mod can give an item a class of its own. If this game doesn't have that class but does have the item,
            // it's built with the class this game uses for it
            List<string> swappedClasses = swapUnknownThingClasses(document);
            if (swappedClasses.Count > 0)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Built parts of a received {0} with this game's own classes instead of: {1}", protoThing.DefName, string.Join(", ", swappedClasses)), LogLevel.INFO));
            }

            // Mod data kept as list entries of a class this game doesn't have is left out, the way loading a save
            // without that mod drops it. The rest of the item (its art, say) still comes through
            List<string> droppedClasses = dropUnknownListEntries(document);
            if (droppedClasses.Count > 0)
            {
                Client.Instance.Log(new LogEventArgs(string.Format("Left out parts of a received {0} from mods this game doesn't have: {1}", protoThing.DefName, string.Join(", ", droppedClasses)), LogLevel.INFO));
            }

            string unsafeReason = whyUnsafe(document);
            if (unsafeReason != null) return refuse(protoThing, unsafeReason);
            stripWorldNodes(document.DocumentElement);

            Thing thing = null;
            try
            {
                // What ScribeLoader.InitLoading does with a file, but from our XML. Scribe_Deep then builds the thing
                // like it builds anything in a save, and FinalizeLoading links up references and runs post-load setup.
                // References to things in the sender's world (e.g. who a weapon is biocoded to) find nothing and are
                // left empty, as when a save loads without a mod
                Scribe.loader.curXmlParent = document.DocumentElement;
                Scribe.mode = LoadSaveMode.LoadingVars;
                Scribe_Deep.Look(ref thing, ROOT_NODE);
                Scribe.loader.FinalizeLoading();
            }
            catch (Exception e)
            {
                Scribe.ForceStop();
                Client.Instance.Log(new LogEventArgs(string.Format("Couldn't load a received {0} from its save, using the usual way: {1}", protoThing.DefName, e), LogLevel.WARNING));
                return null;
            }

            // Only trust it if it's what the rest of the trade says it is
            if (thing?.def == null) return refuse(protoThing, "loading it gave nothing back");
            if (thing.def.defName != protoThing.DefName) return refuse(protoThing, "its save is a " + thing.def.defName);
            if (thing is Pawn || thing is Corpse) return refuse(protoThing, "its save is a pawn or corpse");

            // Fresh IDs for it and anything inside it (a minified building's furniture, say), so nothing clashes with
            // this game's own things, and no faction until it lands
            List<Thing> all = new List<Thing> { thing };
            if (thing is IThingHolder holder) ThingOwnerUtility.GetAllThingsRecursively(holder, all, true, null);
            foreach (Thing part in all.Distinct())
            {
                // Only things that can have a faction (furniture, weapons...) take one; asking anything else (leather,
                // say) logs an error
                if (part.def.CanHaveFaction) part.SetFactionDirect(null);
                part.thingIDNumber = -1;
                ThingIDMaker.GiveIDTo(part);
            }

            // A market can sell part of a stack, so the count in the trade wins over the one in the save
            thing.stackCount = protoThing.StackCount;
            return thing;
        }

        /// <summary>
        /// Logs why a thing wasn't sent with a full copy.
        /// </summary>
        /// <returns>An empty string, for <see cref="Save"/> to return</returns>
        private static string skipSave(Thing thing, string reason)
        {
            Client.Instance.Log(new LogEventArgs(string.Format("Couldn't save {0} for a trade, sending it the usual way: {1}", thing.LabelShort, reason), LogLevel.WARNING));
            return "";
        }

        /// <summary>
        /// Logs why a received full copy wasn't used. The thing is then built from the usual fields, so anything only
        /// the copy carries (a sculpture's art, say) is made up afresh.
        /// </summary>
        /// <returns>Null, for <see cref="Load"/> to return</returns>
        private static Thing refuse(ProtoThing protoThing, string reason)
        {
            Client.Instance.Log(new LogEventArgs(string.Format("Couldn't use the full copy of a received {0}, building it the usual way: {1}", protoThing.DefName, reason), LogLevel.WARNING));
            return null;
        }

        /// <summary>
        /// Whether a thing holds a pawn anywhere inside it, e.g. a minified cryptosleep casket with someone in it.
        /// </summary>
        private static bool holdsPawn(Thing thing)
        {
            if (!(thing is IThingHolder holder)) return false;

            List<Thing> inside = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(holder, inside, true, null);
            return inside.Any(innerThing => innerThing is Pawn || innerThing is Corpse);
        }

        /// <summary>
        /// Reads the XML under a root element (the loader looks for the item as a child of the current node), refusing
        /// DTDs and external entities so a crafted save can't read files or make the game fetch URLs.
        /// </summary>
        private static XmlDocument parse(string data, out string problem)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                XmlDocument document = new XmlDocument { XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(new StringReader("<root>" + data + "</root>"), settings))
                {
                    document.Load(reader);
                }

                // Exactly one item
                XmlNodeList items = document.DocumentElement.SelectNodes(ROOT_NODE);
                if (items != null && items.Count == 1 && document.DocumentElement.ChildNodes.Count == 1)
                {
                    problem = null;
                    return document;
                }

                problem = string.Format("expected one <{0}> in its save, found {1} (and {2} nodes in all)", ROOT_NODE, items?.Count ?? 0, document.DocumentElement.ChildNodes.Count);
                return null;
            }
            catch (XmlException e)
            {
                problem = "its save isn't valid XML: " + e.Message;
                return null;
            }
        }

        /// <summary>
        /// Checks every <c>Class="..."</c> in the XML, which is how a save says what to create. Each must be a type this
        /// game has, and not a world, map, game, pawn or corpse. Other things the game can look up by ID (factions,
        /// quests, ideologies...) are refused too unless they're ordinary items, since an item never owns one. Anything
        /// that looks like a saved pawn is refused as well.
        /// </summary>
        /// <returns>Why it isn't safe to load, or null if it is</returns>
        private static string whyUnsafe(XmlDocument document)
        {
            // A pawn kept inside an item (in a mod's container, say) is saved without a Class when the container only
            // holds pawns, but every saved pawn has a kindDef
            XmlNode kindDef = document.SelectSingleNode("//kindDef");
            if (kindDef != null) return "it looks like it holds a pawn (a kindDef at " + nodePath(kindDef) + ")";

            XmlNodeList classAttributes = document.SelectNodes("//@Class");
            if (classAttributes == null) return null;

            foreach (XmlAttribute attribute in classAttributes)
            {
                string where = nodePath(attribute.OwnerElement);
                Type type = GenTypes.GetTypeInAnyAssembly(attribute.Value);
                if (type == null) return string.Format("it uses the class {0} (at {1}), which this game doesn't have", attribute.Value, where);
                if (forbiddenTypes.Any(forbidden => forbidden.IsAssignableFrom(type))) return string.Format("it asks for a {0} (at {1}), which items may not hold", type.Name, where);
                if (typeof(ILoadReferenceable).IsAssignableFrom(type) && !typeof(Thing).IsAssignableFrom(type))
                    return string.Format("it asks for a {0} (at {1}), which belongs to a world rather than an item", type.Name, where);
            }

            return null;
        }

        /// <summary>
        /// Checks the def and stuff of every saved thing in the XML against this game's defs. A saved thing is recognised
        /// by having both a <c>def</c> and an <c>id</c>, as in <see cref="stripWorldNodes"/>.
        /// </summary>
        /// <returns>What this game is missing, or null if it has them all</returns>
        private static string whyDefsMissing(XmlDocument document)
        {
            foreach (XmlElement element in document.SelectNodes("//*[def and id]").Cast<XmlElement>())
            {
                // The item itself is the root, which has no path; things packed inside it say where they are
                string path = nodePath(element);
                string where = path.Length > 0 ? " (at " + path + ")" : "";

                string defName = element["def"].InnerText.Trim();
                if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) == null)
                    return string.Format("this game doesn't have {0}{1}, so it's probably from a mod this game lacks", defName, where);

                string stuffName = element["stuff"]?.InnerText.Trim();
                if (!string.IsNullOrEmpty(stuffName) && DefDatabase<ThingDef>.GetNamedSilentFail(stuffName) == null)
                    return string.Format("this game doesn't have its material {0}{1}", stuffName, where);
            }

            return null;
        }

        /// <summary>
        /// Gives every saved thing whose <c>Class</c> this game doesn't have the class this game uses for its def
        /// (<see cref="ThingDef.thingClass"/>). A saved thing is recognised by its <c>def</c>, as in
        /// <see cref="stripWorldNodes"/>. Things whose def this game doesn't have are left as they are.
        /// </summary>
        /// <returns>The class names that were replaced, each once</returns>
        private static List<string> swapUnknownThingClasses(XmlDocument document)
        {
            List<string> swapped = new List<string>();
            foreach (XmlElement element in document.SelectNodes("//*[@Class]").Cast<XmlElement>().ToList())
            {
                string className = element.GetAttribute("Class");
                if (GenTypes.GetTypeInAnyAssembly(className) != null) continue;

                ThingDef def = element["def"] == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(element["def"].InnerText.Trim());
                if (def?.thingClass == null) continue;

                element.SetAttribute("Class", def.thingClass.FullName);
                if (!swapped.Contains(className)) swapped.Add(className);
            }
            return swapped;
        }

        /// <summary>
        /// Removes list entries (<c>li</c>) whose <c>Class</c> this game doesn't have. A list can lose an entry without
        /// breaking anything, unlike a single field, so an unknown class anywhere else still refuses the copy
        /// (see <see cref="whyUnsafe"/>).
        /// </summary>
        /// <returns>The class names that were left out, each once</returns>
        private static List<string> dropUnknownListEntries(XmlDocument document)
        {
            List<string> dropped = new List<string>();
            foreach (XmlElement entry in document.SelectNodes("//li[@Class]").Cast<XmlElement>().ToList())
            {
                string className = entry.GetAttribute("Class");
                if (GenTypes.GetTypeInAnyAssembly(className) != null) continue;

                // An entry inside one already removed goes with it
                entry.ParentNode?.RemoveChild(entry);
                if (!dropped.Contains(className)) dropped.Add(className);
            }
            return dropped;
        }

        /// <summary>
        /// Where a node is in the save, like "saveable/comps/li", for log messages.
        /// </summary>
        private static string nodePath(XmlNode node)
        {
            List<string> names = new List<string>();
            for (XmlNode current = node; current != null && current.ParentNode is XmlElement; current = current.ParentNode)
            {
                names.Add(current.Name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>
        /// Removes <see cref="worldNodes"/> from every saved thing in the XML. A saved thing is recognised by having both
        /// a <c>def</c> and an <c>id</c>, which is how <c>Thing.ExposeData</c> writes every thing.
        /// </summary>
        private static void stripWorldNodes(XmlNode node)
        {
            foreach (XmlNode child in node.ChildNodes.Cast<XmlNode>().ToList())
            {
                if (child.NodeType == XmlNodeType.Element) stripWorldNodes(child);
            }

            if (node["def"] == null || node["id"] == null) return;
            foreach (string name in worldNodes)
            {
                XmlElement worldNode = node[name];
                if (worldNode != null) node.RemoveChild(worldNode);
            }
        }
    }
}
