using System.Collections.Generic;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    internal class UserWidget : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidWidth { get; }
        /// <inheritdoc />
        public override bool IsFluidHeight { get; }

        /// <summary>
        /// The user's UUID.
        /// </summary>
        public string Uuid;

        /// <summary>
        /// The user's display name.
        /// </summary>
        public string DisplayName => underlyingWidget.label;

        /// <summary>
        /// The underlying <see cref="ButtonWidget"/> used for rendering.
        /// </summary>
        private ButtonWidget underlyingWidget;

        /// <summary>
        /// Creates a new <see cref="UserWidget"/> with the given UUID and initial display name.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="initialDisplayName">User's display name</param>
        public UserWidget(string uuid, string initialDisplayName)
        {
            this.Uuid = uuid;

            // Create the underlying button widget
            this.underlyingWidget = new ButtonWidget(
                label: initialDisplayName,
                clickAction: () => drawContextMenu(uuid, underlyingWidget.label),
                drawBackground: false
            );
        }

        /// <inheritdoc />
        public override void Draw(Rect inRect)
        {
            underlyingWidget.Draw(inRect);
        }

        /// <inheritdoc />
        public override float CalcWidth(float height)
        {
            return underlyingWidget.CalcWidth(height);
        }

        /// <inheritdoc />
        public override float CalcHeight(float width)
        {
            return underlyingWidget.CalcHeight(width);
        }

        /// <inheritdoc />
        public override void Update()
        {
            // Refresh the button label
            if (!Client.Instance.TryGetDisplayName(Uuid, out underlyingWidget.label)) underlyingWidget.label = "???";
        }

        /// <summary>
        /// Draws a context menu with user-specific actions.
        /// </summary>
        /// <param name="uuid">User's UUID</param>
        /// <param name="displayName">User's display name</param>
        private void drawContextMenu(string uuid, string displayName)
        {
            // Do nothing if this is our UUID
            if (uuid == Client.Instance.Uuid) return;

            // Create and populate a list of context menu items
            List<FloatMenuOption> items = new List<FloatMenuOption>();
            items.Add(
                new FloatMenuOption(
                    label: "Phinix_chat_contextMenu_tradeWith".Translate(TextHelper.StripRichText(displayName)),
                    action: () => Client.Instance.CreateTrade(uuid)
                )
            );

            // Draw the context menu
            Find.WindowStack.Add(new FloatMenu(items));
        }
    }
}