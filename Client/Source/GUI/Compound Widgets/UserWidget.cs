using System.Collections.Generic;
using UnityEngine;
using Utils;
using Verse;

// TODO: Properly merge blocking functionality from b6c68ca
namespace PhinixClient.GUI
{
    internal class UserWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => false;

        /// <summary>
        /// Padding above and below the display name text.
        /// </summary>
        private static float verticalPadding = 5f;
        /// <summary>
        /// Padding either side of the display name text.
        /// </summary>
        private static float horizontalPadding = 3f;

        /// <summary>
        /// Background colour for blocked users.
        /// </summary>
        private readonly Color blockedBackgroundColour = new Color(0f, 0f, 0f, 0.35f);
        /// <summary>
        /// Text colour for blocked users.
        /// </summary>
        private readonly Color blockedNameColour = new Color(0.6f, 0.6f, 0.6f);

        /// <summary>
        /// The user's UUID.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// The user's display name.
        /// </summary>
        public string DisplayName => cachedDisplayName;

        /// <summary>
        /// Cached copy of the user's display name.
        /// Updated when <see cref="Update"/> is called.
        /// </summary>
        private string cachedDisplayName;

        /// <summary>
        /// Cached copy of the user's blocked state.
        /// Updated when <see cref="Update"/> is called.
        /// </summary>
        private bool cachedBlockedState;

        /// <summary>
        /// Creates a new <see cref="UserWidget"/> with the given UUID and initial display name.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="initialDisplayName">User's display name</param>
        /// <param name="initialBlockedState">User's initial blocked state</param>
        public UserWidget(string uuid, string initialDisplayName, bool initialBlockedState = false)
        {
            this.Uuid = uuid;
            this.cachedDisplayName = initialDisplayName;
            this.cachedBlockedState = initialBlockedState;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            string displayName = Client.Instance.ShowNameFormatting ? cachedDisplayName : TextHelper.StripRichText(cachedDisplayName);

            if (cachedBlockedState)
            {
                // Draw a highlighted background
                Widgets.DrawRectFast(inRect, blockedBackgroundColour);

                // Strip display name formatting and grey it out
                displayName = TextHelper.StripRichText(displayName).Colorize(blockedNameColour);
            }

            // Get a padded area to draw the text in
            Rect paddedRect = new Rect(inRect.x + horizontalPadding, inRect.y + verticalPadding, inRect.width - (horizontalPadding * 2), inRect.height - (verticalPadding * 2));

            // Draw the text
            Widgets.Label(paddedRect, Mouse.IsOver(inRect) ? displayName.Colorize(Widgets.MouseoverOptionColor) : displayName);

            // Draw the button and optionally the context menu if clicked
            if (Widgets.ButtonInvisible(inRect, false))
            {
                drawContextMenu();
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            string displayName = cachedDisplayName;

            // Strip name formatting if the user wishes not to see it
            if (!Client.Instance.ShowNameFormatting) displayName = TextHelper.StripRichText(displayName);

            // Strip display name formatting and grey it out
            if (cachedBlockedState) displayName = TextHelper.StripRichText(displayName).Colorize(blockedNameColour);

            // Return the calculated the height of the formatted text
            return Text.CalcHeight(displayName, width - (horizontalPadding * 2)) + (verticalPadding * 2);
        }

        /// <inheritdoc />
        public override void Update()
        {
            // Refresh the button label and blocked state
            if (!Client.Instance.TryGetDisplayName(Uuid, out cachedDisplayName)) cachedDisplayName = "???";
            cachedBlockedState = Client.Instance.BlockedUsers.Contains(Uuid);
        }

        /// <summary>
        /// Draws a context menu with user-specific actions.
        /// </summary>
        private void drawContextMenu()
        {
            // Do nothing if this is our UUID
            if (Uuid == Client.Instance.Uuid) return;

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(
                new FloatMenuOption(
                    label: "Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(cachedDisplayName)),
                    action: () => Client.Instance.CreateTrade(Uuid)
                )
            );
            items.Add(
                new FloatMenuOption(
                        label: (cachedBlockedState ? "Phinix_chat_contextMenu_unblockUser" : "Phinix_chat_contextMenu_blockUser").Translate(),
                        action: () =>
                        {
                            if (cachedBlockedState) Client.Instance.UnBlockUser(Uuid);
                            else Client.Instance.BlockUser(Uuid);
                        }
                )
            );

            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}