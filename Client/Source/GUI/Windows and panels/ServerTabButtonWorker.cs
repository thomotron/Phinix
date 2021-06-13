using PhinixClient.GUI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    public class ServerTabButtonWorker : MainButtonWorker_ToggleTab
    {
        private const float PADDING = 5f;

        public override void DoButton(Rect inRect)
        {
            base.DoButton(inRect);

            // Check if we should draw the unread messages count
            if (Client.Instance.UnreadMessages > 0 && Client.Instance.ShowUnreadMessageCount && Client.Instance.ShowBlockedUnreadMessageCount)
            {
                // Get a square on the right of the tab with some padding on the right side
                Rect iconRect = inRect.RightPartPixels(inRect.height + PADDING).LeftPartPixels(inRect.height);

                // Get the number of unread messages depending on if blocked users should be included
                int messageCount = Client.Instance.ShowBlockedUnreadMessageCount ? Client.Instance.UnreadMessages : Client.Instance.UnreadMessagesExcludingBlocked;

                // Format the unread message count
                string formattedMessageCount = messageCount > 99 ? "99+" : messageCount.ToString();

                // Draw the count within the square
                new TextWidget(formattedMessageCount, anchor: TextAnchor.MiddleCenter).Draw(iconRect);
            }
        }
    }
}