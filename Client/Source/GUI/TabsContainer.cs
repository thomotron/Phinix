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
        private const float TAB_HEIGHT = 45f;

        /// <summary>
        /// Collection of tabs that will be drawn.
        /// </summary>
        private List<TabEntry> tabs = new List<TabEntry>();
        
        /// <summary>
        /// Index of the currently-selected tab.
        /// </summary>
        private int selectedTab;

        public TabsContainer(int selectedTab, Action onChange)
        {
            this.selectedTab = selectedTab;
        }

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
            // We draw the top with tabs
            Rect tabsArea = inRect.TopPartPixels(TAB_HEIGHT);
            TabDrawer.DrawTabs(tabsArea, (List<TabRecord>) tabs.Select((e) => e.tab));

            // We draw the selected tab
            Rect childArea = inRect.BottomPartPixels(inRect.height - TAB_HEIGHT);
            Displayable selectedDisplayable = tabs[selectedTab].displayable;
            selectedDisplayable.Draw(childArea);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return TAB_HEIGHT;
        }

        /// <inheritdoc />
        public override bool IsFluidHeight()
        {
            return false;
        }
    }
}
