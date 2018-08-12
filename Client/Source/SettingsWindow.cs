using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    class SettingsWindow : Window
    {
        private const float DEFAULT_SPACING = 10f;

        private const float SERVER_ADDRESS_BOX_HEIGHT = 30f;

        private const float CONNECT_BUTTON_HEIGHT = 30f;
        private const float CONNECT_BUTTON_WIDTH = 120f;

        private const float USERNAME_BOX_HEIGHT = 30f;

        private const float USERNAME_SET_BUTTON_HEIGHT = 30f;
        private const float USERNAME_SET_BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(500f, 100f);

        private static string serverAddress = "";
        private bool conn = false;

        public override void DoWindowContents(Rect inRect)
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;

            // Server details (address and [dis]connect button) container
            Rect serverDetailRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin,
                width: inRect.width,
                height: SERVER_ADDRESS_BOX_HEIGHT
            );
            if (conn)
            {
                DrawConnectedServerDetails(serverDetailRect);
            }
            else
            {
                DrawDisconnectedServerDetails(serverDetailRect);
            }
        }

        /// <summary>
        /// Draws a non-editable server address and disconnect button within a given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawConnectedServerDetails(Rect container)
        {
            // Server address label
            Rect serverAddressLabelRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width - (CONNECT_BUTTON_WIDTH + DEFAULT_SPACING),
                height: SERVER_ADDRESS_BOX_HEIGHT
            );
            Widgets.Label(serverAddressLabelRect, "Phinix_settings_connectedToLabel".Translate(serverAddress));

            // Disconnect button
            Rect connectButtonRect = new Rect(
                x: container.xMax - CONNECT_BUTTON_WIDTH,
                y: container.yMin,
                width: CONNECT_BUTTON_WIDTH,
                height: CONNECT_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(connectButtonRect, "Phinix_settings_disconnectButton".Translate()))
            {
                // TODO: Disconnect from server
                conn = !conn;
            }
        }

        /// <summary>
        /// Draw an editable server address and connect button within a given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawDisconnectedServerDetails(Rect container)
        {
            // Server address box
            Rect serverAddressBoxRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: container.width - (CONNECT_BUTTON_WIDTH + DEFAULT_SPACING),
                height: SERVER_ADDRESS_BOX_HEIGHT
            );
            serverAddress = Widgets.TextField(serverAddressBoxRect, serverAddress);

            // Connect button
            Rect connectButtonRect = new Rect(
                x: container.xMax - CONNECT_BUTTON_WIDTH,
                y: container.yMin,
                width: CONNECT_BUTTON_WIDTH,
                height: CONNECT_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(connectButtonRect, "Phinix_settings_connectButton".Translate()))
            {
                // TODO: Try connect to a server
                conn = !conn;
            }
        }
    }
}
