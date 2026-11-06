using System;
using System.Collections.Generic;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Trading;
using UserManagement;
using Utils;

namespace Agora
{
    /// <summary>
    /// Client half of the Market, Bounties and Contracts tabs. Keeps a copy of what the server sends and passes this
    /// player's requests on to it.
    /// </summary>
    /// <remarks>
    /// Modelled on <c>Chat.ClientChat</c>, so compare the two if anything here is unclear.
    /// <para>
    /// Like ServerAgora, this is a "partial" class: player markets live in ClientAgora.Shops.cs, bounties in
    /// ClientAgora.Bounties.cs and contracts in ClientAgora.Contracts.cs.
    /// </para>
    /// </remarks>
    public partial class ClientAgora : Agora
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <see cref="NetClient"/> instance to bind the packet handler to.
        /// </summary>
        private NetClient netClient;

        /// <summary>
        /// <see cref="ClientAuthenticator"/> to get the session ID from.
        /// </summary>
        private ClientAuthenticator authenticator;

        /// <summary>
        /// <see cref="ClientUserManager"/> to get our UUID and login state from.
        /// </summary>
        private ClientUserManager userManager;

        /// <summary>
        /// Whether the server has Agora (markets, bounties and contracts). An Agora server sends its markets as soon as
        /// we log in; an original Phinix server never does, so this stays false there.
        /// </summary>
        public bool Available { get; private set; }

        /// <summary>
        /// Initialises a new <see cref="ClientAgora"/> instance.
        /// </summary>
        /// <param name="netClient"><see cref="NetClient"/> to send and receive packets with</param>
        /// <param name="authenticator"><see cref="ClientAuthenticator"/> to get the session ID from</param>
        /// <param name="userManager"><see cref="ClientUserManager"/> to get our UUID from</param>
        public ClientAgora(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;

            netClient.RegisterPacketHandler(MODULE_NAME, packetHandler);
            netClient.OnDisconnect += disconnectHandler;
        }

        /// <summary>
        /// Forgets everything when we disconnect, since it may change before we reconnect.
        /// </summary>
        private void disconnectHandler(object sender, EventArgs e)
        {
            Available = false;

            // The markets (in ClientAgora.Shops.cs)
            clearShops();

            // Forget the bounty board too (in ClientAgora.Bounties.cs)
            clearBounties();

            // And our contracts (in ClientAgora.Contracts.cs)
            clearContracts();
        }

        /// <summary>
        /// Handles incoming packets, sending each one to the right handler by its type.
        /// </summary>
        /// <param name="module">Target module</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            // Discard packet if it fails validation
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientAgora).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            switch (typeUrl.Type)
            {
                // Player markets, handled in ClientAgora.Shops.cs
                case "SyncShopsPacket":
                    RaiseLogEntry(new LogEventArgs("Got a SyncShopsPacket", LogLevel.DEBUG));
                    syncShopsPacketHandler(message.Unpack<SyncShopsPacket>());
                    break;
                case "ShopResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a ShopResponsePacket", LogLevel.DEBUG));
                    OnShopResponse?.Invoke(this, message.Unpack<ShopResponsePacket>());
                    break;

                // Bounty packets, handled in ClientAgora.Bounties.cs
                case "SyncBountiesPacket":
                    RaiseLogEntry(new LogEventArgs("Got a SyncBountiesPacket", LogLevel.DEBUG));
                    syncBountiesPacketHandler(message.Unpack<SyncBountiesPacket>());
                    break;
                case "PostBountyResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a PostBountyResponsePacket", LogLevel.DEBUG));
                    OnPostBountyResponse?.Invoke(this, message.Unpack<PostBountyResponsePacket>());
                    break;
                case "FulfilBountyResponsePacket":
                    RaiseLogEntry(new LogEventArgs("Got a FulfilBountyResponsePacket", LogLevel.DEBUG));
                    OnFulfilBountyResponse?.Invoke(this, message.Unpack<FulfilBountyResponsePacket>());
                    break;
                case "BountyDeliveryPacket":
                    RaiseLogEntry(new LogEventArgs("Got a BountyDeliveryPacket", LogLevel.DEBUG));
                    OnBountyDelivery?.Invoke(this, message.Unpack<BountyDeliveryPacket>());
                    break;

                case "TokenResultsPacket":
                    OnTokenResults?.Invoke(this, message.Unpack<TokenResultsPacket>());
                    break;

                // Contract packets, handled in ClientAgora.Contracts.cs
                case "SyncContractsPacket":
                    syncContractsPacketHandler(message.Unpack<SyncContractsPacket>());
                    break;
                case "ProposeContractResponsePacket":
                    OnProposeContractResponse?.Invoke(this, message.Unpack<ProposeContractResponsePacket>());
                    break;
                case "DeliverContractResponsePacket":
                    OnDeliverContractResponse?.Invoke(this, message.Unpack<DeliverContractResponsePacket>());
                    break;

                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
    }
}
