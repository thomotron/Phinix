using System.Collections.Generic;
using System.Linq;
using Trading;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class StackedThings
    {
        public List<Thing> Things;

        public int Count => Things.Sum(thing => thing.stackCount);

        public int Selected = 0;

        public StackedThings(IEnumerable<Thing> things)
        {
            this.Things = things.ToList();
        }

        /// <summary>
        /// Returns whether the given thing can stack with all things in the stack.
        /// </summary>
        /// <param name="thing">Thing to check</param>
        /// <returns>Whether the given thing can stack with all things in the stack</returns>
        public bool CanStack(Thing thing)
        {
            return Things.All(thing.CanStackWith);
        }

        /// <summary>
        /// Gets the selected amounf of things as their <c>ProtoThing</c> equivalents.
        /// Does some hacky-feeling stuff to get just the right amount of stacks set in stone as <c>ProtoThing</c>s.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ProtoThing> GetSelectedThingsAsProto()
        {
            List<Thing> thingsToConvert = new List<Thing>();
            Thing thingToModify = null;

            int remainingThings = Selected;
            foreach (Thing thing in Things)
            {
                // Check if we have collected all necessary things
                if (remainingThings == 0) break;
                
                // Check if this thing has more in its stack than we need to take
                if (thing.stackCount > remainingThings)
                {
                    // Set this as the thing to modify and stop
                    remainingThings = 0;
                    thingToModify = thing;
                }
                else
                {
                    // Subtract this thing's stack size from the remaining count and add it to the conversion list
                    remainingThings -= thing.stackCount;
                    thingsToConvert.Add(thing);
                }
            }

            // Convert the readily-convertible things
            List<ProtoThing> convertedThings = thingsToConvert.Select(TradingThingConverter.ConvertThingFromVerse).ToList();

            // Check if we need to modify a thing to get the right amount
            if (thingToModify != null)
            {
                // Get the target stack size and difference from the current stack size
                int targetAmount = Selected - thingsToConvert.Sum(thing => thing.stackCount);
                int actualAmount = thingToModify.stackCount;

                // Set the stack size to the target amount
                thingToModify.stackCount = targetAmount;
                
                // Convert and add the modified thing
                convertedThings.Add(TradingThingConverter.ConvertThingFromVerse(thingToModify));

                // Set the stack size to what it was before
                thingToModify.stackCount = actualAmount;
            }

            // Return the list of converted ProtoThings
            return convertedThings;
        }

        /// <summary>
        /// Deletes the selected amount of things from the thing list.
        /// </summary>
        public void DeleteSelected()
        {
            // Set up a list to hold all things pending destruction
            List<Thing> thingsToDestroy = new List<Thing>();
            
            int remainingThings = Selected;
            foreach (Thing thing in Things)
            {
                // Check if we have deleted all the necessary things, exiting the loop if so
                if (remainingThings == 0) break;
                
                // Check if this thing has more in its stack than we need to take
                if (thing.stackCount > remainingThings)
                {
                    // Just take the amount we need from this stack
                    remainingThings = 0;
                    thing.stackCount -= remainingThings;
                }
                else
                {
                    // Subtract this thing's stack size from the remaining count and destroy it
                    remainingThings -= thing.stackCount;
                    thingsToDestroy.Add(thing);
                }
            }

            // Remove and destroy all things pending destruction
            foreach (Thing thing in thingsToDestroy)
            {
                // Remove this thing from the things list
                Things.Remove(thing);
                
                // Destroy it
                thing.Destroy();
            }
        }
        
        /// <summary>
        /// Groups the given collection of items by their def type and stackability.
        /// </summary>
        /// <param name="items">Items to group</param>
        /// <returns>Grouped items list</returns>
        public static List<StackedThings> GroupThings(IEnumerable<Thing> items)
        {
            // Set up an item dictionary
            Dictionary<string, List<StackedThings>> groupedItems = new Dictionary<string, List<StackedThings>>();
            
            foreach (Thing item in items)
            {
                // Check if this item type already has a group
                if (groupedItems.ContainsKey(item.def.defName))
                {
                    // Loop over all the item stacks in the group
                    bool stacked = false;
                    foreach (StackedThings itemStack in groupedItems[item.def.defName])
                    {
                        // Check if this item can stack on this stack
                        if (itemStack.CanStack(item))
                        {
                            // Increment this stack's item count by the item's stack count and break the loop
                            itemStack.Things.Add(item);
                            stacked = true;
                            break;
                        }
                    }

                    // Check if a stack wasn't found within this group
                    if (!stacked)
                    {
                        // Add a new stack with this item in it
                        groupedItems[item.def.defName].Add(new StackedThings(new[]{item}));
                    }
                }
                else
                {
                    // Create a new item stack with this item in it
                    StackedThings itemStack = new StackedThings(new[]{item});

                    // Add a new group with the item stack
                    groupedItems.Add(item.def.defName, new List<StackedThings>{itemStack});
                }
            }

            // Return the grouped items dictionary
            return groupedItems.SelectMany(pair => pair.Value).ToList();
        }
    }
}