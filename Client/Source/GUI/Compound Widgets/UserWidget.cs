using System.Collections.Generic;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class UserWidget : Displayable
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
        /// UUID of the user.
        /// </summary>
        private readonly string uuid;

        public UserWidget(User user) : this(user.Uuid) {}

        public UserWidget(string uuid)
        {
            this.uuid = uuid;
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            string displayName = format();

            if (Client.Instance.BlockedUsers.Contains(uuid))
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
                drawNameContextMenu();
            }
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            // Return the calculated the height of the formatted text
            return Text.CalcHeight(format(), width - (horizontalPadding * 2)) + (verticalPadding * 2);
        }

        private string format()
        {
            // Try to get the display name of the user
            if (!Client.Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";

            // Strip name formatting if the user wishes not to see it
            if (!Client.Instance.ShowNameFormatting) displayName = TextHelper.StripRichText(displayName);

            return displayName;
        }

        private void drawNameContextMenu()
        {
            // Do nothing if this is our UUID
            if (uuid == Client.Instance.Uuid) return;

            // Try to get the display name of this message's sender
            if (!Client.Instance.TryGetDisplayName(uuid, out string displayName)) displayName = "???";

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(new FloatMenuOption("Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.CreateTrade(uuid)));
            if (!Client.Instance.BlockedUsers.Contains(uuid))
            {
                // Block
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_blockUser".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.BlockUser(uuid)));
            }
            else
            {
                // Unblock
                items.Add(new FloatMenuOption("Phinix_chat_contextMenu_unblockUser".Translate(TextHelper.StripRichText(displayName)), () => Client.Instance.UnBlockUser(uuid)));
            }

            // Draw the context menu
            if (items.Count > 0) Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}