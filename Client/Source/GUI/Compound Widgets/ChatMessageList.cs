using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chat;
using Trading;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient.GUI
{
    public class ChatMessageList : Displayable
    {
        /// <inheritdoc />
        public override bool IsFluidHeight => true;
        /// <inheritdoc />
        public override bool IsFluidWidth => true;

        private const float SCROLLBAR_WIDTH = 16f;

        /// <summary>
        /// List of message widgets.
        /// </summary>
        /// <remarks>
        /// These are stored separately so it can be updated within the <see cref="chatFlexContainer"/> post-generation.
        /// </remarks>
        private List<ChatMessageWidget> messageWidgets;
        /// <summary>
        /// A list of message widgets to be added to <see cref="messageWidgets"/>.
        /// </summary>
        /// <remarks>
        /// This list is locked and modified by the <see cref="ChatMessageReceivedEventHandler"/> method when an event
        /// is fired. The drawing thread will attempt to lock and append this list to the <see cref="messageWidgets"/>
        /// list when <see cref="Draw"/> is first called. If the lock cannot be taken by the drawing thread, it ignores
        /// this list and draws with the existing <see cref="messageWidgets"/> list instead.
        /// </remarks>
        private List<ChatMessageWidget> newMessageWidgets;
        /// <summary>
        /// Lock object to prevent multi-threaded access problems with <see cref="messageWidgets"/>.
        /// </summary>
        private object newMessageWidgetsLock = new object();

        /// <summary>
        /// Container encapsulating the message widgets.
        /// </summary>
        private VerticalFlexContainer chatFlexContainer;

        // A collection of state variables for the sticky scroll logic
        private Vector2 chatScroll = new Vector2(0, 0);
        private float oldHeight = 0f;
        private bool scrollToBottom = false;
        private bool stickyScroll = true;

        /// <summary>
        /// Creates a new <see cref="ChatMessageList" /> and populates it with all received chat messages.
        /// </summary>
        public ChatMessageList()
        {
            // Generate message widgets from chat messages
            messageWidgets = Client.Instance.GetChatMessages().Select(message => new ChatMessageWidget(message)).ToList();
            newMessageWidgets = new List<ChatMessageWidget>();

            // Pack them into a vertical flex container
            chatFlexContainer = new VerticalFlexContainer(messageWidgets, 0f);

            // Subscribe to chat message events
            // TODO: Unsubscribe from the event when being destroyed (not that it will be until Phinix shuts down)
            Client.Instance.OnChatMessageReceived += ChatMessageReceivedEventHandler;
        }

        public override void Draw(Rect inRect) {
            // Try and append new widgets
            if (Monitor.TryEnter(newMessageWidgetsLock))
            {
                if (newMessageWidgets.Count > 0)
                {
                    // Append each new widget to our list and the flex container
                    foreach (ChatMessageWidget widget in newMessageWidgets)
                    {
                        messageWidgets.Add(widget);
                        chatFlexContainer.Add(widget);
                    }

                    // Clear the new widget list and mark the messages as read
                    newMessageWidgets.Clear();
                    Client.Instance.MarkAsRead();
                }

                Monitor.Exit(newMessageWidgetsLock);
            }

            // Set up the scrollable container
            Rect innerContainer = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width - SCROLLBAR_WIDTH,
                height: chatFlexContainer.CalcHeight(inRect.width - SCROLLBAR_WIDTH)
            );

            // Get a copy of the old scroll position
            Vector2 oldChatScroll = new Vector2(chatScroll.x, chatScroll.y);

            // Start scrolling
            Widgets.BeginScrollView(inRect, ref chatScroll, innerContainer);

            // Draw the flex container
            chatFlexContainer.Draw(innerContainer);

            // Stop scrolling
            Widgets.EndScrollView();

            // Enter the logic to get sticky scrolling to work

            #region Sticky scroll logic

            // Credit to Aze for figuring out how to get the bottom scroll pos
            bool scrolledToBottom = chatScroll.y.Equals(innerContainer.height - inRect.height);
            bool scrollChanged = !chatScroll.y.Equals(oldChatScroll.y);
            float heightDifference = oldHeight - innerContainer.height;

            if (scrollChanged)
            {
                if (scrolledToBottom)
                {
                    // Enable sticky scroll
                    stickyScroll = true;
                }
                else
                {
                    // Not at bottom, disable sticky scroll
                    stickyScroll = false;
                }
            }
            else if (!heightDifference.Equals(0f))
            {
                if (stickyScroll || scrollToBottom)
                {
                    // Scroll to bottom
                    chatScroll.y = innerContainer.height - inRect.height;
                    scrollToBottom = false;
                }
            }

            // Update old height for the next pass
            oldHeight = innerContainer.height;

            #endregion
        }

        /// <summary>
        /// Scrolls to the bottom of the list.
        /// </summary>
        public void ScrollToBottom()
        {
            scrollToBottom = true;
        }

        private void ChatMessageReceivedEventHandler(object sender, ClientChatMessageEventArgs args)
        {
            Client.Instance.Log(new LogEventArgs("The chat message handler works"));

            lock (newMessageWidgetsLock)
            {
                // Append the new message to the list
                newMessageWidgets.Add(new ChatMessageWidget(args.Message));
            }
        }
    }
}