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

            // Unread private messages are added to the chat count, so a DM shows on the bottom bar too
            int directMessageCount = Client.Instance.Online ? Client.Instance.NewDirectMessageCount : 0;

            // Check if we should draw the unread messages count
            if (Client.Instance.UnreadMessages + directMessageCount > 0 && Client.Instance.Settings.ShowUnreadMessageCount)
            {
                // Get a square on the right of the tab with some padding on the right side
                Rect iconRect = inRect.RightPartPixels(inRect.height + PADDING).LeftPartPixels(inRect.height);

                // Get the number of unread messages depending on if blocked users should be included
                int messageCount = Client.Instance.Settings.ShowBlockedUnreadMessageCount ? Client.Instance.UnreadMessages : Client.Instance.UnreadMessagesExcludingBlocked;
                messageCount += directMessageCount;

                // Format the unread message count
                string formattedMessageCount = messageCount > 99 ? "99+" : messageCount.ToString();

                // Draw the count within the square
                new TextWidget(formattedMessageCount, anchor: TextAnchor.MiddleCenter).Draw(iconRect);
            }
        }
    }
}