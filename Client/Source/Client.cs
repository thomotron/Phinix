using Authentication;
using Agora;
using Chat;
using Connections;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Trading;
using UnityEngine;
using UserManagement;
using Utils;
using Verse;
using Verse.Sound;
using Thing = Verse.Thing;

namespace PhinixClient
{
    public class Client : Mod
    {
        public static Client Instance;
        public static readonly Version Version = typeof(Client).Assembly.GetName().Version;
        public const string PackageId = "Thomotron.Phinix";

        public void Log(LogEventArgs e) => ILoggableHandler(null, e);

        public override string SettingsCategory() => "Phinix";

        #region Modules
        private NetClient netClient;
        public bool Connected => netClient.Connected;
        public void Send(string module, byte[] serialisedMessage) => netClient.Send(module, serialisedMessage);
        public event EventHandler OnConnecting;
        public event EventHandler OnDisconnect;

        private ClientAuthenticator authenticator;
        public bool Authenticated => authenticator.Authenticated;
        public string SessionId => authenticator.SessionId;
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationSuccess;
        public event EventHandler<AuthenticationEventArgs> OnAuthenticationFailure;

        private ClientUserManager userManager;
        public bool LoggedIn => userManager.LoggedIn;
        public string Uuid => userManager.Uuid;
        public bool TryGetDisplayName(string uuid, out string displayName) => userManager.TryGetDisplayName(uuid, out displayName);
        public bool TryGetUser(string uuid, out ImmutableUser user) => userManager.TryGetUser(uuid, out user);
        public string[] GetUserUuids(bool loggedIn = false) => userManager.GetUuids(loggedIn);
        public ImmutableUser[] GetUsers(bool loggedIn = false) => userManager.GetUsers(loggedIn);
        public event EventHandler<LoginEventArgs> OnLoginSuccess;
        public event EventHandler<LoginEventArgs> OnLoginFailure;
        public event EventHandler<UserDisplayNameChangedEventArgs> OnUserDisplayNameChanged;
        public event EventHandler<UserLoginStateChangedEventArgs> OnUserLoggedIn;
        public event EventHandler<UserLoginStateChangedEventArgs> OnUserLoggedOut;
        public event EventHandler<UserCreatedEventArgs> OnUserCreated;
        public event EventHandler OnUserSync;

        // False until the modules exist: on RimWorld 1.7 the game can start updating before the mod has finished loading
        public bool Online => netClient != null && authenticator != null && userManager != null && Connected && Authenticated && LoggedIn;

        private ClientChat chat;
        public void SendMessage(string message) => chat.Send(message);
        public void MarkAsRead() => chat.MarkAsRead();
        public UIChatMessage[] GetUnreadChatMessages(bool markAsRead = true) => GetChatMessages(markAsRead, true);
        public int UnreadMessages => chat.UnreadMessages;
        public int UnreadMessagesExcludingBlocked => chat.GetUnreadMessagesExcluding(Settings.BlockedUsers);
        public event EventHandler<UIChatMessageEventArgs> OnChatMessageReceived;
        public event EventHandler OnChatSync;

        // Player markets. Like the chat lines above, these are one-line shortcuts ("=>") that pass straight through
        // to ClientAgora, so the UI only ever has to talk to Client.Instance.
        // They go to boardAgora: the main server's Agora, or the companion's when the main server has none
        private ClientAgora agora;
        public Agora.Shop[] GetShops() => boardAgora.GetShops();
        public bool OpenShop(string name) => boardAgora.OpenShop(name);
        public bool CloseShop() => boardAgora.CloseShop();
        public bool StockShop(string token, IEnumerable<ShopStock> stock) => boardAgora.StockShop(token, stock);
        public bool SetShopPrice(string stockId, int pricePerUnit) => boardAgora.SetShopPrice(stockId, pricePerUnit);
        public bool WithdrawShopStock(string stockId, int count) => boardAgora.WithdrawShopStock(stockId, count);
        public bool CollectShopTill() => boardAgora.CollectShopTill();
        public bool BuyFromShop(string token, string shopId, string stockId, int count, int expectedPricePerUnit, IEnumerable<ProtoThing> payment) => boardAgora.BuyFromShop(token, shopId, stockId, count, expectedPricePerUnit, payment);
        public event EventHandler OnShopsChanged;

        /// <summary>
        /// Our own market, or null if we haven't opened one.
        /// </summary>
        public Agora.Shop MyShop => GetShops().FirstOrDefault(shop => shop.OwnerUuid == Uuid);

        /// <summary>
        /// One market by ID, or null if it's gone (closed, or we can't see it any more).
        /// </summary>
        public Agora.Shop GetShop(string shopId) => GetShops().FirstOrDefault(shop => shop.ShopId == shopId);

        // Bounties, passed straight through to ClientAgora (its bounty half is in ClientAgora.Bounties.cs)
        public Agora.Bounty[] GetBounties() => boardAgora.GetBounties();
        public bool PostBounty(string token, IEnumerable<ProtoThing> wantedItems, IEnumerable<ProtoThing> rewardItems, string note) => boardAgora.PostBounty(token, wantedItems, rewardItems, note);
        public void CancelBounty(string bountyId) => boardAgora.CancelBounty(bountyId);
        public bool FulfilBounty(string token, string bountyId, IEnumerable<ProtoThing> items) => boardAgora.FulfilBounty(token, bountyId, items);

        // Contracts, passed straight through to ClientAgora (its contract part is in ClientAgora.Contracts.cs)
        public Agora.Contract[] GetContracts() => boardAgora.GetContracts();

        /// <summary>
        /// Whether to show contracts at all: when the server has them turned on, or when we're still part of one
        /// from before they were turned off (so we can finish it).
        /// </summary>
        public bool ShowContracts => boardAgora.ContractsEnabled || GetContracts().Length > 0;

        /// <summary>
        /// Whether the server lets us propose new contracts.
        /// </summary>
        public bool ContractsEnabled => boardAgora.ContractsEnabled;

        /// <summary>
        /// Whether the main server or the companion has the market and bounty boards. False on an original Phinix
        /// server with no companion, which would never answer a post, so the UI hides them there.
        /// </summary>
        public bool MarketAvailable => boardAgora.Available;

        /// <summary>
        /// Whether the main server or the companion can take gifts. False on an original Phinix server with no
        /// companion: a gift sent there would be held waiting for an answer that never comes, so the UI hides the option.
        /// </summary>
        public bool GiftsAvailable => giftHermes.Available && FeatureEnabled(Features.GIFTS);

        /// <summary>
        /// Whether a feature is switched on. Either server can switch some off in server.conf, to release them a few at
        /// a time: the main server, or the companion we're linked to (which is where the new features live when the
        /// main server runs the original Phinix, so its switches count too). Everything counts as off until we've
        /// logged in, so nothing a server might be holding back shows while logged out, and as on with servers that
        /// can't switch them.
        /// </summary>
        /// <param name="feature">Feature name from <see cref="Features"/></param>
        public bool FeatureEnabled(string feature) => Online && !userManager.IsFeatureDisabled(feature) && !(CompanionOnline && companionUserManager.IsFeatureDisabled(feature));

        /// <summary>
        /// Other players' mod lists (lower-case package IDs) by UUID, as the server last told us. A player maps to null
        /// when the server doesn't know theirs. See <see cref="TryGetModList"/>.
        /// </summary>
        private Dictionary<string, HashSet<string>> modLists = new Dictionary<string, HashSet<string>>();
        /// <summary>
        /// When we last asked for each player's mod list, in real seconds since the game started, so a window drawing
        /// every frame doesn't ask every frame.
        /// </summary>
        private Dictionary<string, float> modListAsked = new Dictionary<string, float>();
        /// <summary>
        /// Every mod's name by lower-case package ID, from the mod lists the server has sent us, so the player card can
        /// name mods we don't have. Uses <see cref="modListsLock"/>.
        /// </summary>
        private Dictionary<string, string> modNames = new Dictionary<string, string>();
        private object modListsLock = new object();
        private const float MOD_LIST_REFRESH_SECONDS = 60f;

        /// <summary>
        /// Gets the mods another player has loaded, asking the server for them if we don't have them yet (or haven't
        /// asked for a minute). Must be called from the main thread.
        /// </summary>
        /// <param name="uuid">The player's UUID</param>
        /// <param name="packageIds">Their mods' lower-case package IDs</param>
        /// <returns>Whether we know their mods. False until the server answers, or if it can't</returns>
        public bool TryGetModList(string uuid, out HashSet<string> packageIds)
        {
            bool ask;
            lock (modListsLock)
            {
                modLists.TryGetValue(uuid, out packageIds);
                ask = !modListAsked.TryGetValue(uuid, out float askedAt) || Time.realtimeSinceStartup - askedAt > MOD_LIST_REFRESH_SECONDS;
                if (ask) modListAsked[uuid] = Time.realtimeSinceStartup;
            }

            // Ask both servers: an original Phinix main server never answers, but a companion can
            if (ask)
            {
                userManager.RequestModList(uuid);
                companionUserManager.RequestModList(uuid);
            }

            return packageIds != null;
        }

        /// <summary>
        /// The mods we have loaded, to send when logging in.
        /// </summary>
        private static List<ModInfo> ownMods() => LoadedModManager.RunningMods
            .Select(mod => new ModInfo { PackageId = mod.PackageId ?? "", Name = mod.Name ?? "" })
            .ToList();

        private void modListReceived(object sender, ModListEventArgs args)
        {
            // Don't let an unknown answer from one server wipe out a known one from the other
            lock (modListsLock)
            {
                if (args.Known) modLists[args.Uuid] = new HashSet<string>(args.Mods.Select(mod => mod.PackageId.ToLowerInvariant()));
                else if (!modLists.ContainsKey(args.Uuid)) modLists[args.Uuid] = null;

                foreach (ModInfo mod in args.Mods)
                {
                    if (!string.IsNullOrEmpty(mod.Name)) modNames[mod.PackageId.ToLowerInvariant()] = mod.Name;
                }
            }
        }

        /// <summary>
        /// A mod's name from another player's mod list, or its package ID if no list we've seen named it.
        /// </summary>
        /// <param name="packageId">Lower-case package ID</param>
        public string ModName(string packageId)
        {
            lock (modListsLock) return modNames.TryGetValue(packageId, out string name) ? name : packageId;
        }
        public bool ProposeContract(string token, string supplierUuid, IEnumerable<ProtoThing> goods, IEnumerable<ProtoThing> paymentPerDelivery, IEnumerable<ProtoThing> heldPayment, int totalDeliveries, int intervalHours, string note) => boardAgora.ProposeContract(token, supplierUuid, goods, paymentPerDelivery, heldPayment, totalDeliveries, intervalHours, note);
        public void AcceptContract(string contractId) => boardAgora.AcceptContract(contractId);
        public void CancelContract(string contractId) => boardAgora.CancelContract(contractId);
        public bool DeliverContract(string token, string contractId, IEnumerable<ProtoThing> items) => boardAgora.DeliverContract(token, contractId, items);

        /// <summary>
        /// Contracts proposed to us that we haven't looked at yet. The Contracts tab's "New" count.
        /// </summary>
        public int NewContractCount => GetVisibleContracts().Count(contract => contract.State == ContractState.Proposed && contract.SupplierUuid == Uuid && !seenContractIds.Contains(contract.ContractId));

        /// <summary>
        /// The players we could propose a contract to, leaving out ourselves and anyone we've blocked.
        /// </summary>
        /// <remarks>
        /// When contracts go through the companion, that's only players from our main server who have linked to it: the
        /// companion refuses anyone else. A big stock server like phinix.chat knows thousands of players who've never
        /// heard of Phime, so listing them all made a menu so long the game froze building it. When the main server
        /// handles contracts itself, it's everyone online plus our friends, so the list stays short there too.
        /// </remarks>
        /// <returns>Possible suppliers, offline ones included where they can still get a proposal</returns>
        public ImmutableUser[] GetContractPartners()
        {
            IEnumerable<ImmutableUser> users = userManager.GetUsers();
            if (boardAgora == companionAgora)
            {
                HashSet<string> linked = new HashSet<string>(companionUserManager.GetUuids());
                users = users.Where(user => linked.Contains(user.Uuid));
            }
            else
            {
                users = users.Where(user => user.LoggedIn || IsFriend(user.Uuid));
            }

            return users.Where(user => user.Uuid != Uuid && !IsBlocked(user.Uuid)).ToArray();
        }

        /// <summary>
        /// Whether we could propose a contract to this player (see <see cref="GetContractPartners"/>).
        /// </summary>
        public bool CanProposeContractTo(string uuid) => GetContractPartners().Any(user => user.Uuid == uuid);

        /// <summary>
        /// Whether we've blocked this player.
        /// </summary>
        public bool IsBlocked(string uuid) => uuid != null && Settings.BlockedUsers.Contains(uuid);

        /// <summary>
        /// Markets, without those run by players we've blocked.
        /// </summary>
        public Agora.Shop[] GetVisibleShops() => GetShops().Where(shop => !IsBlocked(shop.OwnerUuid)).ToArray();

        /// <summary>
        /// Bounties, without those posted by players we've blocked.
        /// </summary>
        public Agora.Bounty[] GetVisibleBounties() => GetBounties().Where(bounty => !IsBlocked(bounty.PosterUuid)).ToArray();

        /// <summary>
        /// Contracts, without proposals to or from players we've blocked (<see cref="enforceBlocks"/> turns those down).
        /// Contracts already under way stay, since goods and silver are tied up in them and either side can still end them.
        /// </summary>
        public Agora.Contract[] GetVisibleContracts() => GetContracts()
            .Where(contract => contract.State != ContractState.Proposed || (!IsBlocked(contract.BuyerUuid) && !IsBlocked(contract.SupplierUuid)))
            .ToArray();

        /// <summary>
        /// Marks every contract as seen, clearing the Contracts tab's "New" count.
        /// </summary>
        public void MarkContractsSeen() => seenContractIds.UnionWith(GetContracts().Select(contract => contract.ContractId));

        // Private messages and gifts, passed straight through to ClientHermes the same way.
        // Both use the companion server when the main server doesn't have them (see messageHermes and giftHermes). Items
        // held for a gift are tagged with the server they went to (GiftEscrowModule), so they're settled with that server
        private Hermes.ClientHermes hermes;
        public string[] GetConversationPartners() => messageHermes.GetConversationPartners();
        public Hermes.DirectMessage[] GetConversation(string otherUuid) => messageHermes.GetConversation(otherUuid);
        public int UnreadDirectMessages(string otherUuid) => messageHermes.UnreadCount(otherUuid);
        public void MarkDirectMessagesRead(string otherUuid) => messageHermes.MarkRead(otherUuid);
        public void StartConversation(string otherUuid) => messageHermes.StartConversation(otherUuid);
        public void SendDirectMessage(string recipientUuid, string text) => messageHermes.SendDirectMessage(recipientUuid, text);
        public bool SendGift(string token, string recipientUuid, IEnumerable<ProtoThing> items, string note) => giftHermes.SendGift(token, recipientUuid, items, note);

        /// <summary>
        /// Unread private messages across every conversation, leaving out blocked players. Part of the Inbox tab's "New" count.
        /// </summary>
        public bool DirectMessagesEnabled => messageHermes.DirectMessagesEnabled;
        public int NewDirectMessageCount => !DirectMessagesEnabled ? 0 : GetConversationPartners()
            .Where(uuid => !Settings.BlockedUsers.Contains(uuid))
            .Sum(uuid => messageHermes.UnreadCount(uuid));

        // Screenshots shared in chat (see ScreenshotSharing), kept by the main server or the companion like gifts
        private Hermes.ClientScreenshots screenshots;
        private Hermes.ClientScreenshots companionScreenshots;

        /// <summary>
        /// Where screenshots go and come from: the main server if it keeps them, otherwise the companion if it does.
        /// </summary>
        private Hermes.ClientScreenshots screenshotServer => screenshots.Available || !companionScreenshots.Available ? screenshots : companionScreenshots;

        /// <summary>
        /// Whether the main server or the companion keeps screenshots. False on an original Phinix server with no
        /// companion, so the UI hides the button there.
        /// </summary>
        public bool ScreenshotsAvailable => Online && screenshotServer.Available;

        /// <summary>
        /// How long the server keeps each screenshot, in minutes.
        /// </summary>
        public int ScreenshotLifetimeMinutes => screenshotServer.LifetimeMinutes;

        public bool UploadScreenshot(string token, byte[] jpeg) => screenshotServer.Upload(token, jpeg);
        public bool RequestScreenshot(string screenshotId) => screenshotServer.Request(screenshotId);

        // The companion server: an optional second connection for Phime features the main server doesn't have, so
        // players can stay on a normal Phinix server for chat and trades. It logs in as the same player (same UUID) by
        // claiming our main server's UUID (see CompanionClaim), so the main user list works for both
        private NetClient companionNetClient;
        private ClientAuthenticator companionAuthenticator;
        private ClientUserManager companionUserManager;
        private Hermes.ClientHermes companionHermes;
        private ClientAgora companionAgora;

        /// <summary>
        /// Whether we're logged in to a companion server.
        /// </summary>
        public bool CompanionOnline => companionNetClient != null && companionAuthenticator != null && companionUserManager != null && companionNetClient.Connected && companionAuthenticator.Authenticated && companionUserManager.LoggedIn;

        /// <summary>
        /// Where private messages go: the main server if it has them turned on, otherwise the companion if we're on one.
        /// </summary>
        private Hermes.ClientHermes messageHermes => hermes.DirectMessagesEnabled || !CompanionOnline ? hermes : companionHermes;

        /// <summary>
        /// Whether private messages are going through the companion rather than the main server.
        /// </summary>
        public bool DirectMessagesViaCompanion => messageHermes != hermes;

        /// <summary>
        /// Where gifts go: the main server if it has Hermes, otherwise the companion if it does.
        /// </summary>
        /// <remarks>
        /// <see cref="Hermes.ClientHermes.Available"/> only turns true once that server's login sync arrives, and false again
        /// when it disconnects, so this never picks a server that can't answer.
        /// </remarks>
        private Hermes.ClientHermes giftHermes => hermes.Available || !companionHermes.Available ? hermes : companionHermes;

        /// <summary>
        /// Where the market, bounties and contracts go: the main server if it has Agora, otherwise the companion if it does.
        /// </summary>
        private ClientAgora boardAgora => agora.Available || !companionAgora.Available ? agora : companionAgora;

        /// <summary>
        /// The companion as <see cref="CompanionClaim.NormaliseServer"/> writes it, e.g. "example.org:16200".
        /// </summary>
        private string companionServer => CompanionClaim.NormaliseServer(Settings.CompanionAddress, Settings.CompanionPort);

        /// <summary>
        /// The <see cref="ItemEscrow"/> module to hold bounty and contract items under, for wherever they're about to go.
        /// </summary>
        /// <remarks>
        /// Items sent to the companion are tagged with its address ("Agora@example.org:16200"), so after a reconnect
        /// they're only ever checked with that same companion. Asking any other server would get "never had them", and
        /// the items would come back here while the companion still holds them.
        /// </remarks>
        public string BoardEscrowModule => boardAgora == agora ? ItemEscrow.AGORA : companionEscrowModule(ItemEscrow.AGORA);

        /// <summary>
        /// The <see cref="ItemEscrow"/> module to hold gift items under, for wherever they're about to go. See
        /// <see cref="BoardEscrowModule"/>.
        /// </summary>
        public string GiftEscrowModule => giftHermes == hermes ? ItemEscrow.HERMES : companionEscrowModule(ItemEscrow.HERMES);

        /// <summary>
        /// The escrow module name for items sent to the current companion's copy of a module.
        /// </summary>
        private string companionEscrowModule(string module) => module + "@" + companionServer;

        // "New" counts for the Phinix window's tabs. These sets remember which trades and markets the player has
        // already seen, like a Python set(). Only the UI uses them, so they're always on the main thread and need no lock
        private readonly HashSet<string> seenTradeIds = new HashSet<string>();
        private readonly HashSet<string> seenShopIds = new HashSet<string>();
        private readonly HashSet<string> seenBountyIds = new HashSet<string>();
        private readonly HashSet<string> seenContractIds = new HashSet<string>();

        /// <summary>
        /// Contract proposals we've already popped up a message for, so each one is announced once.
        /// Only touched on the main thread.
        /// </summary>
        private readonly HashSet<string> announcedProposalIds = new HashSet<string>();

        /// <summary>
        /// Deliveries we've already reminded the player about, as "contractId:deliveryNumber". Only touched on the
        /// main thread.
        /// </summary>
        private readonly HashSet<string> remindedDeliveries = new HashSet<string>();

        /// <summary>
        /// When we last checked for contract deliveries that need making, in seconds since the game started.
        /// </summary>
        private float lastDeliveryReminderCheck = -999f;
        private float lastColonyShare = -999f;

        /// <summary>
        /// Number of chat messages that arrived since the chat tab was last open.
        /// </summary>
        public int NewChatCount => Settings.ShowBlockedUnreadMessageCount ? UnreadMessages : UnreadMessagesExcludingBlocked;

        /// <summary>
        /// Number of trades that haven't been looked at yet.
        /// </summary>
        public int NewTradeCount => GetVisibleTrades().Count(trade => !seenTradeIds.Contains(trade.TradeId));

        /// <summary>
        /// Number of other people's markets opened since the market tab was last open.
        /// </summary>
        public int NewShopCount => GetVisibleShops().Count(shop => shop.OwnerUuid != Uuid && !seenShopIds.Contains(shop.ShopId));

        /// <summary>
        /// Marks every current trade as seen, clearing the Inbox (or Trades) tab's "New" count.
        /// </summary>
        public void MarkTradesSeen() => seenTradeIds.UnionWith(GetVisibleTrades().Select(trade => trade.TradeId));

        /// <summary>
        /// Marks one trade as seen, e.g. when its window is opened.
        /// </summary>
        /// <param name="tradeId">ID of the trade</param>
        public void MarkTradeSeen(string tradeId) => seenTradeIds.Add(tradeId);

        /// <summary>
        /// Marks every market as seen, clearing the Market tab's "New" count.
        /// </summary>
        public void MarkShopsSeen() => seenShopIds.UnionWith(GetShops().Select(shop => shop.ShopId));

        /// <summary>
        /// Number of other people's bounties posted since the bounties tab was last open.
        /// </summary>
        public int NewBountyCount => GetVisibleBounties().Count(bounty => bounty.PosterUuid != Uuid && !seenBountyIds.Contains(bounty.BountyId));

        /// <summary>
        /// Marks every bounty as seen, clearing the Bounties tab's "New" count.
        /// </summary>
        public void MarkBountiesSeen() => seenBountyIds.UnionWith(GetBounties().Select(bounty => bounty.BountyId));

        /// <summary>
        /// Gets active trades, leaving out blocked users' trades if the player has chosen to hide them.
        /// </summary>
        /// <returns>Trades the player can see</returns>
        public ImmutableTrade[] GetVisibleTrades() => Settings.ShowBlockedTrades ? GetTrades() : GetTradesExceptWith(Settings.BlockedUsers);

        private ClientTrading trading;

        /// <summary>
        /// Trades through the companion server. Used instead of the main server's trades when the main server runs the
        /// original Phinix, which can't carry animals or colonists (see <see cref="tradingForNewTradeWith"/>).
        /// </summary>
        private ClientTrading companionTrading;

        // Trades can live on either server, so everything that names a trade asks the server that has it, and the
        // lists put both servers' trades together. They look the same to the player
        public void CancelTrade(string tradeId) => tradingOf(tradeId).CancelTrade(tradeId);
        public string[] GetTradeIds() => trading.GetTradeIds().Concat(companionTrading.GetTradeIds()).ToArray();
        public string[] GetTradeIdsExceptWith(IEnumerable<string> otherPartyUuids) => trading.GetTradeIdsExceptWith(otherPartyUuids).Concat(companionTrading.GetTradeIdsExceptWith(otherPartyUuids)).ToArray();
        public ImmutableTrade[] GetTrades() => trading.GetTrades().Concat(companionTrading.GetTrades()).ToArray();
        public ImmutableTrade[] GetTradesExceptWith(IEnumerable<string> otherPartyUuids) => trading.GetTradesExceptWith(otherPartyUuids).Concat(companionTrading.GetTradesExceptWith(otherPartyUuids)).ToArray();
        public bool TryGetOtherPartyUuid(string tradeId, out string otherPartyUuid) => tradingOf(tradeId).TryGetOtherPartyUuid(tradeId, out otherPartyUuid);
        public bool TryGetOtherPartyAccepted(string tradeId, out bool otherPartyAccepted) => tradingOf(tradeId).TryGetOtherPartyAccepted(tradeId, out otherPartyAccepted);
        public bool TryGetPartyAccepted(string tradeId, string partyUuid, out bool accepted) => tradingOf(tradeId).TryGetPartyAccepted(tradeId, partyUuid, out accepted);
        public bool TryGetItemsOnOffer(string tradeId, string uuid, out IEnumerable<Trading.ProtoThing> items) => tradingOf(tradeId).TryGetItemsOnOffer(tradeId, uuid, out items);
        public void UpdateTradeItems(string tradeId, IEnumerable<ProtoThing> items, string token = "") => tradingOf(tradeId).UpdateItems(tradeId, items, token);
        public void UpdateTradeStatus(string tradeId, bool? accepted = null, bool? cancelled = null) => tradingOf(tradeId).UpdateStatus(tradeId, accepted, cancelled);

        /// <summary>
        /// The server a trade lives on: the companion if it has it, otherwise the main server.
        /// </summary>
        private ClientTrading tradingOf(string tradeId) => companionTrading.TryGetOtherPartyUuid(tradeId, out _) ? companionTrading : trading;

        /// <summary>
        /// The <see cref="ItemEscrow"/> module to hold a trade's items under. Items for a companion trade are tagged with
        /// the companion's address, the same as <see cref="BoardEscrowModule"/>, so they're only settled with it.
        /// </summary>
        /// <param name="tradeId">Trade the items are going into, or coming back from</param>
        public string TradeEscrowModule(string tradeId) => tradingOf(tradeId) == trading ? ItemEscrow.TRADING : companionEscrowModule(ItemEscrow.TRADING);

        /// <summary>
        /// Whether the main server is a Phime server, which carries animals and colonists in trades. The original
        /// Phinix doesn't, and it doesn't have the market or private messages either, which is how we tell them apart.
        /// </summary>
        private bool mainCarriesPawns => agora.Available || hermes.Available;

        /// <summary>
        /// Whether a trade goes through a server running the original Phinix, which drops everything Phime adds to an
        /// item (its full copy, art, mod list) and to a pawn. Things then arrive rebuilt from their basic details.
        /// </summary>
        /// <param name="tradeId">Trade to check</param>
        public bool TradeStripsDetails(string tradeId) => tradingOf(tradeId) == trading && !mainCarriesPawns;

        /// <summary>
        /// Where to open a new trade with someone: the companion when the main server can't carry animals or colonists
        /// and the other player is on the companion too, otherwise the main server.
        /// </summary>
        /// <remarks>
        /// A companion only knows players who have linked to it, so someone who hasn't (or is offline there) still
        /// gets a normal trade on the main server, just without pawns.
        /// </remarks>
        /// <param name="otherPartyUuid">The other player</param>
        private ClientTrading tradingForNewTradeWith(string otherPartyUuid)
        {
            if (mainCarriesPawns || !CompanionOnline) return trading;
            return companionUserManager.GetUuids(true).Contains(otherPartyUuid) ? companionTrading : trading;
        }
        public LookTargets DropPods(IEnumerable<Thing> verseThings) => dropPods(verseThings);
        public event EventHandler<UICreateTradeEventArgs> OnTradeCreationSuccess;
        public event EventHandler<UICreateTradeEventArgs> OnTradeCreationFailure;
        public event EventHandler<UICompleteTradeEventArgs> OnTradeCompleted;
        public event EventHandler<UICompleteTradeEventArgs> OnTradeCancelled;
        public event EventHandler<UITradeUpdateEventArgs> OnTradeUpdateSuccess;
        public event EventHandler<UITradeUpdateEventArgs> OnTradeUpdateFailure;
        public event EventHandler<UITradesSyncedEventArgs> OnTradesSynced;
        #endregion

        public event EventHandler<BlockedUsersChangedEventArgs> OnBlockedUsersChanged;
        public event EventHandler OnFriendsChanged;
        public event EventHandler OnChatMessageLimitChanged;

        public Settings Settings { get; }

        /// <summary>
        /// Queue of sounds to play on the next frame.
        /// Necessary because sounds are only played on the main Unity thread.
        /// </summary>
        private List<SoundDef> soundQueue = new List<SoundDef>();
        /// <summary>
        /// Lock object to prevent race conditions when accessing soundQueue.
        /// </summary>
        private object soundQueueLock = new object();

        /// <summary>
        /// Collection of UUIDs that we have created trades with and are waiting for a confirmation from the server for.
        /// Used to display the trade immediately once it's confirmed.
        /// </summary>
        private HashSet<string> waitingForTradeCreationWith = new HashSet<string>();
        /// <summary>
        /// Lock object protecting <see cref="waitingForTradeCreationWith"/>
        /// </summary>
        private object waitingForTradeCreationWithLock = new object();

        /// <summary>
        /// Collection of trades queued to be opened on the next frame.
        /// Necessary because textures and other assets can only be gotten on the main Unity thread.
        /// </summary>
        private List<ImmutableTrade> tradeWindowQueue = new List<ImmutableTrade>();
        /// <summary>
        /// Lock object protecting <see cref="tradeWindowQueue"/>.
        /// </summary>
        private object tradeWindowQueueLock = new object();

        /// <summary>
        /// Jobs queued to run on the next frame.
        /// Network packets arrive on a background thread, but most of RimWorld (making pawns, spawning things,
        /// sending letters) may only be touched from the main game thread. Handlers put that work in here
        /// with <see cref="RunOnMainThread"/>, and <see cref="Update"/> runs it from the main thread.
        /// </summary>
        private Queue<Action> mainThreadQueue = new Queue<Action>();
        /// <summary>
        /// Lock object protecting <see cref="mainThreadQueue"/>.
        /// </summary>
        private object mainThreadQueueLock = new object();

        public Client(ModContentPack content) : base(content)
        {
            Instance = this;

            // Apply Harmony patches
            new HarmonyLib.Harmony(PackageId).PatchAll();

            // Load in Settings
            Settings = GetSettings<Settings>();
            if (!Settings.Migrated) Settings.MigrateFromHugsLib();

            // Set up our module instances
            netClient = new NetClient();
            authenticator = new ClientAuthenticator(netClient, getCredentials);
            userManager = new ClientUserManager(netClient, authenticator);
            chat = new ClientChat(netClient, authenticator, userManager);
            trading = new ClientTrading(netClient, authenticator, userManager);
            agora = new ClientAgora(netClient, authenticator, userManager);
            hermes = new Hermes.ClientHermes(netClient, authenticator, userManager);
            screenshots = new Hermes.ClientScreenshots(netClient, authenticator, userManager);

            // The companion gets its own key file, so the main server (which sees our main key) can't pose as us there.
            // Its username is our key plus the main server and our UUID on it, which is read when the companion asks
            companionNetClient = new NetClient();
            companionAuthenticator = new ClientAuthenticator(companionNetClient, getCredentials, "PhimeCompanionCredentials.bin",
                key => CompanionClaim.Build(key, CompanionClaim.NormaliseServer(Settings.ServerAddress, Settings.ServerPort), Uuid));
            companionUserManager = new ClientUserManager(companionNetClient, companionAuthenticator);
            userManager.OnModListReceived += modListReceived;
            companionUserManager.OnModListReceived += modListReceived;
            companionHermes = new Hermes.ClientHermes(companionNetClient, companionAuthenticator, companionUserManager);
            companionAgora = new ClientAgora(companionNetClient, companionAuthenticator, companionUserManager);
            companionTrading = new ClientTrading(companionNetClient, companionAuthenticator, companionUserManager);
            companionScreenshots = new Hermes.ClientScreenshots(companionNetClient, companionAuthenticator, companionUserManager);

            // Subscribe to log events
            authenticator.OnLogEntry += ILoggableHandler;
            userManager.OnLogEntry += ILoggableHandler;
            chat.OnLogEntry += ILoggableHandler;
            trading.OnLogEntry += ILoggableHandler;
            agora.OnLogEntry += ILoggableHandler;
            hermes.OnLogEntry += ILoggableHandler;
            screenshots.OnLogEntry += ILoggableHandler;
            EventHandler<LogEventArgs> companionLog = (sender, args) => ILoggableHandler(sender, new LogEventArgs("[Companion] " + args.Message, args.LogLevel));
            companionAuthenticator.OnLogEntry += companionLog;
            companionUserManager.OnLogEntry += companionLog;
            companionHermes.OnLogEntry += companionLog;
            companionAgora.OnLogEntry += companionLog;
            companionTrading.OnLogEntry += companionLog;
            companionScreenshots.OnLogEntry += companionLog;

            #region Module Event Handlers
            // Subscribe to connection events
            netClient.OnDisconnect += (sender, args) =>
            {
                // Clear the waiting list for opening trades
                lock (waitingForTradeCreationWithLock) waitingForTradeCreationWith.Clear();

                // Mod lists are per server, so forget them
                lock (modListsLock)
                {
                    modLists.Clear();
                    modListAsked.Clear();
                }

                // The companion only makes sense alongside the main server (it logs in as our main server's player)
                companionNetClient.Disconnect();
            };

            // Companion: log in once authenticated, and say plainly if it refuses us. Unlike the main server these use
            // messages rather than dialogs, since the main server still works without it
            companionAuthenticator.OnAuthenticationSuccess += (sender, args) =>
            {
                companionUserManager.SendLogin(displayName: Settings.DisplayName, acceptingTrades: Settings.AcceptingTrades, mods: ownMods(), bio: Settings.Bio);
            };
            companionAuthenticator.OnAuthenticationFailure += (sender, args) =>
            {
                RunOnMainThread(() => Messages.Message("Phinix_companion_failed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false));
                companionNetClient.Disconnect();
            };
            companionUserManager.OnLoginSuccess += (sender, args) =>
            {
                // A server without companion mode turned on gives us a brand new UUID instead of the one we claimed.
                // Messages sent through it would go to the wrong players, so don't use it
                if (companionUserManager.Uuid != userManager.Uuid)
                {
                    Verse.Log.Warning(string.Format("Companion server {0} gave us UUID {1} instead of {2}; it isn't in companion mode", Settings.CompanionAddress, companionUserManager.Uuid, userManager.Uuid));
                    RunOnMainThread(() => Messages.Message("Phinix_companion_failed".Translate("Phinix_companion_notCompanion".Translate().Resolve()), MessageTypeDefOf.RejectInput, historical: false));
                    companionNetClient.Disconnect();
                    return;
                }

                Verse.Log.Message(string.Format("Linked to companion server {0} as UUID {1}", Settings.CompanionAddress, companionUserManager.Uuid));
                RunOnMainThread(() => Messages.Message("Phinix_companion_linked".Translate(), MessageTypeDefOf.PositiveEvent, historical: false));
                announceConfirmations(companionAgora, companionHermes, companionTrading);

                // Settle anything still held for this companion, the same way the main server's login does. Only tokens
                // tagged with this companion's address are asked about (see BoardEscrowModule)
                RunOnMainThread(() =>
                {
                    if (Current.Game == null) return;
                    companionAgora.CheckTokens(ItemEscrow.HeldTokens(companionEscrowModule(ItemEscrow.AGORA)));
                    companionHermes.CheckTokens(ItemEscrow.HeldTokens(companionEscrowModule(ItemEscrow.HERMES)));
                    companionTrading.CheckTokens(ItemEscrow.HeldTokens(companionEscrowModule(ItemEscrow.TRADING)));
                });
            };
            companionUserManager.OnLoginFailure += (sender, args) =>
            {
                RunOnMainThread(() => Messages.Message("Phinix_companion_failed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false));
                companionNetClient.Disconnect();
            };

            // Subscribe to authentication events
            authenticator.OnAuthenticationSuccess += (sender, args) =>
            {
                Verse.Log.Message("Successfully authenticated with server.");
                userManager.SendLogin(
                    displayName: Settings.DisplayName,
                    acceptingTrades: Settings.AcceptingTrades,
                    mods: ownMods(),
                    bio: Settings.Bio
                );
            };
            authenticator.OnAuthenticationFailure += (sender, args) =>
            {
                Verse.Log.Message(string.Format("Failed to authenticate with server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString()));

                // Windows can only be opened on the main thread, and this fires on the network thread
                RunOnMainThread(() => Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_authFailedTitle".Translate(), text: "Phinix_error_authFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString()))));

                Disconnect();
            };

            // Subscribe to user management events
            userManager.OnLoginSuccess += (sender, args) =>
            {
                // Settle anything still held from before (the connection dropped mid-hand-over, or the game was
                // closed). ItemEscrow reads the save game, so this has to happen on the main thread
                RunOnMainThread(() =>
                {
                    if (Current.Game == null) return;
                    agora.CheckTokens(ItemEscrow.HeldTokens(ItemEscrow.AGORA));
                    hermes.CheckTokens(ItemEscrow.HeldTokens(ItemEscrow.HERMES));
                    trading.CheckTokens(ItemEscrow.HeldTokens(ItemEscrow.TRADING));
                });

                // Tell each module we confirm deliveries, so the server keeps ours until we do (see DeliveryLog)
                announceConfirmations(agora, hermes, trading);

                // Now that we know who we are on the main server, we can claim that player on the companion
                RunOnMainThread(connectCompanion);

                Verse.Log.Message(string.Format("Successfully logged in with UUID {0}", userManager.Uuid));
            };
            userManager.OnLoginFailure += (sender, args) =>
            {
                Verse.Log.Message(string.Format("Failed to log in to server: {0} ({1})", args.FailureMessage, args.FailureReason.ToString()));

                // Windows can only be opened on the main thread, and this fires on the network thread
                RunOnMainThread(() => Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_loginFailedTitle".Translate(), text: "Phinix_error_loginFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString()))));

                Disconnect();
            };
            userManager.OnUserDisplayNameChanged += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User with UUID {0} changed their display name from \"{1}\" to \"{2}\"", args.Uuid, args.OldDisplayName, args.NewDisplayName));
            };
            userManager.OnUserLoggedIn += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User {0} logged in", args.Uuid));
            };
            userManager.OnUserLoggedOut += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("User {0} logged out", args.Uuid));
            };
            userManager.OnUserCreated += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("New user created: {0} ({1}) - {2}ogged in", args.DisplayName, args.Uuid, args.LoggedIn ? "L" : "Not l"));
            };

            // The market, bounties and contracts, from the main server or the companion: they look the same to the player
            foreach (ClientAgora board in new[] { agora, companionAgora })
            {
                // Bounty answers from the server. Each carries the token of the items ItemEscrow is holding, and Resolve
                // either destroys them (the server kept them) or puts them back on the map (the server refused).
                // Both touch the map, so they run on the main thread
                board.OnPostBountyResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    if (!args.Success) Messages.Message("Phinix_bounty_postFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });
                board.OnFulfilBountyResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    if (!args.Success) Messages.Message("Phinix_bounty_fulfilFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });

                // Contract answers. Same token idea as bounties: Resolve destroys the held items if the server took them,
                // or puts them back if it didn't
                board.OnProposeContractResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    if (args.Success) Messages.Message("Phinix_contract_proposed".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
                    else Messages.Message("Phinix_contract_proposeFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });
                board.OnDeliverContractResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    if (!args.Success) Messages.Message("Phinix_contract_deliverFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });

                // The answers to the login token checks: destroy what the server has, put back the rest
                board.OnTokenResults += (sender, args) => RunOnMainThread(() => ItemEscrow.Reconcile(args.Checked, args.Accepted));

                // Pop up a message the first time we see each contract someone proposed to us
                board.OnContractsChanged += (sender, args) => RunOnMainThread(() =>
                {
                    enforceBlocks();
                    announceNewProposals();
                });

                // Items arriving from a bounty: what we asked for, a reward, or a reward coming back.
                // Contracts send their items the same way, with their own reasons
                board.OnBountyDelivery += (sender, args) => RunOnMainThread(() => receiveBountyDelivery(board, args));

                // Market answers. Stocking and buying carry the token of the items (or silver) ItemEscrow is holding,
                // the same as bounties; the rest carry none, and Resolve ignores an empty token
                board.OnShopResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    if (!args.Success) Messages.Message("Phinix_shop_failed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });
            }

            hermes.OnTokenResults += (sender, args) => RunOnMainThread(() => ItemEscrow.Reconcile(args.Checked, args.Accepted));
            companionHermes.OnTokenResults += (sender, args) => RunOnMainThread(() => ItemEscrow.Reconcile(args.Checked, args.Accepted));
            trading.OnTokenResults += (sender, args) => RunOnMainThread(() => ItemEscrow.Reconcile(args.Checked, args.Accepted));
            companionTrading.OnTokenResults += (sender, args) => RunOnMainThread(() => ItemEscrow.Reconcile(args.Checked, args.Accepted));

            // Private messages, from the main server or the companion: they look the same to the player
            foreach (Hermes.ClientHermes messenger in new[] { hermes, companionHermes })
            {
                // A new one from someone else pops up a message in the top left (unless they're blocked), with the same
                // little tick chat uses
                messenger.OnDirectMessageReceived += (sender, message) =>
                {
                    if (Settings.BlockedUsers.Contains(message.SenderUuid)) return;

                    RunOnMainThread(() =>
                    {
                        Messages.Message("Phinix_dm_received".Translate(displayNameOf(message.SenderUuid), shorten(ScreenshotSharing.ShowLinks(TradeRequests.Readable(message.Text), colour: false))), MessageTypeDefOf.NeutralEvent, historical: false);
                        if (Settings.PlayNoiseOnMessageReceived) SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    });
                };

                // Messages that arrived while we were offline. One letter sums them up rather than a pile of pop-ups
                messenger.OnMissedDirectMessages += (sender, missed) =>
                {
                    string[] senders = missed
                        .Where(message => !Settings.BlockedUsers.Contains(message.SenderUuid))
                        .Select(message => message.SenderUuid)
                        .Distinct()
                        .Select(displayNameOf)
                        .ToArray();
                    if (senders.Length == 0) return;

                    int count = missed.Count(message => !Settings.BlockedUsers.Contains(message.SenderUuid));
                    RunOnMainThread(() => Find.LetterStack.ReceiveLetter(
                        "Phinix_dm_missed_label".Translate(),
                        "Phinix_dm_missed_description".Translate(count, string.Join(", ", senders)),
                        LetterDefOf.NeutralEvent
                    ));
                };

                messenger.OnSendDirectMessageFailed += (sender, args) => RunOnMainThread(() =>
                    Messages.Message("Phinix_dm_sendFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false));
            }

            // Gifts, from the main server or the companion
            foreach (Hermes.ClientHermes giver in new[] { hermes, companionHermes })
            {
                // Gift answers carry the token of the items ItemEscrow is holding, exactly like bounties
                giver.OnSendGiftResponse += (sender, args) => RunOnMainThread(() =>
                {
                    ItemEscrow.Resolve(args.Token, args.Success);
                    // Say plainly when the gift is waiting rather than delivered, so nobody thinks it arrived
                    if (args.Success && args.Waiting) Messages.Message("Phinix_gift_sentWaiting".Translate(displayNameOf(args.RecipientUuid)), MessageTypeDefOf.CautionInput, historical: false);
                    else if (args.Success) Messages.Message("Phinix_gift_sent".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
                    else Messages.Message("Phinix_gift_sendFailed".Translate(args.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                });

                // A gift arriving for us. Blocked players' gifts still arrive: refusing them would just destroy the items
                giver.OnGiftReceived += (sender, gift) => RunOnMainThread(() => receiveGift(giver, gift));
            }

            // Screenshots, from the main server or the companion. Pictures are only stored here; the viewer window picks
            // them up when it next draws
            foreach (Hermes.ClientScreenshots keeper in new[] { screenshots, companionScreenshots })
            {
                keeper.OnUploadResponse += (sender, args) => RunOnMainThread(() => ScreenshotSharing.UploadAnswered(args));
                keeper.OnScreenshotReceived += (sender, args) => ScreenshotSharing.Received(args);
            }

            // Subscribe to chat events
            chat.OnChatMessageReceived += (sender, args) =>
            {
                if (Prefs.DevMode) Verse.Log.Message("Received chat message from UUID " + args.Message.SenderUuid);

                // Check if the message wasn't ours, chat noises are enabled, and if we are in-game before playing a sound
                if (args.Message.SenderUuid != Uuid && Settings.PlayNoiseOnMessageReceived && Current.Game != null && !Settings.BlockedUsers.Contains(args.Message.SenderUuid))
                {
                    lock (soundQueueLock)
                    {
                        // Add a little tick noise to the sound queue
                        // (queue is necessary because sounds only play on the main Unity thread)
                        soundQueue.Add(SoundDefOf.Tick_Tiny);
                    }
                }
            };

            // Subscribe to trading events
            // Trades from the main server or the companion: they look the same to the player
            foreach (ClientTrading trader in new[] { trading, companionTrading })
            {
                trader.OnTradeCreationSuccess += (sender, args) =>
                {
                    if (Prefs.DevMode) Verse.Log.Message(string.Format("Created trade {0} with {1}", args.TradeId, args.OtherPartyUuid));

                    // A trade with someone we've blocked gets cancelled straight away, unless we chose to see their trades
                    if (!Settings.ShowBlockedTrades && IsBlocked(args.OtherPartyUuid))
                    {
                        RunOnMainThread(enforceBlocks);
                        return;
                    }

                    // Check if we are waiting for this trade to be created. If so, show the trade window immediately.
                    lock (waitingForTradeCreationWithLock)
                    {
                        // Check for and remove the other party's UUID in one go
                        if (waitingForTradeCreationWith.Remove(args.OtherPartyUuid))
                        {
                            if (trader.TryGetTrade(args.TradeId, out ImmutableTrade trade))
                            {
                                // Show the trade window and skip any further processing. No need to generate a letter if
                                // we already have the window up.
                                lock (tradeWindowQueueLock) tradeWindowQueue.Add(trade);
                                return;
                            }
                            else
                            {
                                // Log the failure and revert to the letter instead
                                Verse.Log.Warning(string.Format("Failed to get newly created trade {0} when attempting to open immediately", args.TradeId));
                            }
                        }
                    }

                    // Try get the other party's display name
                    if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                    {
                        // Strip formatting
                        displayName = TextHelper.StripRichText(displayName);
                    }
                    else
                    {
                        // Unknown display name, default to ???
                        displayName = "???";
                    }

                    // Also pop up a message in the top left, like RimWorld's own "a trader has arrived" messages.
                    // Messages are drawn by the game, so they have to be added on the main thread
                    string messageName = displayName;
                    RunOnMainThread(() => Messages.Message("Phinix_trade_tradeReceivedMessage".Translate(messageName), MessageTypeDefOf.PositiveEvent, historical: false));

                    // Generate a letter, on the main thread too: mods that patch ReceiveLetter can crash the game when
                    // it's called from the network thread
                    RunOnMainThread(() =>
                    {
                        TradeOfferLetter.Offer(
                            args.TradeId,
                            "Phinix_trade_tradeReceivedLetter_label".Translate(messageName),
                            "Phinix_trade_tradeReceivedLetter_description".Translate(messageName)
                        );
                    });
                };
                // Things we traded away coming back, because the other player's game couldn't use them
                trader.OnTradeItemsReturned += (sender, args) => RunOnMainThread(() =>
                {
                    if (alreadyReceived(trader, "trade:" + args.TradeId, args.TradeId)) return;

                    Verse.Thing[] verseItems = buildReceivedItems(args.Items, out string lostNote, out List<int> unusable);
                    LookTargets dropSpot = dropPods(verseItems);
                    confirmReceived(trader, "trade:" + args.TradeId, args.TradeId, unusable);

                    Find.LetterStack.ReceiveLetter(
                        "Phinix_trade_returned_label".Translate(),
                        "Phinix_trade_returned_description".Translate(displayNameOf(args.OtherPartyUuid)) + lostNote,
                        DefDatabase<LetterDef>.GetNamed("TradeCancelled"),
                        dropSpot);
                });
                trader.OnTradeCreationFailure += (sender, args) =>
                {
                    if (Prefs.DevMode) Verse.Log.Message(string.Format("Failed to create trade with {0}: {1} ({2})", args.OtherPartyUuid, args.FailureMessage, args.FailureReason.ToString()));

                    // This event fires on the network thread, and windows can only be opened on the main thread
                    RunOnMainThread(() => Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_tradeCreationFailedTitle".Translate(), text: "Phinix_error_tradeCreationFailedMessage".Translate(args.FailureMessage, args.FailureReason.ToString()))));

                    // Remove the other party from the waiting list
                    lock (waitingForTradeCreationWithLock) waitingForTradeCreationWith.Remove(args.OtherPartyUuid);
                };
                trader.OnTradeCompleted += (sender, args) =>
                {
                    // Building the received things (especially animals) and dropping them in has to happen on the
                    // main game thread, but this event fires on the network thread, so hand the work over
                    RunOnMainThread(() =>
                    {
                        // The server sends a result again if our confirmation never reached it; it only gets confirmed again
                        if (alreadyReceived(trader, "trade:" + args.TradeId, args.TradeId)) return;

                        // Try get the other party's display name
                        if (Instance.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                        {
                            // Strip formatting
                            displayName = TextHelper.StripRichText(displayName);
                        }
                        else
                        {
                            // Unknown display name, default to ???
                            displayName = "???";
                        }

                        // Convert all the received items into their Verse counterparts and strip out any unknown ones
                        //// While it would be less computationally-expensive to strip out unknown items beforehand, we would
                        //// have no idea whether we could actually make the item without another check, so we just piggy-back
                        //// off of the converter's checks and strip them out afterward.
                        Verse.Thing[] verseItems = buildReceivedItems(args.Items, out string lostNote, out List<int> unusable);


                        // Launch drop pods to a trade spot on a home tile
                        LookTargets dropSpotLookTarget = dropPods(verseItems);

                        // They're in the colony now (or kept safe until there's one), so the server can let go of them
                        confirmReceived(trader, "trade:" + args.TradeId, args.TradeId, unusable);

                        // Say what was left behind on any animal or colonist with parts this game doesn't have
                        announceMissingParts(args.Items);

                        // Generate a letter
                        LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeAccepted");
                        Find.LetterStack.ReceiveLetter("Phinix_trade_tradeCompletedLetter_label".Translate(), "Phinix_trade_tradeCompletedLetter_description".Translate(displayName) + lostNote, letterDef, dropSpotLookTarget);

                        if (Prefs.DevMode) Verse.Log.Message(string.Format("Trade with {0} completed successfully", args.OtherPartyUuid));
                    });
                };
                trader.OnTradeCancelled += (sender, args) =>
                {
                    // Don't display anything if the other party is blocked and we want to hide their trades. But if
                    // anything of ours was on offer, it's coming back, so it still has to be dropped in
                    if (!Settings.ShowBlockedTrades && IsBlocked(args.OtherPartyUuid) && (args.Items == null || !args.Items.Any()))
                    {
                        // Nothing to drop in, but the server still waits to hear we got it
                        trader.AckDeliveries(new[] { new Trading.DeliveryAck { DeliveryId = args.TradeId } });
                        return;
                    }

                    // Building the received things (especially animals) and dropping them in has to happen on the
                    // main game thread, but this event fires on the network thread, so hand the work over
                    RunOnMainThread(() =>
                    {
                        // The server sends a result again if our confirmation never reached it; it only gets confirmed again
                        if (alreadyReceived(trader, "trade:" + args.TradeId, args.TradeId)) return;

                        // Try get the other party's display name
                        if (userManager.TryGetDisplayName(args.OtherPartyUuid, out string displayName))
                        {
                            // Strip formatting
                            displayName = TextHelper.StripRichText(displayName);
                        }
                        else
                        {
                            // Unknown display name, default to ???
                            displayName = "???";
                        }

                        // Convert all the received items into their Verse counterparts and strip out any unknown ones
                        //// While it would be less computationally-expensive to strip out unknown items beforehand, we would
                        //// have no idea whether we could actually make the item without another check, so we just piggy-back
                        //// off of the converter's checks and strip them out afterward.
                        Verse.Thing[] verseItems = buildReceivedItems(args.Items, out string lostNote, out List<int> unusable);

                        // Our things go back where they were taken from, as far as we remember; anything else is
                        // dropped in at a trade spot on a home tile
                        LookTargets dropSpotLookTarget = dropPods(ItemEscrow.ReturnToOrigins(verseItems));
                        confirmReceived(trader, "trade:" + args.TradeId, args.TradeId, unusable);

                        // Generate a letter
                        LetterDef letterDef = DefDatabase<LetterDef>.GetNamed("TradeCancelled");
                        Find.LetterStack.ReceiveLetter("Phinix_trade_tradeCancelled_label".Translate(), "Phinix_trade_tradeCancelled_description".Translate(displayName) + lostNote, letterDef, dropSpotLookTarget);

                        if (Prefs.DevMode) Verse.Log.Message(string.Format("Trade with {0} cancelled", args.OtherPartyUuid));
                    });
                };
                trader.OnTradeUpdateFailure += (sender, args) =>
                {
                    // Try get the other party's display name
                    if (trader.TryGetOtherPartyUuid(args.TradeId, out string otherPartyUuid) &&
                        userManager.TryGetDisplayName(otherPartyUuid, out string displayName))
                    {
                        // Strip formatting
                        displayName = TextHelper.StripRichText(displayName);
                    }
                    else
                    {
                        // Unknown display name, default to ???
                        displayName = "???";
                    }

                    // Windows can only be opened on the main thread, and this fires on the network thread
                    string failedName = displayName;
                    RunOnMainThread(() => Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_tradeUpdateFailedTitle".Translate(), text: "Phinix_error_tradeUpdateFailedMessage".Translate(failedName, args.FailureMessage, args.FailureReason.ToString()))));
                };
                trader.OnTradesSynced += (sender, args) =>
                {
                    if (Prefs.DevMode) Verse.Log.Message(string.Format("Synced {0} trade{1} from server", args.TradeIds.Length, args.TradeIds.Length != 1 ? "s" : ""));

                    // Cancel any trades with players we blocked while we were away
                    RunOnMainThread(enforceBlocks);
                };
            }
            #endregion

            // Forward events so the UI can handle them
            netClient.OnConnecting += (sender, e) => { OnConnecting?.Invoke(sender, e); };
            netClient.OnDisconnect += (sender, e) => { OnDisconnect?.Invoke(sender, e); };
            authenticator.OnAuthenticationSuccess += (sender, e) => { OnAuthenticationSuccess?.Invoke(sender, e); };
            authenticator.OnAuthenticationFailure += (sender, e) => { OnAuthenticationFailure?.Invoke(sender, e); };
            userManager.OnLoginSuccess += (sender, e) => { OnLoginSuccess?.Invoke(sender, e); };
            userManager.OnLoginFailure += (sender, e) => { OnLoginFailure?.Invoke(sender, e); };
            userManager.OnUserDisplayNameChanged += (sender, e) => { OnUserDisplayNameChanged?.Invoke(sender, e); };
            userManager.OnUserLoggedIn += (sender, e) => { OnUserLoggedIn?.Invoke(sender, e); };
            userManager.OnUserLoggedOut += (sender, e) => { OnUserLoggedOut?.Invoke(sender, e); };
            userManager.OnUserCreated += (sender, e) => { OnUserCreated?.Invoke(sender, e); };
            userManager.OnUserSync += (sender, e) => { OnUserSync?.Invoke(sender, e); };
            chat.OnChatMessageReceived += (sender, e) => { OnChatMessageReceived?.Invoke(sender, new UIChatMessageEventArgs(new UIChatMessage(userManager, e.Message))); };
            chat.OnChatSync += (sender, e) => { OnChatSync?.Invoke(sender, e); };
            foreach (ClientAgora board in new[] { agora, companionAgora })
            {
                board.OnShopsChanged += (sender, e) => { OnShopsChanged?.Invoke(sender, e); };
            }
            // Names come from the main server's user list for both, since a companion player has the same UUID there
            foreach (ClientTrading trader in new[] { trading, companionTrading })
            {
                trader.OnTradeCreationSuccess += (sender, e) => { OnTradeCreationSuccess?.Invoke(sender, UICreateTradeEventArgs.FromCreateTradeEventArgs(e, userManager)); };
                trader.OnTradeCreationFailure += (sender, e) => { OnTradeCreationFailure?.Invoke(sender, UICreateTradeEventArgs.FromCreateTradeEventArgs(e, userManager)); };
                trader.OnTradeCompleted += (sender, e) => { OnTradeCompleted?.Invoke(sender, UICompleteTradeEventArgs.FromCompleteTradeEventArgs(e, userManager)); };
                trader.OnTradeCancelled += (sender, e) => { OnTradeCancelled?.Invoke(sender, UICompleteTradeEventArgs.FromCompleteTradeEventArgs(e, userManager)); };
                trader.OnTradeUpdateSuccess += (sender, e) =>
                {
                    UITradeUpdateEventArgs args = UITradeUpdateEventArgs.FromTradeUpdateEventArgs(e, trader);
                    RunOnMainThread(() => settleTradeOffer(args, true));
                    OnTradeUpdateSuccess?.Invoke(sender, args);
                };
                trader.OnTradeUpdateFailure += (sender, e) =>
                {
                    UITradeUpdateEventArgs args = UITradeUpdateEventArgs.FromTradeUpdateEventArgs(e, trader);
                    RunOnMainThread(() => settleTradeOffer(args, false));
                    OnTradeUpdateFailure?.Invoke(sender, args);
                };
                trader.OnTradesSynced += (sender, e) => { OnTradesSynced?.Invoke(sender, UITradesSyncedEventArgs.FromTradesSyncedEventArgs(e, trader, userManager)); };
            }

            // Remember trade happenings for the Inbox's conversations
            TradeEventLog.Subscribe(this);
            TradeHistory.Subscribe(this);

            // We don't connect here, since the game starts on the main menu. Update() connects once a colony is loaded
        }

        /// <summary>
        /// Settles the items held for a trade offer change once the server has answered it. Must run on the main thread.
        /// </summary>
        /// <remarks>
        /// This lives here rather than in the trade window so it still happens if the window was closed in the meantime.
        /// <para>
        /// One special case: a server running the original Phinix accepts the offer but drops the Animal and Colonist
        /// details it doesn't know about. Letting the pawn go would leave only an empty shell on offer, which crashes
        /// the trade window and comes back as nothing. So pawns are kept home, the rest is let go as usual, the offer
        /// is sent again without the shells, and the player is told why.
        /// </para>
        /// </remarks>
        /// <param name="args">The server's answer</param>
        /// <param name="success">Whether it accepted the change</param>
        private void settleTradeOffer(UITradeUpdateEventArgs args, bool success)
        {
            // Nothing held means there's nothing to settle (no token, or the login check already did it)
            if (!ItemEscrow.IsHolding(args.Token)) return;

            if (success && args.Trade.ItemsOnOffer.Any(TradingThingConverter.IsStrippedPawn))
            {
                ItemEscrow.ResolveKeepingPawns(args.Token);

                // Re-send our offer minus the empty shells, so cancelling the trade doesn't drop "unknown items" on us
                UpdateTradeItems(args.Trade.TradeId, args.Trade.ItemsOnOffer.Where(protoThing => !TradingThingConverter.IsStrippedPawn(protoThing)));

                Messages.Message("Phinix_trade_pawnsNotSupported".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            ItemEscrow.Resolve(args.Token, success);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard()
            {
                ColumnWidth = Math.Min(600f, inRect.width / 2)
            };
            listing.Begin(inRect);

            listing.Label("Phinix_modSettings_serverAddressTitle".Translate());
            Settings.ServerAddress = listing.TextEntry(Settings.ServerAddress);

            listing.Label("Phinix_modSettings_serverPortTitle".Translate());
            string portStr = Settings.ServerPort.ToString();
            portStr = listing.TextEntry(portStr);
            int.TryParse(portStr, out int serverPort);
            Settings.ServerPort = serverPort;

            listing.Label("Phinix_modSettings_companionAddressTitle".Translate());
            Settings.CompanionAddress = listing.TextEntry(Settings.CompanionAddress);

            listing.Label("Phinix_modSettings_companionPortTitle".Translate());
            string companionPortStr = Settings.CompanionPort.ToString();
            companionPortStr = listing.TextEntry(companionPortStr);
            int.TryParse(companionPortStr, out int companionPort);
            Settings.CompanionPort = companionPort;

            listing.Label("Phinix_modSettings_displayNameTitle".Translate());
            Settings.DisplayName = listing.TextEntry(Settings.DisplayName);

            bool acceptingTrades = Settings.AcceptingTrades;
            listing.CheckboxLabeled("Phinix_modSettings_acceptingTradesTitle".Translate(), ref acceptingTrades);
            Settings.AcceptingTrades = acceptingTrades;

            bool showNameFormatting = Settings.ShowNameFormatting;
            listing.CheckboxLabeled("Phinix_modSettings_showNameFormatting".Translate(), ref showNameFormatting);
            Settings.ShowNameFormatting = showNameFormatting;

            bool showChatFormatting = Settings.ShowChatFormatting;
            listing.CheckboxLabeled("Phinix_modSettings_showChatFormatting".Translate(), ref showChatFormatting);
            Settings.ShowChatFormatting = showChatFormatting;

            bool playNoiseOnMessageReceived = Settings.PlayNoiseOnMessageReceived;
            listing.CheckboxLabeled("Phinix_modSettings_playNoiseOnMessageReceived".Translate(), ref playNoiseOnMessageReceived);
            Settings.PlayNoiseOnMessageReceived = playNoiseOnMessageReceived;

            bool showUnreadMessageCount = Settings.ShowUnreadMessageCount;
            listing.CheckboxLabeled("Phinix_modSettings_showUnreadMessageCount".Translate(), ref showUnreadMessageCount);
            Settings.ShowUnreadMessageCount = showUnreadMessageCount;

            bool showBlockedUnreadMessageCount = Settings.ShowBlockedUnreadMessageCount;
            listing.CheckboxLabeled("Phinix_modSettings_showBlockedUnreadMessageCount".Translate(), ref showBlockedUnreadMessageCount);
            Settings.ShowBlockedUnreadMessageCount = showBlockedUnreadMessageCount;

            listing.Label("Phinix_modSettings_chatMessageLimit".Translate());
            string limitStr = Settings.ChatMessageLimit.ToString();
            limitStr = listing.TextEntry(limitStr);
            int.TryParse(limitStr, out int chatMessageLimit);
            Settings.ChatMessageLimit = chatMessageLimit;

            bool forceMessageFieldFocus = Settings.ForceMessageFieldFocus;
            listing.CheckboxLabeled("Phinix_modSettings_forceMessageFieldFocus".Translate(), ref forceMessageFieldFocus);
            Settings.ForceMessageFieldFocus = forceMessageFieldFocus;

            bool allItemsTradable = Settings.AllItemsTradable;
            listing.CheckboxLabeled("Phinix_modSettings_allItemsTradable".Translate(), ref allItemsTradable);
            Settings.AllItemsTradable = allItemsTradable;

            bool showBlockedTrades = Settings.ShowBlockedTrades;
            listing.CheckboxLabeled("Phinix_modSettings_showBlockedTrades".Translate(), ref showBlockedTrades);
            Settings.ShowBlockedTrades = showBlockedTrades;

            bool shareColonyStats = Settings.ShareColonyStats;
            listing.CheckboxLabeled("Phinix_modSettings_shareColonyStats".Translate(), ref shareColonyStats);
            Settings.ShareColonyStats = shareColonyStats;

            bool dropCurrentMap = Settings.DropCurrentMap;
            listing.CheckboxLabeled("Phinix_modSettings_dropCurrentMap".Translate(), ref dropCurrentMap);
            Settings.DropCurrentMap = dropCurrentMap;

            // Three choices, so a button that opens a menu rather than a checkbox
            Rect giftsRow = listing.GetRect(Text.LineHeight);
            Widgets.Label(giftsRow.LeftPart(0.6f), "Phinix_modSettings_giftsFromStrangers".Translate());
            if (Widgets.ButtonText(giftsRow.RightPart(0.4f), ("Phinix_modSettings_giftsFromStrangers_" + Settings.GiftsFromStrangers).Translate()))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(GiftsFromStrangers)).Cast<GiftsFromStrangers>()
                    .Select(choice => new FloatMenuOption(("Phinix_modSettings_giftsFromStrangers_" + choice).Translate(), () => Settings.GiftsFromStrangers = choice))
                    .ToList()));
            }
            TooltipHandler.TipRegion(giftsRow, "Phinix_modSettings_giftsFromStrangers_tip".Translate());
            listing.Gap(listing.verticalSpacing);

            listing.End();
        }

        public override void WriteSettings()
        {
            if (!Settings.IsChanged) return;

            Settings.AcceptChanges();
            userManager.UpdateSelf(Settings.DisplayName, Settings.AcceptingTrades);
        }

        /// <summary>
        /// A player's bio, for the tooltip on their name. Nothing for players we've blocked or who haven't written one.
        /// </summary>
        /// <param name="uuid">UUID of the player</param>
        /// <param name="bio">Their bio</param>
        /// <returns>Whether there's a bio to show</returns>
        public bool TryGetBio(string uuid, out string bio)
        {
            bio = null;
            if (string.IsNullOrEmpty(uuid) || IsBlocked(uuid) || !FeatureEnabled(Features.PROFILES)) return false;

            // The main server first; the companion knows players from other servers
            if ((!userManager.TryGetBio(uuid, out bio) || string.IsNullOrEmpty(bio)) &&
                (!companionUserManager.TryGetBio(uuid, out bio) || string.IsNullOrEmpty(bio))) return false;

            bio = UserManager.CleanBio(bio);
            return bio.Length > 0;
        }

        /// <summary>
        /// The colony a player has shared for their player card. Nothing for players we've blocked or who aren't sharing.
        /// </summary>
        /// <param name="uuid">UUID of the player</param>
        /// <param name="colony">Their colony</param>
        /// <returns>Whether there's a colony to show</returns>
        public bool TryGetColony(string uuid, out ColonyStats colony)
        {
            colony = null;
            if (string.IsNullOrEmpty(uuid) || IsBlocked(uuid) || !FeatureEnabled(Features.PROFILES)) return false;
            return userManager.TryGetColony(uuid, out colony) || companionUserManager.TryGetColony(uuid, out colony);
        }

        /// <summary>
        /// The colony we're playing, as our player card shows it: the current map if it's a colony, otherwise any of
        /// ours. Null when there's no colony to show.
        /// </summary>
        private static ColonyStats currentColony()
        {
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null) return null;
            Map map = Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome ? Find.CurrentMap : Find.AnyPlayerHomeMap;
            if (map == null) return null;

            return UserManager.CleanColony(new ColonyStats
            {
                Name = map.Parent?.Label ?? "",
                Biome = map.Biome?.label ?? "",
                Days = GenDate.DaysPassed,
                Colonists = map.mapPawns.FreeColonistsSpawnedCount,
                Animals = map.mapPawns.SpawnedColonyAnimals.Count,
                // Rounded, so small changes don't send an update every minute
                Wealth = Mathf.Round(map.wealthWatcher.WealthTotal / 100f) * 100f
            });
        }

        /// <summary>
        /// Sends our colony to the servers when it's changed from what they have, or clears it there when we've stopped
        /// sharing. Only while profiles are on.
        /// </summary>
        private void shareColony()
        {
            if (Current.ProgramState != ProgramState.Playing || !FeatureEnabled(Features.PROFILES)) return;

            // An empty one clears it. While there's no colony (between games), what's there is kept
            ColonyStats colony = Settings.ShareColonyStats ? currentColony() : new ColonyStats();
            if (colony == null) return;

            void send(ClientUserManager manager)
            {
                if (!manager.LoggedIn) return;
                bool has = manager.TryGetColony(manager.Uuid, out ColonyStats shared);
                if (has ? shared.Equals(colony) : string.IsNullOrEmpty(colony.Name)) return;
                manager.UpdateSelf(colony: colony);
            }

            send(userManager);
            if (CompanionOnline) send(companionUserManager);
        }

        /// <summary>
        /// Turns sharing our colony on our player card on or off, and updates the servers straight away.
        /// </summary>
        public void SetShareColonyStats(bool share)
        {
            if (Settings.ShareColonyStats == share) return;
            Settings.ShareColonyStats = share;
            Settings.AcceptChanges();
            shareColony();
        }

        /// <summary>
        /// When a player last logged out, in Unix seconds by the server's clock. False if no server knows.
        /// </summary>
        public bool TryGetLastSeen(string uuid, out long lastSeen)
        {
            lastSeen = 0;
            return FeatureEnabled(Features.PROFILES) && (userManager.TryGetLastSeen(uuid, out lastSeen) || companionUserManager.TryGetLastSeen(uuid, out lastSeen));
        }

        /// <summary>
        /// "Last seen 3h ago", or "Last seen just now", for a time from <see cref="TryGetLastSeen"/>.
        /// </summary>
        /// <param name="lastSeen">Unix seconds</param>
        public static string FormatLastSeen(long lastSeen)
        {
            long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastSeen;
            if (seconds < 60) return "Phinix_chat_lastSeenJustNow".Translate();

            TimeSpan ago = TimeSpan.FromSeconds(seconds);
            // Long gaps go up to weeks, months and years rather than "400d"
            string amount = ago.TotalDays >= 365 ? (int) (ago.TotalDays / 365) + "y"
                          : ago.TotalDays >= 30 ? (int) (ago.TotalDays / 30) + "mo"
                          : ago.TotalDays >= 7 ? (int) (ago.TotalDays / 7) + "w"
                          : ago.TotalDays >= 1 ? (int) ago.TotalDays + "d"
                          : ago.TotalHours >= 1 ? (int) ago.TotalHours + "h"
                          : (int) ago.TotalMinutes + "m";
            return "Phinix_chat_lastSeen".Translate(amount);
        }

        /// <summary>
        /// Adds a user's UUID to the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of user to block</param>
        public void BlockUser(string senderUuid)
        {
            // Blocking a friend unfriends them
            RemoveFriend(senderUuid);

            if (!Settings.BlockedUsers.Add(senderUuid)) return;

            Settings.AcceptChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, true));

            // Close the inbox conversation if it was open, and end trades and contract proposals with them
            if (PhinixClient.GUI.InboxPanel.SelectedUuid == senderUuid) PhinixClient.GUI.InboxPanel.SelectedUuid = null;
            enforceBlocks();
        }

        /// <summary>
        /// Trades and contract proposals we've already asked the server to end, so <see cref="enforceBlocks"/> doesn't
        /// ask again every time something syncs. Only touched on the main thread.
        /// </summary>
        private readonly HashSet<string> endedForBlocks = new HashSet<string>();

        /// <summary>
        /// Ends everything still open with players we've blocked: open trades get cancelled (which sends back anything
        /// on offer, ours included) unless we chose to see blocked players' trades, and contract proposals either way
        /// get declined or withdrawn. Contracts already under way are left alone. Runs on the main thread, after
        /// blocking someone and whenever trades or contracts sync.
        /// </summary>
        private void enforceBlocks()
        {
            if (!Online || Settings.BlockedUsers.Count == 0) return;

            if (!Settings.ShowBlockedTrades)
            {
                foreach (ImmutableTrade trade in GetTrades())
                {
                    if (IsBlocked(trade.OtherPartyUuid) && endedForBlocks.Add(trade.TradeId)) CancelTrade(trade.TradeId);
                }
            }

            foreach (Agora.Contract contract in GetContracts())
            {
                if (contract.State != ContractState.Proposed) continue;
                if (!IsBlocked(contract.BuyerUuid) && !IsBlocked(contract.SupplierUuid)) continue;
                if (endedForBlocks.Add(contract.ContractId)) CancelContract(contract.ContractId);
            }
        }

        /// <summary>
        /// Removes a user's UUID from the blocked user list.
        /// </summary>
        /// <param name="senderUuid">UUID of the user to unblock</param>
        public void UnBlockUser(string senderUuid)
        {
            if (!Settings.BlockedUsers.Remove(senderUuid)) return;

            Settings.AcceptChanges();

            OnBlockedUsersChanged?.Invoke(this, new BlockedUsersChangedEventArgs(senderUuid, false));
        }

        /// <summary>
        /// Lock object protecting <see cref="PhinixClient.Settings.Friends"/>, which the user list reads from the
        /// network thread.
        /// </summary>
        private readonly object friendsLock = new object();

        /// <summary>
        /// Adds a user to the friends list, remembering their current display name so they can still be recognised
        /// if they change it. Unblocks them if they were blocked.
        /// </summary>
        /// <param name="uuid">UUID of the user to friend</param>
        public void AddFriend(string uuid)
        {
            if (string.IsNullOrEmpty(uuid) || uuid == Uuid) return;

            if (Settings.BlockedUsers.Contains(uuid)) UnBlockUser(uuid);

            string friendedAs = TryGetDisplayName(uuid, out string displayName) ? TextHelper.StripRichText(displayName) : "???";
            lock (friendsLock)
            {
                if (Settings.Friends.ContainsKey(uuid)) return;

                Settings.Friends[uuid] = friendedAs;
                Settings.AcceptChanges();
            }

            OnFriendsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Removes a user from the friends list.
        /// </summary>
        /// <param name="uuid">UUID of the user to unfriend</param>
        public void RemoveFriend(string uuid)
        {
            lock (friendsLock)
            {
                if (!Settings.Friends.Remove(uuid)) return;

                Settings.AcceptChanges();
            }

            OnFriendsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Whether the given user is on the friends list.
        /// </summary>
        /// <param name="uuid">UUID of the user</param>
        public bool IsFriend(string uuid)
        {
            if (!FriendsEnabled) return false;
            lock (friendsLock) return Settings.Friends.ContainsKey(uuid);
        }

        /// <summary>
        /// Whether the server has friends switched on. While it's off nobody counts as a friend, but the list is kept
        /// for when it comes back.
        /// </summary>
        public bool FriendsEnabled => FeatureEnabled(Features.FRIENDS);

        /// <summary>
        /// Gets a copy of the friends list: each friend's UUID and the display name they had when they were friended.
        /// Safe to call from any thread.
        /// </summary>
        public Dictionary<string, string> GetFriends()
        {
            if (!FriendsEnabled) return new Dictionary<string, string>();
            lock (friendsLock) return new Dictionary<string, string>(Settings.Friends);
        }

        /// <summary>
        /// A hook into the main update loop. Periodically updates state.
        /// </summary>
        /// <seealso cref="Patches.RootPatch.Update"/>
        public void Update()
        {
            connectOnlyWhileInGame();

            // Deliveries that arrived when there was no colony map to drop them on
            PendingDrops.Update();

            // Once a minute, keep the colony on our player card up to date
            if (Time.realtimeSinceStartup - lastColonyShare > 60f)
            {
                lastColonyShare = Time.realtimeSinceStartup;
                shareColony();
            }

            // Every 30 seconds, remind the player about contract deliveries they can make now
            if (Time.realtimeSinceStartup - lastDeliveryReminderCheck > 30f)
            {
                lastDeliveryReminderCheck = Time.realtimeSinceStartup;
                if (Online && Current.Game != null) remindAboutDeliveries();
            }

            lock (soundQueueLock)
            {
                // Check if we have sounds to play
                while (soundQueue.Any())
                {
                    // Dequeue and play a sound
                    SoundDef sound = soundQueue.Pop();
                    sound.PlayOneShotOnCamera();
                }
            }

            lock (tradeWindowQueueLock)
            {
                // Check if we have any trade windows to open
                while (tradeWindowQueue.Any())
                {
                    // Dequeue and open the window
                    Find.WindowStack.Add(new TradeWindow(tradeWindowQueue.Pop()));
                }
            }

            // Take every queued job out first, then run them outside the lock.
            // That way a job that queues another job can't deadlock, and the network thread isn't kept waiting.
            Action[] jobs;
            lock (mainThreadQueueLock)
            {
                jobs = mainThreadQueue.ToArray();
                mainThreadQueue.Clear();
            }
            foreach (Action job in jobs)
            {
                // One broken job shouldn't stop the rest (or the game's update loop) from running
                try
                {
                    job();
                }
                catch (Exception e)
                {
                    Verse.Log.Error(string.Format("Phinix main thread job failed: {0}", e));
                }
            }
        }

        /// <summary>
        /// Whether we've already tried connecting since the player loaded into a game.
        /// Stops us retrying every frame if the server is down or the player disconnected on purpose.
        /// </summary>
        private bool connectAttemptedThisGame = false;

        /// <summary>
        /// Connects when the player loads into a colony and disconnects when they go back to the main menu, so nobody
        /// shows as online while they're sitting in the menu.
        /// </summary>
        private void connectOnlyWhileInGame()
        {
            if (Current.ProgramState == ProgramState.Playing && !connectAttemptedThisGame)
            {
                // Just finished loading a colony
                connectAttemptedThisGame = true;
                if (!Connected) Connect(Settings.ServerAddress, Settings.ServerPort);
            }
            else if (GenScene.InEntryScene && connectAttemptedThisGame)
            {
                // Just went back to the main menu
                connectAttemptedThisGame = false;
                if (Connected) Disconnect();
            }
        }

        /// <summary>
        /// Queues a job to run on the main game thread on the next frame.
        /// Use this for anything that touches the game world from a network event handler.
        /// </summary>
        /// <param name="job">Job to run, e.g. a lambda like <c>() => { ... }</c></param>
        public void RunOnMainThread(Action job)
        {
            lock (mainThreadQueueLock) mainThreadQueue.Enqueue(job);
        }

        /// <summary>
        /// Attempts to connect to the server at the given address and port.
        /// This will disconnect from the current server, if any.
        /// </summary>
        /// <param name="address">Server address</param>
        /// <param name="port">Server port</param>
        public void Connect(string address, int port)
        {
            if (Connected) Disconnect();

            try
            {
                netClient.Connect(address, port);
            }
            catch
            {
                Verse.Log.Message(string.Format("Could not connect to {0}:{1}", Settings.ServerAddress, Settings.ServerPort));

                Find.WindowStack.Add(new Dialog_MessageBox(title: "Phinix_error_connectionFailedTitle".Translate(), text: "Phinix_error_connectionFailedMessage".Translate(Settings.ServerAddress, Settings.ServerPort)));
            }
        }

        /// <summary>
        /// If connected, disconnects from the current server.
        /// </summary>
        public void Disconnect()
        {
            netClient.Disconnect();
        }

        /// <summary>
        /// Connects to the companion server from the settings, if there is one. Called on the main thread once we've
        /// logged in to the main server, since the companion login claims the player we are there.
        /// </summary>
        private void connectCompanion()
        {
            if (string.IsNullOrWhiteSpace(Settings.CompanionAddress) || !LoggedIn || companionNetClient.Connected) return;

            try
            {
                companionNetClient.Connect(Settings.CompanionAddress.Trim(), Settings.CompanionPort);
            }
            catch (Exception e)
            {
                Verse.Log.Warning(string.Format("Could not connect to companion server {0}:{1}: {2}", Settings.CompanionAddress, Settings.CompanionPort, e.Message));
                Messages.Message("Phinix_companion_failed".Translate(e.Message), MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        /// <summary>
        /// A display name as it should be drawn: with its formatting if the player shows name formatting (repaired if
        /// its tags are out of order, see <see cref="TextHelper.RepairRichText"/>), otherwise plain.
        /// </summary>
        /// <param name="displayName">Display name, as sent</param>
        public static string ShownName(string displayName)
        {
            if (displayName == null) return null;
            if (!Instance.Settings.ShowNameFormatting) return TextHelper.StripRichText(displayName);

            // Some name generators close tags out of order, which the game can't draw, so put them right first
            if (TextHelper.CanDrawRichText(displayName)) return displayName;
            string repaired = TextHelper.RepairRichText(displayName);
            return TextHelper.CanDrawRichText(repaired) ? repaired : TextHelper.StripRichText(displayName);
        }

        /// <summary>
        /// Updates the user's display name locally and on the server.
        /// </summary>
        /// <param name="displayName">Display name</param>
        public void UpdateDisplayName(string displayName)
        {
            // Try to update within the user manager
            userManager.UpdateSelf(displayName);
        }

        /// <summary>
        /// Saves a new bio and sends it to the server (and the companion, which shows it to players from other servers).
        /// </summary>
        /// <param name="bio">New bio</param>
        public void UpdateBio(string bio)
        {
            Settings.Bio = UserManager.CleanBio(bio);
            Settings.AcceptChanges();

            userManager.UpdateSelf(bio: Settings.Bio);
            companionUserManager.UpdateSelf(bio: Settings.Bio);
        }

        /// <summary>
        /// Gets the current chat message buffer, optionally marking them as read.
        /// </summary>
        /// <param name="markAsRead">Whether to mark the messages as read</param>
        /// <param name="unreadOnly">Whether to only get unread messages</param>
        /// <returns>List of chat messages</returns>
        public UIChatMessage[] GetChatMessages(bool markAsRead = true, bool unreadOnly = false)
        {
            ClientChatMessage[] chatMessages = unreadOnly ? chat.GetUnreadMessages(markAsRead) : chat.GetMessages(markAsRead);

            return chatMessages.Select(m => new UIChatMessage(userManager, m)).ToArray();
        }

        /// <summary>
        /// Tries to get the chat message with the given ID.
        /// </summary>
        /// <param name="messageId">ID of the chat message to retrieve</param>
        /// <param name="message">Chat message output</param>
        /// <returns>Whether the chat message was retrieved successfully</returns>
        public bool TryGetMessage(string messageId, out UIChatMessage message)
        {
            message = null;

            // Try pull out the message
            if (!chat.TryGetMessage(messageId, out ClientChatMessage clientChatMessage)) return false;

            // Wrap it with the sender's user details
            message = new UIChatMessage(userManager, clientChatMessage);

            return true;
        }

        /// <summary>
        /// Creates a trade with the given user.
        /// </summary>
        /// <param name="uuid">Other party's UUID</param>
        /// <exception cref="ArgumentException">UUID cannot be null or empty</exception>
        public void CreateTrade(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                throw new ArgumentException("UUID cannot be null or empty", nameof(uuid));
            }

            // Add the other party to the waiting list so we can open it immediately
            lock (waitingForTradeCreationWithLock)
            {
                waitingForTradeCreationWith.Add(uuid);
            }

            tradingForNewTradeWith(uuid).CreateTrade(uuid);
        }

        /// <summary>
        /// Handler for <see cref="ILoggable"/> <c>OnLogEvent</c> events.
        /// Raised by modules as a way to hook into the log.
        /// </summary>
        /// <param name="sender">Object that raised the event</param>
        /// <param name="args">Event arguments</param>
        private void ILoggableHandler(object sender, LogEventArgs args)
        {
            switch (args.LogLevel)
            {
                case LogLevel.DEBUG:
                    if (Prefs.DevMode) Verse.Log.Message(args.Message);
                    break;
                case LogLevel.WARNING:
                    Verse.Log.Warning(args.Message);
                    break;
                case LogLevel.ERROR:
                case LogLevel.FATAL:
                    Verse.Log.Error(args.Message);
                    break;
                case LogLevel.INFO:
                default:
                    Verse.Log.Message(args.Message);
                    break;
            }
        }

        /// <summary>
        /// Handles credential requests from the <see cref="ClientAuthenticator"/> module.
        /// This forwards the server details and a callback to the GUI for user input.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="serverName">Server name</param>
        /// <param name="serverDescription">Server description</param>
        /// <param name="authType">Authentication type</param>
        /// <param name="callback">Callback delegate to pass entered credentials to</param>
        private void getCredentials(string sessionId, string serverName, string serverDescription, AuthTypes authType, ClientAuthenticator.ReturnCredentialsDelegate callback)
        {
            if (Prefs.DevMode) Verse.Log.Message(string.Format("Authentication needs more credentials for the server \"{0}\" with authentication type \"{1}\"", serverName, authType.ToString()));

            // Asked for from the network thread, and windows can only be opened on the main thread
            RunOnMainThread(() => Find.WindowStack.Add(new CredentialsWindow
            {
                SessionId = sessionId,
                ServerName = serverName,
                ServerDescription = serverDescription,
                AuthType = authType,
                CredentialsCallback = callback
            }));
        }

        /// <summary>
        /// Drops in items delivered from a bounty and sends a letter saying why they arrived.
        /// Must run on the main thread.
        /// </summary>
        /// <param name="delivery">The delivery from the server</param>
        private void receiveBountyDelivery(Agora.ClientAgora board, Agora.BountyDeliveryPacket delivery)
        {
            // The server sends a delivery again if our confirmation never reached it; it only gets confirmed again
            string logKey = "agora:" + delivery.DeliveryId;
            if (!string.IsNullOrEmpty(delivery.DeliveryId) && DeliveryLog.AlreadyReceived(logKey))
            {
                board.AckDeliveries(new[] { new Agora.DeliveryAck { DeliveryId = delivery.DeliveryId } });
                return;
            }

            // "Someone bought from your market" carries no items, just the news. The item rides along so we can name it,
            // and Detail is the silver it went for. A quick message is enough, since the till already shows the takings
            if (delivery.Reason == Agora.BountyDeliveryReason.ShopSale)
            {
                ProtoThing sold = delivery.Items.FirstOrDefault();
                if (sold != null)
                {
                    Messages.Message("Phinix_shop_sold".Translate(displayNameOf(delivery.OtherPartyUuid), sold.StackCount, ShopItems.Label(sold), delivery.Detail), MessageTypeDefOf.PositiveEvent, historical: false);
                }
                return;
            }

            // Drop the items in before anything else. The server has already let go of them, so if something below
            // went wrong first, they'd be lost for good
            // Build the items, skipping any this game doesn't have (the same way completed trades do). A market's till
            // or a merged stack can be bigger than one stack is allowed to be, so split those up first
            Thing[] things = buildReceivedItems(delivery.Items, out string lostNote, out List<int> unusable);
            // Some contract letters carry no items (e.g. "the contract ended"), so only send pods when there's something
            LookTargets dropSpot = things.Length > 0 ? dropPods(things) : null;

            // In the colony now (or kept safe until there's one), so the server can let go of them, and send back
            // whatever this game couldn't use
            if (!string.IsNullOrEmpty(delivery.DeliveryId))
            {
                DeliveryLog.Remember(logKey);
                Agora.DeliveryAck ack = new Agora.DeliveryAck { DeliveryId = delivery.DeliveryId };
                ack.UnusableItems.AddRange(unusable);
                board.AckDeliveries(new[] { ack });
            }

            // Say what was left behind on any animal or colonist with parts this game doesn't have
            announceMissingParts(delivery.Items);

            // Returned stock, a till, a cancelled or an expired bounty have no other player, so this is often empty
            string otherName = displayNameOf(delivery.OtherPartyUuid);

            // Each reason gets its own letter. The keys are e.g. Phinix_bounty_delivery_Fulfilled_label, built from
            // the enum's name, so adding a reason later only needs two new lines in English.xml
            string reasonName = delivery.Reason.ToString();
            bool good = delivery.Reason == Agora.BountyDeliveryReason.Fulfilled || delivery.Reason == Agora.BountyDeliveryReason.Reward ||
                        delivery.Reason == Agora.BountyDeliveryReason.ContractGoods || delivery.Reason == Agora.BountyDeliveryReason.ContractPayment ||
                        delivery.Reason == Agora.BountyDeliveryReason.ShopPurchase || delivery.Reason == Agora.BountyDeliveryReason.ShopTill ||
                        delivery.Reason == Agora.BountyDeliveryReason.ShopReturned;

            // Contracts add a line explaining what happened, e.g. "Delivery 2 of 5."
            string description = ("Phinix_bounty_delivery_" + reasonName + "_description").Translate(otherName);
            if (!string.IsNullOrEmpty(delivery.Detail)) description += "\n\n" + delivery.Detail;
            description += lostNote;

            LetterDef letterDef = DefDatabase<LetterDef>.GetNamed(good ? "TradeAccepted" : "TradeCancelled");
            Find.LetterStack.ReceiveLetter(
                ("Phinix_bounty_delivery_" + reasonName + "_label").Translate(),
                description,
                letterDef,
                dropSpot
            );
        }

        /// <summary>
        /// Pops up a message for each contract proposed to us that we haven't announced yet. Runs on the main thread.
        /// </summary>
        private void announceNewProposals()
        {
            foreach (Agora.Contract contract in GetVisibleContracts())
            {
                if (contract.State != ContractState.Proposed || contract.SupplierUuid != Uuid) continue;
                if (!announcedProposalIds.Add(contract.ContractId)) continue;  // Add returns false if it was already there

                Messages.Message("Phinix_contract_proposalReceived".Translate(displayNameOf(contract.BuyerUuid)), MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        /// <summary>
        /// Pops up a message for each contract delivery we could make now and haven't been reminded about.
        /// Missing a delivery ends the contract, so this nudge is worth the screen space. Runs on the main thread.
        /// </summary>
        private void remindAboutDeliveries()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (Agora.Contract contract in GetContracts())
            {
                if (contract.State != ContractState.Active || contract.SupplierUuid != Uuid) continue;
                if (now < ServerAgora.NextWindowOpens(contract)) continue;

                // One reminder per delivery: the key includes which delivery it is
                if (!remindedDeliveries.Add(contract.ContractId + ":" + contract.DeliveriesMade)) continue;

                string timeLeft = PhinixClient.GUI.ContractList.FormatDuration(ServerAgora.NextDeliveryDue(contract) - now);
                Messages.Message("Phinix_contract_deliveryReady".Translate(displayNameOf(contract.BuyerUuid), timeLeft), MessageTypeDefOf.CautionInput, historical: false);
            }
        }

        /// <summary>
        /// Handles a gift from the server: turns it away, asks about it or lets it in, depending on who it's from.
        /// Must run on the main thread.
        /// </summary>
        /// <param name="giver">The connection the gift came from</param>
        /// <param name="gift">The gift from the server</param>
        private void receiveGift(Hermes.ClientHermes giver, Hermes.GiftDeliveryPacket gift)
        {
            bool returned = gift.ReturnReason != Hermes.GiftReturnReason.NotReturned;
            string logKey = "gift:" + gift.GiftId;
            bool confirms = !string.IsNullOrEmpty(gift.GiftId);

            // The server sends a gift again if our confirmation never reached it; it only gets confirmed again
            if (confirms && DeliveryLog.AlreadyReceived(logKey))
            {
                giver.AckDeliveries(new[] { new Hermes.DeliveryAck { DeliveryId = gift.GiftId } });
                return;
            }

            // Gifts from players we've blocked are turned away: nothing drops in and there's no letter. The server
            // sends them back to the sender. (An older server can't, so there they're gone, as before.) Our own gifts
            // coming back always arrive
            if (!returned && IsBlocked(gift.SenderUuid))
            {
                if (Prefs.DevMode) Verse.Log.Message(string.Format("Turned away a gift of {0} item(s) from blocked player {1}", gift.Items.Count, gift.SenderUuid));
                if (confirms) giver.AckDeliveries(new[] { new Hermes.DeliveryAck { DeliveryId = gift.GiftId, Refused = true } });
                return;
            }

            // Gifts from people who aren't friends follow the player's setting. Friends' gifts always come straight in.
            // (Asking needs an ID to answer with, which older servers don't send, so those just arrive)
            if (!returned && confirms && !IsFriend(gift.SenderUuid))
            {
                switch (Settings.GiftsFromStrangers)
                {
                    case GiftsFromStrangers.Never:
                        giver.AckDeliveries(new[] { new Hermes.DeliveryAck { DeliveryId = gift.GiftId, Refused = true } });
                        return;
                    case GiftsFromStrangers.AskFirst:
                        // The server sends it back if the player never answers (see GiftOfferLetter.AnswerDays)
                        giver.ReportGiftsWaiting(new[] { gift.GiftId });

                        // Not in a game yet: leave it unconfirmed and the server offers it again next time we connect
                        if (Current.Game != null) GiftOfferLetter.Offer(giver, gift, displayNameOf(gift.SenderUuid));
                        return;
                }
            }

            AcceptGift(giver, gift);
        }

        /// <summary>
        /// Drops in a gift that's been let in, confirms it to the server and sends a letter saying who it's from.
        /// Must run on the main thread.
        /// </summary>
        /// <param name="giver">The connection the gift came from</param>
        /// <param name="gift">The gift from the server</param>
        public void AcceptGift(Hermes.ClientHermes giver, Hermes.GiftDeliveryPacket gift)
        {
            bool returned = gift.ReturnReason != Hermes.GiftReturnReason.NotReturned;
            string logKey = "gift:" + gift.GiftId;
            bool confirms = !string.IsNullOrEmpty(gift.GiftId);

            // Answered twice (two letters from two connections, say): it's already here
            if (confirms && DeliveryLog.AlreadyReceived(logKey))
            {
                giver.AckDeliveries(new[] { new Hermes.DeliveryAck { DeliveryId = gift.GiftId } });
                return;
            }

            // Build the items, skipping any this game doesn't have (the same way completed trades do)
            Thing[] things = buildReceivedItems(gift.Items, out string lostNote, out List<int> unusable);
            LookTargets dropSpot = dropPods(things);

            // In the colony now (or kept safe until there's one), so the server can let go of it, and send back
            // whatever this game couldn't use
            if (confirms)
            {
                DeliveryLog.Remember(logKey);
                Hermes.DeliveryAck ack = new Hermes.DeliveryAck { DeliveryId = gift.GiftId };
                ack.UnusableItems.AddRange(unusable);
                giver.AckDeliveries(new[] { ack });
            }

            // One of our gifts coming back: who it was for is in SenderUuid
            if (returned)
            {
                Find.LetterStack.ReceiveLetter(
                    "Phinix_gift_returned_label".Translate(),
                    ("Phinix_gift_returned_" + gift.ReturnReason + "_description").Translate(displayNameOf(gift.SenderUuid)) + lostNote,
                    LetterDefOf.NeutralEvent,
                    dropSpot
                );
                return;
            }

            string description = "Phinix_gift_received_description".Translate(displayNameOf(gift.SenderUuid));
            if (!string.IsNullOrWhiteSpace(gift.Note))
            {
                description += "\n\n" + "Phinix_gift_received_note".Translate(TextHelper.StripRichText(gift.Note));
            }
            description += lostNote;

            Find.LetterStack.ReceiveLetter(
                "Phinix_gift_received_label".Translate(displayNameOf(gift.SenderUuid)),
                description,
                LetterDefOf.PositiveEvent,
                dropSpot
            );
        }

        /// <summary>
        /// Tells the player what was left behind on each animal or colonist that just arrived, for the parts this game
        /// doesn't have (the converters skip those, see <see cref="PawnCheck"/>). Must run on the main thread.
        /// </summary>
        private static void announceMissingParts(IEnumerable<ProtoThing> arrived)
        {
            foreach (ProtoThing thing in arrived)
            {
                if (thing.Animal == null && thing.Colonist == null) continue;

                List<string> missing = PawnCheck.MissingParts(thing);
                if (missing.Count == 0) continue;

                Messages.Message("Phinix_pawnCheck_arrived".Translate(StackedThings.PawnLabel(thing), string.Join(", ", missing)), MessageTypeDefOf.CautionInput, historical: true);
            }
        }

        /// <summary>
        /// Builds received items (from a trade, market, bounty, contract or gift) one at a time, so one that fails to
        /// build can't stop the rest from arriving. Must run on the main thread.
        /// </summary>
        /// <remarks>
        /// Items this game doesn't have are left out quietly, as they always were (the converter turns them into
        /// "unknown items"). Anything that throws while being built is logged and named in
        /// <paramref name="lostNote"/>, since the server has already let go of it.
        /// </remarks>
        /// <param name="items">Items that arrived</param>
        /// <param name="lostNote">A paragraph for the letter naming items that failed to build, or empty</param>
        /// <returns>The items that were built</returns>
        private static Thing[] buildReceivedItems(IEnumerable<ProtoThing> items, out string lostNote)
        {
            return buildReceivedItems(items, out lostNote, out _);
        }

        /// <inheritdoc cref="buildReceivedItems(IEnumerable{ProtoThing}, out string)"/>
        /// <param name="unusable">Positions in <paramref name="items"/> of the ones this game couldn't build, for the
        /// server to send back</param>
        private static Thing[] buildReceivedItems(IEnumerable<ProtoThing> items, out string lostNote, out List<int> unusable)
        {
            List<Thing> things = new List<Thing>();
            List<string> lost = new List<string>();
            List<string> notInThisGame = new List<string>();
            unusable = new List<int>();

            int index = 0;
            foreach (ProtoThing item in items)
            {
                try
                {
                    Thing thing = TradingThingConverter.ConvertReceivedThing(item);
                    if (thing.def.defName != "UnknownItem")
                    {
                        things.Add(thing);
                    }
                    else
                    {
                        notInThisGame.Add(safeLabel(item));
                        unusable.Add(index);
                    }
                }
                catch (Exception e)
                {
                    Verse.Log.Error(string.Format("Phinix couldn't build a received {0}: {1}", item.DefName, e));
                    lost.Add(safeLabel(item));
                    unusable.Add(index);
                }
                index++;
            }

            lostNote = lost.Count > 0 ? "\n\n" + "Phinix_receivedItemsLost".Translate(string.Join(", ", lost)).ToString() : "";
            if (notInThisGame.Count > 0) lostNote += "\n\n" + "Phinix_receivedItemsUnknown".Translate(string.Join(", ", notInThisGame)).ToString();
            return things.ToArray();
        }

        /// <summary>
        /// Tells a server's market, gift and trade modules that we confirm deliveries, so it keeps ours until we do.
        /// A server without these modules (or an older one) just ignores it.
        /// </summary>
        private static void announceConfirmations(Agora.ClientAgora board, Hermes.ClientHermes giver, ClientTrading trader)
        {
            board.AckDeliveries(Enumerable.Empty<Agora.DeliveryAck>());
            giver.AckDeliveries(Enumerable.Empty<Hermes.DeliveryAck>());
            trader.AckDeliveries(Enumerable.Empty<Trading.DeliveryAck>());
        }

        /// <summary>
        /// Whether this colony already got a trade result. If so it's confirmed again, since the server only sends one
        /// again when our confirmation didn't reach it.
        /// </summary>
        private static bool alreadyReceived(ClientTrading trader, string logKey, string tradeId)
        {
            if (!DeliveryLog.AlreadyReceived(logKey)) return false;
            trader.AckDeliveries(new[] { new Trading.DeliveryAck { DeliveryId = tradeId } });
            return true;
        }

        /// <summary>
        /// Notes a trade result as received and confirms it to the server, naming what this game couldn't use.
        /// </summary>
        private static void confirmReceived(ClientTrading trader, string logKey, string tradeId, List<int> unusable)
        {
            DeliveryLog.Remember(logKey);
            Trading.DeliveryAck ack = new Trading.DeliveryAck { DeliveryId = tradeId };
            ack.UnusableItems.AddRange(unusable);
            trader.AckDeliveries(new[] { ack });
        }

        /// <summary>
        /// <see cref="ShopItems.Label"/>, falling back to the def name if even that fails on a broken item.
        /// </summary>
        private static string safeLabel(ProtoThing item)
        {
            try
            {
                return ShopItems.Label(item);
            }
            catch (Exception)
            {
                return item.DefName;
            }
        }

        /// <summary>
        /// A player's display name with colours stripped, or "???" if we don't know them.
        /// </summary>
        private string displayNameOf(string uuid)
        {
            // TryGetDisplayName throws on an empty UUID, which deliveries with no other player have
            if (string.IsNullOrEmpty(uuid)) return "???";
            return TryGetDisplayName(uuid, out string name) ? TextHelper.StripRichText(name) : "???";
        }

        /// <summary>
        /// Cuts long text down to fit in a top-left pop-up message.
        /// </summary>
        private static string shorten(string text, int maxLength = 80)
        {
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Launches the given <see cref="Thing"/>s in drop pods to a trade spot at the home colony.
        /// </summary>
        /// <param name="things">Collection of <see cref="Thing"/>s to drop</param>
        /// <returns>LookTarget for the drop location</returns>
        private LookTargets dropPods(IEnumerable<Thing> things)
        {
            // Splits oversized stacks, and keeps everything for later if there's no colony map to drop it on right now
            // (only caravans left, or another save loading), instead of losing it
            return PendingDrops.DropOrKeep(things);
        }
    }
}
