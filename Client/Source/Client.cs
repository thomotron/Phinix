using System;
using System.Reflection;
using Connections;
using HugsLib;

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

        /// <summary>
        /// Called by HugsLib shortly after the mod is loaded.
        /// Used for initial setup only.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Client.Instance = this;

            // Set up our module instances
            this.netClient = new NetClient();
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
