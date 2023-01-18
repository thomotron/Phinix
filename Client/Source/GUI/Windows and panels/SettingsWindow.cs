using System;
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

        public override Vector2 InitialSize => new Vector2(600f, 156f); // (30f * rows) + (10f * (rows - 1)) + 36f

        private static string serverAddress = Client.Instance.ServerAddress;
        private static string serverPortString = Client.Instance.ServerPort.ToString();

        /// <summary>
        /// The pre-generated window contents.
        /// </summary>
        private VerticalFlexContainer contents;
        /// <summary>
        /// Whether an update call to <see cref="contents"/> has been requested by <see cref="updateOnEventHandler"/>.
        /// </summary>
        private bool needsUpdate = false;

        public SettingsWindow()
        {
            doCloseX = true;
            doCloseButton = false;
            doWindowBackground = true;
            draggable = true;

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
                    childIfTrue: GenerateEditableDisplayName(),
                    childIfFalse: new BlankWidget(),
                    condition: () => Client.Instance.Online
                )
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (needsUpdate)
            {
                // Update contents and reset the flag
                contents.Update();
                needsUpdate = false;
            }

            // Calculate height and constrain the container so we have even row heights with fluid contents
            float contentHeight = 0f;
            foreach (Displayable item in contents.Contents)
            {
                contentHeight += item.IsFluidHeight ? ROW_HEIGHT : item.CalcHeight(inRect.width);
            }
            contentHeight += (contents.Contents.Count - 1) * DEFAULT_SPACING;
            HeightContainer heightContainer = new HeightContainer(contents, contentHeight);

            // Draw the container with 5f padding at the top to avoid clipping with the close button
            heightContainer.Draw(inRect.BottomPartPixels(inRect.height - 5f));
        }

        /// <inheritdoc />
        public override void PreOpen()
        {
            base.PreOpen();

            // Bind to events
            Client.Instance.OnConnecting += updateOnEventHandler;
            Client.Instance.OnDisconnect += updateOnEventHandler;
            Client.Instance.OnAuthenticationSuccess += updateOnEventHandler;
            Client.Instance.OnAuthenticationFailure += updateOnEventHandler;
            Client.Instance.OnLoginSuccess += updateOnEventHandler;
            Client.Instance.OnLoginFailure += updateOnEventHandler;

            // Invalidate content to compensate for any missed events
            needsUpdate = true;
        }

        /// <inheritdoc />
        public override void PostClose()
        {
            base.PostClose();

            // Unbind from events
            Client.Instance.OnConnecting -= updateOnEventHandler;
            Client.Instance.OnDisconnect -= updateOnEventHandler;
            Client.Instance.OnAuthenticationSuccess -= updateOnEventHandler;
            Client.Instance.OnAuthenticationFailure -= updateOnEventHandler;
            Client.Instance.OnLoginSuccess -= updateOnEventHandler;
            Client.Instance.OnLoginFailure -= updateOnEventHandler;
        }

        /// <summary>
        /// Refreshes the GUI content.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="args">Event arguments</param>
        private void updateOnEventHandler(object sender, EventArgs args)
        {
            needsUpdate = true;
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
                new DynamicTextWidget(
                    textCallback: () => "Phinix_settings_connectedToLabel".Translate(serverAddress),
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
                    initialText: serverAddress,
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
                        initialText: serverPortString,
                        onChange: newPortString => serverPortString = newPortString,
                        validator: new Regex("(^[0-9]{0,5}$)")
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
        /// Generates an editable display name field, a button to apply the changes, and a preview.
        /// </summary>
        /// <returns><see cref="Displayable"/> containing an editable display name field, a button to apply the changes, and a preview</returns>
        private Displayable GenerateEditableDisplayName()
        {
            // Make the name preview early so we can bind to it's update method
            DynamicTextWidget namePreview = new DynamicTextWidget(
                textCallback: () => "Phinix_settings_displayNamePreview".Translate(Client.Instance.DisplayName).Resolve(),
                wrap: false
            );

            // Create a column to store the editable portion and preview in
            VerticalFlexContainer column = new VerticalFlexContainer();

            // Create a flex container as our 'row' to store the editable name field in
            HorizontalFlexContainer editableRow = new HorizontalFlexContainer();

            // Editable display name text box
            editableRow.Add(
                new TextFieldWidget(
                    initialText: Client.Instance.DisplayName,
                    onChange: newDisplayName =>
                    {
                        Client.Instance.DisplayName = newDisplayName;
                        namePreview.Update();
                    }
                )
            );

            // Set display name button
            editableRow.Add(
                new Container(
                    new ButtonWidget(
                        label: "Phinix_settings_setDisplayNameButton".Translate(),
                        clickAction: () => Client.Instance.UpdateDisplayName(Client.Instance.DisplayName)
                    ),
                    width: DISPLAY_NAME_SET_BUTTON_WIDTH
                )
            );

            // Wrap the editable portion in a container to enforce height and add it to the column
            column.Add(
                new HeightContainer(
                    child: editableRow,
                    height: ROW_HEIGHT
                )
            );

            // Display name preview
            column.Add(new HorizontalScrollContainer(namePreview));

            // Return the generated column
            return column;
        }
    }
}
