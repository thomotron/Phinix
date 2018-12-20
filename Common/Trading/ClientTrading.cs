using System;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Trading
{
    public class ClientTrading : Trading
    {
        /// <inheritdoc />
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        public event EventHandler<ThingReceivedEventArgs> OnThingReceived; 

        /// <summary>
        /// <c>NetClient</c> instance to bind events and send data through.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <c>ClientAuthenticator</c> instance to get a session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <c>ClientUserManager</c> instance to get a UUID from.
        /// </summary>
        private ClientUserManager userManager;

        public ClientTrading(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;
            
            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
        }
        
        public void SendThing(Thing thing)
        {
            // Do nothing unless connected, authenticated, and logged in
            if (!netClient.Connected || !authenticator.Authenticated || !userManager.LoggedIn) return;
            
            // Pack the thing
            Any packedThing = ProtobufPacketHelper.Pack(thing);
            
            // Send it on its way
            netClient.Send(MODULE_NAME, packedThing.ToByteArray());
        }

        /// <summary>
        /// Handles incoming packets from <c>NetCommon</c>.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Validate the incoming packet and discard it if validation fails
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientTrading).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            // Determine what to do with this packet type
            switch (typeUrl.Type)
            {
                case "Thing":
                    thingHandler(connectionId, message.Unpack<Thing>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Handles incoming <c>Thing</c>s.
        /// </summary>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="packet">Incoming <c>Thing</c></param>
        private void thingHandler(string connectionId, Thing packet)
        {
            OnThingReceived?.Invoke(this, new ThingReceivedEventArgs(packet));
        }
    }

    public class ThingReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Incoming <c>Thing</c>
        /// </summary>
        public Thing Thing;

        public ThingReceivedEventArgs(Thing thing)
        {
            this.Thing = thing;
        }
    }
}