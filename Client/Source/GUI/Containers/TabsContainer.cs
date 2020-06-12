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
        private List<TabContainerEntry> tabs;

        /// <summary>
        /// Callback invoked when a different tab is selected.
        /// </summary>
        private Action<int> onTabChange;

        /// <summary>
        /// Index of the currently-selected tab.
        /// </summary>
        private int selectedTab;

        public TabsContainer(Action<int> onTabChange = null, int selectedTab = 0)
        {
            this.onTabChange = onTabChange;
            this.selectedTab = selectedTab;

            this.tabs = new List<TabContainerEntry>();
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
            TabRecord tab = new TabRecord(
                label: label,
                clickedAction: () => { selectedTab = index; onTabChange(index); },
                selected: selectedTab == index
            );

            // Add the tab to the tab list
            tabs.Add(new TabContainerEntry { tab = tab, displayable = displayable });
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
            TabRecord selectedRecord = TabDrawer.DrawTabs(inRect, tabs.Select(e => e.tab).ToList());

            // Change the selected record if it was clicked
            if (selectedRecord != null)
            {
                selectedTab = tabs.IndexOf(tabs.Single(tabEntry => tabEntry.tab.label == selectedRecord.label));
                onTabChange?.Invoke(selectedTab);
            }

            // We draw the selected tab
            Displayable selectedDisplayable = tabs[selectedTab].displayable;
            selectedDisplayable.Draw(inRect);
        }

        /// <inheritdoc />
        public override void Update()
        {
            foreach (TabContainerEntry tab in tabs)
            {
                tab.displayable.Update();
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return TabDrawer.TabHeight;
        }
    }
}
