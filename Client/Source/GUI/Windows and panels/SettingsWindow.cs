using System.Text.RegularExpressions;
using System.Threading;
using PhinixClient.GUI;
using UnityEngine;
using Utils;
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

        public override Vector2 InitialSize => new Vector2(600f, 156f); // (30f * rows) + (10f * (rows - 1)) + 36f

        private static string serverAddress = Client.Instance.ServerAddress;
        private static string serverPortString = Client.Instance.ServerPort.ToString();

        private VerticalFlexContainer contents;

        public SettingsWindow()
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;

            // Create a flex container to hold our settings
            contents = new VerticalFlexContainer(DEFAULT_SPACING);

            // Server details (address and [dis]connect button) container
            contents.Add(
                new ConditionalContainer(
                    childIfTrue: GenerateConnectedServerDetails(),
                    childIfFalse: GenerateDisconnectedServerDetails(),
                    condition: () => Client.Instance.Connected
                )
            );

            // Display name and preview
            contents.Add(
                new ConditionalContainer(
                    childIfTrue: new VerticalFlexContainer(
                        contents: new Displayable[]{GenerateEditableDisplayName(), GenerateNamePreview()},
                        spacing: DEFAULT_SPACING
                    ),
                    childIfFalse: new BlankWidget(),
                    condition: () => Client.Instance.Online
                )
            );
        }

            // Calculate height and constrain the container so we have even row heights with fluid contents
            float contentHeight = 0f;
            foreach (Displayable item in flexContainer.Contents)
            {
                contentHeight += item.IsFluidHeight ? ROW_HEIGHT : item.CalcHeight(inRect.width);
            }
            contentHeight += (flexContainer.Contents.Count - 1) * DEFAULT_SPACING;
            HeightContainer heightContainer = new HeightContainer(flexContainer, contentHeight);

            // Draw the container with 5f padding at the top to avoid clipping with the close button
            // flexContainer.Draw(inRect.BottomPartPixels(inRect.height - 5f));
            heightContainer.Draw(inRect.BottomPartPixels(inRect.height - 5f));
        }

        /// <summary>
        /// Generates a non-editable server address and disconnect button.
        /// </summary>
        /// <returns><see cref="HorizontalFlexContainer"/> containing connected server details</returns>
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
        /// <returns><see cref="HorizontalFlexContainer"/> containing an editable server address, editable server port, and connect button</returns>
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
        /// <returns><see cref="HorizontalFlexContainer"/> containing an editable display name field and a button to apply the changes</returns>
        private HorizontalFlexContainer GenerateEditableDisplayName()
        {
            // Create a flex container as our 'row' to store the editable name field in
            HorizontalFlexContainer row = new HorizontalFlexContainer();

            // Editable display name text box
            row.Add(
                new TextFieldWidget(
                    text: Client.Instance.DisplayName,
                    onChange: newDisplayName => Client.Instance.DisplayName = newDisplayName
                )
            );

            // Set display name button
            row.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_settings_setDisplayNameButton".Translate(),
                        clickAction: () => Client.Instance.UpdateDisplayName(Client.Instance.DisplayName)
                    ),
                    width: DISPLAY_NAME_SET_BUTTON_WIDTH
                )
            );

            // Return the generated row
            return row;
        }

        /// <summary>
        /// Generates a display name preview label.
        /// </summary>
        /// <returns></returns>
        private HorizontalScrollContainer GenerateNamePreview()
        {
            // Create a scroll container to store the text widget in
            HorizontalScrollContainer row = new HorizontalScrollContainer(
                new TextWidget(
                    text: "Phinix_settings_displayNamePreview".Translate(Client.Instance.DisplayName).Resolve(),
                    wrap: false
                )
            );

            // Return the generated row
            return row;
        }
    }
}
