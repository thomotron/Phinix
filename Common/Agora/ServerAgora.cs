using System;
using System.Collections.Generic;
using System.Linq;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Agora
{
    /// <summary>
    /// Server half of the Market, Bounties and Contracts tabs. Holds every player market, bounty and contract, and keeps
    /// every logged-in player's copy of them up to date.
    /// </summary>
    /// <remarks>
    /// Modelled on <c>Chat.ServerChat</c>, so compare the two if anything here is unclear.
    /// <para>
    /// This is a "partial" class, which means its code is split across more than one file. Player markets live in
    /// ServerAgora.Shops.cs, bounties in ServerAgora.Bounties.cs and contracts in ServerAgora.Contracts.cs, but C# glues
    /// them together into one class when it builds, so they share fields and can call each other's methods. It's purely
    /// to keep each file a manageable size.
    /// </para>
    /// </remarks>
    public partial class ServerAgora : Agora
    {
        /// <inheritdoc/>
        public override event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public override void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// <see cref="NetServer"/> instance to bind the packet handler to.
        /// </summary>
        private NetServer netServer;

        /// <summary>
        /// <see cref="ServerAuthenticator"/> instance used to check session validity.
        /// </summary>
        private ServerAuthenticator authenticator;

        /// <summary>
        /// <see cref="ServerUserManager"/> instance used to check login state and source UUID validity.
        /// </summary>
        private ServerUserManager userManager;


        /// <summary>
        /// Longest a bounty's or contract's note can be, in characters.
        /// </summary>
        public const int MAX_NOTE_LENGTH = 200;

        /// <summary>
        /// Initialises a new <see cref="ServerAgora"/> instance.
        /// </summary>
        /// <param name="netServer"><see cref="NetServer"/> to receive packets from</param>
        /// <param name="authenticator"><see cref="ServerAuthenticator"/> to check sessions with</param>
        /// <param name="userManager"><see cref="ServerUserManager"/> to check logins with</param>
        /// <param name="maxStockPerShop">Most stacks one player's market can hold</param>
        /// <param name="postRateLimitPosts">Most bounties one user can post within <paramref name="postRateLimitSeconds"/>. Markets allow several times this, since stocking one takes a few goes</param>
        /// <param name="postRateLimitSeconds">Length of the rate limit window in seconds</param>
        /// <param name="maxBountiesPerUser">Most bounties one user can have up at once</param>
        /// <param name="bountyExpiryDays">How many days a bounty stays up before its reward goes back to the poster. Unanswered contract proposals expire after the same time</param>
        /// <param name="maxContractsPerUser">Most contracts one user can be part of at once</param>
        /// <param name="contractsEnabled">Whether players can make new contracts</param>
        public ServerAgora(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, int maxStockPerShop = 30, int postRateLimitPosts = 2, int postRateLimitSeconds = 60, int maxBountiesPerUser = 2, int bountyExpiryDays = 7, int maxContractsPerUser = 3, bool contractsEnabled = false)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            // Set up player markets (in ServerAgora.Shops.cs)
            initShops(maxStockPerShop, postRateLimitPosts, postRateLimitSeconds);

            // The bounty half (in ServerAgora.Bounties.cs)
            initBounties(maxBountiesPerUser, bountyExpiryDays, postRateLimitPosts, postRateLimitSeconds);

            // And the contract part (in ServerAgora.Contracts.cs)
            initContracts(contractsEnabled, maxContractsPerUser, postRateLimitPosts, postRateLimitSeconds);

            // Ask to be handed every packet addressed to this module, and to be told when someone logs in
            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            userManager.OnLogin += loginHandler;
        }

        /// <summary>
        /// Sends the markets to a player as soon as they log in.
        /// </summary>
        private void loginHandler(object sender, ServerLoginEventArgs args)
        {
            sendSyncShops(args.ConnectionId, args.Uuid);

            // Send the bounty board too, plus anything that was delivered while they were away
            bountyLoginHandler(args.ConnectionId, args.Uuid);
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
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerAgora).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            RaiseLogEntry(new LogEventArgs(string.Format("Got a {0} from {1}", typeUrl.Type, connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.DEBUG));

            // Determine what to do with the packet, like a chain of if/elif in Python
            switch (typeUrl.Type)
            {
                // Player markets, handled in ServerAgora.Shops.cs
                case "OpenShopPacket":
                    openShopPacketHandler(connectionId, message.Unpack<OpenShopPacket>());
                    break;
                case "CloseShopPacket":
                    closeShopPacketHandler(connectionId, message.Unpack<CloseShopPacket>());
                    break;
                case "StockShopPacket":
                    stockShopPacketHandler(connectionId, message.Unpack<StockShopPacket>());
                    break;
                case "SetShopPricePacket":
                    setShopPricePacketHandler(connectionId, message.Unpack<SetShopPricePacket>());
                    break;
                case "WithdrawShopStockPacket":
                    withdrawShopStockPacketHandler(connectionId, message.Unpack<WithdrawShopStockPacket>());
                    break;
                case "CollectShopTillPacket":
                    collectShopTillPacketHandler(connectionId, message.Unpack<CollectShopTillPacket>());
                    break;
                case "BuyFromShopPacket":
                    buyFromShopPacketHandler(connectionId, message.Unpack<BuyFromShopPacket>());
                    break;

                // Bounty packets, handled in ServerAgora.Bounties.cs
                case "PostBountyPacket":
                    postBountyPacketHandler(connectionId, message.Unpack<PostBountyPacket>());
                    break;
                case "CancelBountyPacket":
                    cancelBountyPacketHandler(connectionId, message.Unpack<CancelBountyPacket>());
                    break;
                case "FulfilBountyPacket":
                    fulfilBountyPacketHandler(connectionId, message.Unpack<FulfilBountyPacket>());
                    break;

                // Settling held items after a lost connection, handled in ServerAgora.Escrow.cs
                case "CheckTokensPacket":
                    checkTokensPacketHandler(connectionId, message.Unpack<CheckTokensPacket>());
                    break;
                case "AckDeliveriesPacket":
                    ackDeliveriesPacketHandler(connectionId, message.Unpack<AckDeliveriesPacket>());
                    break;

                // Contract packets, handled in ServerAgora.Contracts.cs
                case "ProposeContractPacket":
                    proposeContractPacketHandler(connectionId, message.Unpack<ProposeContractPacket>());
                    break;
                case "AcceptContractPacket":
                    acceptContractPacketHandler(connectionId, message.Unpack<AcceptContractPacket>());
                    break;
                case "CancelContractPacket":
                    cancelContractPacketHandler(connectionId, message.Unpack<CancelContractPacket>());
                    break;
                case "DeliverContractPacket":
                    deliverContractPacketHandler(connectionId, message.Unpack<DeliverContractPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Narrows board entries down to the ones a player can see. On a companion that's only entries from players on
        /// the same main server (see <see cref="ServerUserManager.SameHomeServer"/>); anywhere else it's everything.
        /// </summary>
        /// <remarks>
        /// This reads the user store, so call it outside the board locks rather than inside them. Taking the two locks
        /// in different orders on different threads could leave each thread waiting for the other forever.
        /// </remarks>
        /// <param name="viewerUuid">Who's looking</param>
        /// <param name="entries">Markets, bounties, etc.</param>
        /// <param name="posterOf">Gets the player an entry belongs to</param>
        /// <returns>The entries they can see</returns>
        private IEnumerable<T> visibleTo<T>(string viewerUuid, IEnumerable<T> entries, Func<T, string> posterOf)
        {
            if (!userManager.CompanionMode) return entries;

            string viewerHome = userManager.HomeServerOf(viewerUuid);
            return entries.Where(entry => userManager.HomeServerOf(posterOf(entry)) == viewerHome).ToList();
        }

        /// <summary>
        /// Sends each logged-in player their own packet, for boards where a companion shows each main server's players
        /// a different set of entries. Without companion mode everyone sees the same thing, so it's packed once.
        /// </summary>
        /// <param name="pack">Builds the packet for a viewer's UUID</param>
        /// <param name="packetName">Packet name for the log if a send fails</param>
        private void broadcastPerViewer(Func<string, Any> pack, string packetName)
        {
            byte[] shared = userManager.CompanionMode ? null : pack(null).ToByteArray();
            foreach (string connectionId in userManager.GetConnections())
            {
                byte[] data = shared;
                if (data == null)
                {
                    if (!userManager.TryGetUuid(connectionId, out string viewerUuid)) continue;
                    data = pack(viewerUuid).ToByteArray();
                }

                if (!netServer.TrySend(connectionId, MODULE_NAME, data))
                {
                    RaiseLogEntry(new LogEventArgs(string.Format("Failed to send {0} to connection {1}", packetName, connectionId.Highlight(HighlightType.ConnectionID)), LogLevel.ERROR));
                }
            }
        }
    }
}
