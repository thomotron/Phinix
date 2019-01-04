using System.Text.RegularExpressions;
using System.Threading;
using PhinixClient.GUI;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    class SettingsWindow : Window
    {
        private const float DEFAULT_SPACING = 10f;
        
        private const float ROW_HEIGHT = 30f;

        private const float SERVER_ADDRESS_LABEL_WIDTH = 60f;

        private const float SERVER_PORT_LABEL_WIDTH = 30f;

        private const float SERVER_PORT_BOX_WIDTH = 50f;

        private const float CONNECT_BUTTON_WIDTH = 120f;

        private const float DISPLAY_NAME_SET_BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(600f, 120f);

        private static string serverAddress = Client.Instance.ServerAddress;
        private static string serverPortString = Client.Instance.ServerPort.ToString();

        private static string displayName = Client.Instance.DisplayName;

        public override void DoWindowContents(Rect inRect)
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;
            
            // Create a flex container to hold our settings
            VerticalFlexContainer flexContainer = new VerticalFlexContainer(DEFAULT_SPACING);

            // Server details (address and [dis]connect button) container
            if (Client.Instance.Connected)
            {
                flexContainer.Add(GenerateConnectedServerDetails());
            }
            else
            {
                flexContainer.Add(GenerateDisconnectedServerDetails());
            }
            
            // Display name
            if (Client.Instance.Online)
            {
                flexContainer.Add(GenerateEditableDisplayName());
            };
            
            // Constrain the flex container within another container to avoid widgets becoming excessively large
            Container container = new Container(
                child: flexContainer,
                height: ROW_HEIGHT * flexContainer.Contents.Count + DEFAULT_SPACING * (flexContainer.Contents.Count - 1)
            );
            
            // Draw the container with 5f padding at the top to avoid clipping with the close button
            container.Draw(inRect.BottomPartPixels(inRect.height - 5f));
        }

        /// <summary>
        /// Generates a non-editable server address and disconnect button.
        /// </summary>
        /// <returns><c>HorizontalFlexContainer</c> containing connected server details</returns>
        private HorizontalFlexContainer GenerateConnectedServerDetails()
        {
            // Create a flex container as our 'row' to store elements in
            HorizontalFlexContainer row = new HorizontalFlexContainer();
            
            // Server address label
            row.Add(
                new TextWidget(
                    text: "Phinix_settings_connectedToLabel".Translate(serverAddress),
                    anchor: TextAnchor.MiddleLeft
                )
            );

            // Disconnect button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_settings_disconnectButton".Translate(),
                        clickAction: () => Client.Instance.Disconnect()
                    ),
                    width: CONNECT_BUTTON_WIDTH
                )
            );

            // Return the generated row
            return row;
        }

        /// <summary>
        /// Generates an editable server address, editable server port, and connect button.
        /// </summary>
        /// <returns><c>HorizontalFlexContainer</c> containing an editable server address, editable server port, and connect button</returns>
        private HorizontalFlexContainer GenerateDisconnectedServerDetails()
        {
            // Create a flex container as our 'row' to store elements in
            HorizontalFlexContainer row = new HorizontalFlexContainer();
            
            // Address label
            row.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_settings_addressLabel".Translate(),
                        anchor: TextAnchor.MiddleLeft
                    ),
                    width: SERVER_ADDRESS_LABEL_WIDTH
                )
            );

            // Server address box
            row.Add(
                new TextFieldWidget(
                    text: serverAddress,
                    onChange: newAddress => serverAddress = newAddress 
                )
            );

            // Port label
            row.Add(
                new Container(
                    new TextWidget(
                        text: "Phinix_settings_portLabel".Translate(),
                        anchor: TextAnchor.MiddleLeft
                    ),
                    width: SERVER_PORT_LABEL_WIDTH
                )
            );

            // Server port box
            row.Add(
                new Container(
                    new TextFieldWidget(
                        text: serverPortString,
                        onChange: newPortString =>
                        {
                            if (new Regex("(^[0-9]{0,5}$)").IsMatch(newPortString))
                            {
                                serverPortString = newPortString;
                            }
                        }
                    ),
                    width: SERVER_PORT_BOX_WIDTH
                )
            );

            // Connect button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_settings_connectButton".Translate(),
                        clickAction: () =>
                        {
                            // Save the connection details to the client settings
                            Client.Instance.ServerAddress = serverAddress;
                            Client.Instance.ServerPort = int.Parse(serverPortString);

                            // Run this on another thread otherwise the UI will lock up.
                            new Thread(() => {
                                Client.Instance.Connect(serverAddress, int.Parse(serverPortString)); // Assume the port was safely validated by the regex
                            }).Start();
                        }
                    ),
                    width: CONNECT_BUTTON_WIDTH
                )
            );
            
            // Return the generated row
            return row;
        }
        
        /// <summary>
        /// Generates an editable display name field and a button to apply the changes.
        /// </summary>
        /// <returns><c>HorizontalFlexContainer</c> containing an editable display name field and a button to apply the changes</returns>
        private HorizontalFlexContainer GenerateEditableDisplayName()
        {
            // Create a flex container as our 'row' to store elements in
            HorizontalFlexContainer row = new HorizontalFlexContainer();
            
            // Editable display name text box
            row.Add(
                new TextFieldWidget(
                    text: displayName,
                    onChange: newDisplayName => displayName = newDisplayName
                )
            );
            
            // Set display name button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_settings_setDisplayNameButton".Translate(),
                        clickAction: () => Client.Instance.UpdateDisplayName(displayName)
                    ),
                    width: DISPLAY_NAME_SET_BUTTON_WIDTH
                )
            );
            
            // Return the generated row
            return row;
        }
    }
}
