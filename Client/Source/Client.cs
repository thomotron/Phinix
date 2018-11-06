using System;
using System.Reflection;
using Authentication;
using Connections;
using HugsLib;
using HugsLib.Settings;

namespace PhinixClient
{
    public class Client : ModBase
    {
        public static Client Instance;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Client)).GetName().Version;

        public override string ModIdentifier => "Phinix";

        private NetClient netClient;
        public bool Connected => netClient.Connected;
        public void Send(string module, byte[] serialisedMessage) => netClient.Send(module, serialisedMessage);

        private SettingHandle<string> serverAddressHandle;
        public string ServerAddress
        {
            get => serverAddressHandle.Value;
            set => serverAddressHandle.Value = value;
        }
        private SettingHandle<int> serverPortHandle;
        public int ServerPort
        {
            get => serverPortHandle.Value;
            set => serverPortHandle.Value = value;
        }

        /// <summary>
        /// Called by HugsLib shortly after the mod is loaded.
        /// Used for initial setup only.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Client.Instance = this;

            // Load in Settings
            serverAddressHandle = Settings.GetHandle(
                settingName: "serverAddress",
                title: "Server Address",
                description: null,
                defaultValue: "localhost"
            );
            serverPortHandle = Settings.GetHandle(
                settingName: "serverPort",
                title: "Server Port",
                description: null,
                defaultValue: 16180,
                validator: value => int.TryParse(value, out _)
            );

            // Set up our module instances
            this.netClient = new NetClient();

            // Connect to the server set in the config
            try
            {
                this.netClient.Connect(ServerAddress, ServerPort);
            }
            catch (Exception)
            {
                Logger.Message("Could not connect to {0}:{1}", ServerAddress, ServerPort);
            }
        }

        /// <summary>
        /// Called by Unity on every physics update.
        /// Used for synchronisation.
        /// </summary>
        public override void FixedUpdate()
        {
            base.FixedUpdate();

        }

        /// <summary>
        /// Attempts to connect and then authenticate with the server at the given address and port.
        /// This will disconnect from the current server, if any.
        /// </summary>
        /// <param name="address">Server address</param>
        /// <param name="port">Server port</param>
        public void Connect(string address, int port)
        {
            if (Connected)
            {
                Disconnect();
            }

            try
            {
                netClient.Connect(address, port);
            }
            catch
            {
                // We shouldn't try to authenticate if we hit an error
                return;
            }

            Packet hello = Authenticator.GetHelloPacket();
        }

        /// <summary>
        /// If connected, disconnects from the current server.
        /// </summary>
        public void Disconnect()
        {
            netClient.Disconnect();
        }
    }
}
