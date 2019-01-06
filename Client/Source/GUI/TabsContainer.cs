// Original file provided by Longwelwind (https://github.com/Longwelwind)
// as a part of the RimWorld mod Phi (https://github.com/Longwelwind/Phi)

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PhinixClient.GUI
{
    public class TabsContainer : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        /// <summary>
        /// Collection of tabs that will be drawn.
        /// </summary>
        private List<TabEntry> tabs = new List<TabEntry>();

        /// <summary>
        /// Index of the currently-selected tab.
        /// </summary>
        private int selectedTab = 0;

        /// <summary>
        /// Adds a tab to the container.
        /// </summary>
        /// <param name="label">Label shown on the tab itself</param>
        /// <param name="displayable">Contents of the tab</param>
        public void AddTab(string label, Displayable displayable)
        {
            // Set the current index to where this new tab will be
            int index = tabs.Count;
            
            // Create a tab record
            TabRecord tab = new TabRecord(label, () => selectedTab = index, selectedTab == index);
            
            // Add the tab to the tab list
            tabs.Add(new TabEntry { tab = tab, displayable = displayable });
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            // Do nothing if there's no tabs
            if (tabs.Count == 0) return;

            // Ok, so for whatever reason the tabs are drawn /above/ whatever region you give them (why?!)
            // To work around this we just trim the tab height off of the container rect
            inRect = inRect.BottomPartPixels(inRect.height - TabDrawer.TabHeight);
            
            // We draw the top with tabs
            TabDrawer.DrawTabs(inRect, tabs.Select(e => e.tab).ToList());

            // We draw the selected tab
            Rect childArea = inRect.BottomPartPixels(inRect.height - TabDrawer.TabHeight);
            Displayable selectedDisplayable = tabs[selectedTab].displayable;
            selectedDisplayable.Draw(childArea);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return TabDrawer.TabHeight;
        }
    }
}
