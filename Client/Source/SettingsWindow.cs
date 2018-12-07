using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    class SettingsWindow : Window
    {
        private const float DEFAULT_SPACING = 10f;

        private const float SERVER_ADDRESS_LABEL_HEIGHT = 120f;
        private const float SERVER_ADDRESS_LABEL_WIDTH = 60f;

        private const float SERVER_ADDRESS_BOX_HEIGHT = 30f;

        private const float SERVER_PORT_LABEL_HEIGHT = 30f;
        private const float SERVER_PORT_LABEL_WIDTH = 30f;

        private const float SERVER_PORT_BOX_HEIGHT = 30f;
        private const float SERVER_PORT_BOX_WIDTH = 50f;

        private const float CONNECT_BUTTON_HEIGHT = 30f;
        private const float CONNECT_BUTTON_WIDTH = 120f;

        private const float USERNAME_BOX_HEIGHT = 30f;

        private const float USERNAME_SET_BUTTON_HEIGHT = 30f;
        private const float USERNAME_SET_BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(600f, 100f);

        private static string serverAddress = Client.Instance.ServerAddress;
        private static string serverPortString = Client.Instance.ServerPort.ToString();

        public override void DoWindowContents(Rect inRect)
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;

            // Server details (address and [dis]connect button) container
            Rect serverDetailRect = new Rect(
                x: inRect.xMin,
                y: inRect.yMin + 5f, // 5f offset to avoid overlapping close button
                width: inRect.width,
                height: SERVER_ADDRESS_BOX_HEIGHT
            );
            if (Client.Instance.Connected)
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
            Rect disconnectButtonRect = new Rect(
                x: container.xMax - CONNECT_BUTTON_WIDTH,
                y: container.yMin,
                width: CONNECT_BUTTON_WIDTH,
                height: CONNECT_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(disconnectButtonRect, "Phinix_settings_disconnectButton".Translate()))
            {
                Client.Instance.Disconnect();
            }
        }

        /// <summary>
        /// Draw an editable server address, editable server port, and connect button within a given <c>Rect</c>.
        /// </summary>
        /// <param name="container">Container to draw within</param>
        private void DrawDisconnectedServerDetails(Rect container)
        {
            // Address label
            Rect serverAddressLabelRect = new Rect(
                x: container.xMin,
                y: container.yMin,
                width: SERVER_ADDRESS_LABEL_WIDTH,
                height: SERVER_ADDRESS_LABEL_HEIGHT
            );
            Widgets.Label(serverAddressLabelRect, "Phinix_settings_addressLabel".Translate());

            // Server address box
            Rect serverAddressBoxRect = new Rect(
                x: container.xMin + (SERVER_ADDRESS_LABEL_WIDTH + DEFAULT_SPACING),
                y: container.yMin,
                width: container.width - (CONNECT_BUTTON_WIDTH + SERVER_PORT_BOX_WIDTH + SERVER_PORT_LABEL_WIDTH + SERVER_ADDRESS_LABEL_WIDTH + DEFAULT_SPACING * 4),
                height: SERVER_ADDRESS_BOX_HEIGHT
            );
            serverAddress = Widgets.TextField(serverAddressBoxRect, serverAddress);

            // Port label
            Rect serverPortLabelRect = new Rect(
                x: container.xMax - (CONNECT_BUTTON_WIDTH + SERVER_PORT_BOX_WIDTH + SERVER_PORT_LABEL_WIDTH + DEFAULT_SPACING * 2),
                y: container.yMin,
                width: SERVER_PORT_LABEL_WIDTH,
                height: SERVER_PORT_LABEL_HEIGHT
            );
            Widgets.Label(serverPortLabelRect, "Phinix_settings_portLabel".Translate());

            // Server port box
            Rect serverPortBoxRect = new Rect(
                x: container.xMax - (CONNECT_BUTTON_WIDTH + SERVER_PORT_BOX_WIDTH + DEFAULT_SPACING),
                y: container.yMin, 
                width: SERVER_PORT_BOX_WIDTH,
                height: SERVER_PORT_BOX_HEIGHT
            );
            serverPortString = Widgets.TextField(serverPortBoxRect, serverPortString, 5, new Regex("(^[0-9]{0,5}$)")); // Regex matches up to 5 digits or nothing at all

            // Connect button
            Rect connectButtonRect = new Rect(
                x: container.xMax - CONNECT_BUTTON_WIDTH,
                y: container.yMin,
                width: CONNECT_BUTTON_WIDTH,
                height: CONNECT_BUTTON_HEIGHT
            );
            if (Widgets.ButtonText(connectButtonRect, "Phinix_settings_connectButton".Translate()))
            {
                // Save the connection details to the client settings
                Client.Instance.ServerAddress = serverAddress;
                Client.Instance.ServerPort = int.Parse(serverPortString);

                // Run this on another thread otherwise the UI will lock up.
                new Thread(() => {
                    Client.Instance.Connect(serverAddress, int.Parse(serverPortString)); // Assume the port was safely validated by the regex
                }).Start();
            }
        }
    }
}
