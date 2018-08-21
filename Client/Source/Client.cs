using System;
using System.Reflection;
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
        public void Disconnect() => netClient.Disconnect();
        public void Connect(string address, int port) => netClient.Connect(address, port);
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
    }
}
