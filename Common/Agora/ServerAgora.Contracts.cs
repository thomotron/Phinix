using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Trading;
using Utils;

namespace Agora
{
    // The contract part of ServerAgora. See the note on "partial" at the top of ServerAgora.cs, and the explanation of
    // what a contract is at the top of Packets/Contract.proto.
    //
    // How a contract moves through the server:
    //
    //   1. PROPOSE  The buyer's client takes the WHOLE payment (one delivery's payment times the number of
    //               deliveries) off their map and sends a ProposeContractPacket naming the supplier. We check it and
    //               keep it as Proposed. From now on we're holding the payment, exactly like a bounty's reward.
    //
    //   2. ACCEPT   The supplier says yes. The contract becomes Active and the clock starts: delivery 1 is due within
    //               IntervalHours, delivery 2 within the IntervalHours after that, and so on.
    //
    //   3. DELIVER  The supplier's client takes one delivery's goods off their map and sends them. We check they
    //               match, then give the goods to the buyer and one delivery's payment to the supplier. After the
    //               last delivery the contract is finished and removed.
    //
    //   4. END EARLY  Either side cancels, the supplier declines, nobody answers a proposal in time, or the supplier
    //               misses a delivery. In every case the contract is removed and whatever payment is left goes back
    //               to the buyer. The supplier keeps everything they were already paid for.
    //
    // Items are moved with deliver() from ServerAgora.Bounties.cs, so online/offline delivery and saving work exactly
    // like bounties. Everything here uses bountiesLock for the same reason: deliver() needs it, and one lock means
    // nothing can ever be half-finished.
    public partial class ServerAgora
    {
        /// <summary>
        /// Every contract, proposed or active, looked up by contract ID.
        /// </summary>
        private Dictionary<string, Contract> contracts = new Dictionary<string, Contract>();

        /// <summary>
        /// Limits how often each user can propose a contract.
        /// </summary>
        private RateLimiter contractRateLimiter;

        /// <summary>
        /// Whether new contracts can be made on this server (EnableContracts in server.conf, off by default).
        /// </summary>
        /// <remarks>
        /// When it's off, proposals are refused and any proposals still waiting are ended with a refund. Contracts
        /// that were already accepted keep running to the end, so nobody's deal gets torn up by a config change.
        /// </remarks>
        private bool contractsEnabled;

        /// <summary>
        /// Most contracts (proposed or active, as buyer or supplier) one user can be part of at once.
        /// </summary>
        private int maxContractsPerUser;

        /// <summary>
        /// Most different items a contract's goods, or its payment, can list.
        /// </summary>
        public const int MAX_ITEMS_PER_CONTRACT = 10;

        /// <summary>
        /// Most deliveries one contract can have.
        /// </summary>
        public const int MAX_CONTRACT_DELIVERIES = 20;

        /// <summary>
        /// Longest gap between deliveries, in hours (one week).
        /// </summary>
        public const int MAX_CONTRACT_INTERVAL_HOURS = 168;

        /// <summary>
        /// Sets up the contract part. Called from the constructor in ServerAgora.cs.
        /// </summary>
        private void initContracts(bool contractsEnabled, int maxContractsPerUser, int postRateLimitPosts, int postRateLimitSeconds)
        {
            this.contractsEnabled = contractsEnabled;
            this.maxContractsPerUser = maxContractsPerUser;
            this.contractRateLimiter = new RateLimiter(postRateLimitPosts, TimeSpan.FromSeconds(postRateLimitSeconds));
        }

        #region Timing

        // These are public and static so the client can show exactly the same times the server uses.
        //
        // Each delivery gets its own window, one after another, starting when the contract was accepted:
        //
        //   accepted        +1 interval       +2 intervals      +3 intervals
        //      |-- delivery 1 --|-- delivery 2 --|-- delivery 3 --|
        //
        // A delivery can be made any time inside its window, but not before it opens. That keeps the deal steady
        // (the buyer asked for goods every N hours, not all at once). If a window closes with no delivery, the
        // supplier has broken the contract.

        /// <summary>
        /// When the next delivery's window opens, in Unix seconds.
        /// </summary>
        public static long NextWindowOpens(Contract contract)
        {
            return contract.AcceptedTime + (long) contract.DeliveriesMade * contract.IntervalHours * 3600;
        }

        /// <summary>
        /// When the next delivery is due by, in Unix seconds. Missing this ends the contract.
        /// </summary>
        public static long NextDeliveryDue(Contract contract)
        {
            return contract.AcceptedTime + (long) (contract.DeliveriesMade + 1) * contract.IntervalHours * 3600;
        }

        #endregion

        #region Packet handlers

        /// <summary>
        /// Handles a buyer proposing a contract.
        /// </summary>
        private void proposeContractPacketHandler(string connectionId, ProposeContractPacket packet)
        {
            // Every refusal makes the buyer's client put the payment back, so refusing is always safe
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "Session is not valid");
                return;
            }

            if (!contractsEnabled)
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "Contracts are turned off on this server");
                return;
            }

            Contract proposal = packet.Contract;
            if (proposal == null)
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "No contract was sent");
                return;
            }

            if (proposal.SupplierUuid == packet.Uuid || !userManager.TryGetDisplayName(proposal.SupplierUuid, out _))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, userManager.CompanionMode ? "That player hasn't linked to this companion server yet" : "That player doesn't exist");
                return;
            }

            // On a companion, contracts only run between players from the same main server
            if (!userManager.SameHomeServer(packet.Uuid, proposal.SupplierUuid))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "That player is on a different server");
                return;
            }

            if (proposal.Goods.Count == 0 || proposal.Goods.Count > MAX_ITEMS_PER_CONTRACT ||
                proposal.PaymentPerDelivery.Count == 0 || proposal.PaymentPerDelivery.Count > MAX_ITEMS_PER_CONTRACT)
            {
                sendProposeContractResponse(connectionId, false, packet.Token, string.Format("A contract needs between 1 and {0} goods and between 1 and {0} payment items", MAX_ITEMS_PER_CONTRACT));
                return;
            }

            // An amount of 0 would make a delivery (or a payment) free
            if (proposal.Goods.Any(item => item.StackCount < 1) || proposal.PaymentPerDelivery.Any(item => item.StackCount < 1))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "Every item needs an amount of at least 1");
                return;
            }

            if (proposal.TotalDeliveries < 1 || proposal.TotalDeliveries > MAX_CONTRACT_DELIVERIES)
            {
                sendProposeContractResponse(connectionId, false, packet.Token, string.Format("A contract needs between 1 and {0} deliveries", MAX_CONTRACT_DELIVERIES));
                return;
            }

            if (proposal.IntervalHours < 1 || proposal.IntervalHours > MAX_CONTRACT_INTERVAL_HOURS)
            {
                sendProposeContractResponse(connectionId, false, packet.Token, string.Format("Deliveries need to be between 1 and {0} hours apart", MAX_CONTRACT_INTERVAL_HOURS));
                return;
            }

            // The most important check: is the held payment really enough to pay for every delivery?
            if (!PaymentCovers(proposal.HeldPayment, proposal.PaymentPerDelivery, proposal.TotalDeliveries, out string reason))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, reason);
                return;
            }

            if (!contractRateLimiter.TryAct(packet.Uuid))
            {
                sendProposeContractResponse(connectionId, false, packet.Token, "You are proposing too fast!");
                return;
            }

            // Copy it and overwrite everything the server decides (never trust the client)
            Contract contract = proposal.Clone();
            contract.ContractId = Guid.NewGuid().ToString();
            contract.BuyerUuid = packet.Uuid;
            contract.State = ContractState.Proposed;
            contract.DeliveriesMade = 0;
            contract.ProposedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            contract.AcceptedTime = 0;
            contract.Note = TextHelper.SanitiseRichText(contract.Note ?? "");
            if (contract.Note.Length > MAX_NOTE_LENGTH) contract.Note = contract.Note.Substring(0, MAX_NOTE_LENGTH);

            lock (bountiesLock)
            {
                // Both players have to have room. Checked inside the lock so two proposals at once can't both sneak in
                if (contractCount(contract.BuyerUuid) >= maxContractsPerUser)
                {
                    sendProposeContractResponse(connectionId, false, packet.Token, string.Format("You can only be in {0} contracts at once", maxContractsPerUser));
                    return;
                }
                if (contractCount(contract.SupplierUuid) >= maxContractsPerUser)
                {
                    sendProposeContractResponse(connectionId, false, packet.Token, "That player is already in as many contracts as they can be");
                    return;
                }

                contracts.Add(contract.ContractId, contract);
                rememberToken(packet.Uuid, packet.Token);  // Receipt, in case our answer gets lost (ServerAgora.Escrow.cs)
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} proposed contract {1} to {2}", packet.Uuid.Highlight(HighlightType.UUID), contract.ContractId, contract.SupplierUuid.Highlight(HighlightType.UUID))));

            sendProposeContractResponse(connectionId, true, packet.Token, "");
            syncContractsFor(contract.BuyerUuid, contract.SupplierUuid);
        }

        /// <summary>
        /// Handles a supplier accepting a proposal. This starts the delivery clock.
        /// </summary>
        private void acceptContractPacketHandler(string connectionId, AcceptContractPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            Contract contract;
            lock (bountiesLock)
            {
                // Only the supplier can accept, and only while it's still a proposal (and contracts are on)
                if (!contractsEnabled) return;
                if (!contracts.TryGetValue(packet.ContractId, out contract)) return;
                if (contract.SupplierUuid != packet.Uuid || contract.State != ContractState.Proposed) return;

                contract.State = ContractState.Active;
                contract.AcceptedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} accepted contract {1}", packet.Uuid.Highlight(HighlightType.UUID), contract.ContractId)));
            syncContractsFor(contract.BuyerUuid, contract.SupplierUuid);
        }

        /// <summary>
        /// Handles either side ending a contract: declining or withdrawing a proposal, or cancelling an active one.
        /// </summary>
        private void cancelContractPacketHandler(string connectionId, CancelContractPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            Contract contract;
            lock (bountiesLock)
            {
                if (!contracts.TryGetValue(packet.ContractId, out contract)) return;

                bool isBuyer = contract.BuyerUuid == packet.Uuid;
                bool isSupplier = contract.SupplierUuid == packet.Uuid;
                if (!isBuyer && !isSupplier) return;  // Not their contract

                string buyerName = nameOf(contract.BuyerUuid);
                string supplierName = nameOf(contract.SupplierUuid);

                // Work out what to tell each side. The buyer always gets their leftover payment back; the supplier
                // only needs a letter if the buyer pulled out of a deal they'd already agreed to
                if (contract.State == ContractState.Proposed)
                {
                    endContract(contract, isBuyer ? "You withdrew the proposal." : supplierName + " declined the proposal.", null);
                }
                else if (isBuyer)
                {
                    endContract(contract, "You cancelled the contract.", buyerName + " cancelled the contract.");
                }
                else
                {
                    endContract(contract, supplierName + " cancelled the contract.", null);
                }
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} ended contract {1}", packet.Uuid.Highlight(HighlightType.UUID), contract.ContractId)));
            syncContractsFor(contract.BuyerUuid, contract.SupplierUuid);
        }

        /// <summary>
        /// Handles a supplier making one delivery.
        /// </summary>
        private void deliverContractPacketHandler(string connectionId, DeliverContractPacket packet)
        {
            // Every refusal makes the supplier's client put the goods back
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                sendDeliverContractResponse(connectionId, false, packet.Token, "Session is not valid");
                return;
            }

            Contract contract;
            bool finished;
            lock (bountiesLock)
            {
                if (!contracts.TryGetValue(packet.ContractId, out contract) || contract.SupplierUuid != packet.Uuid)
                {
                    sendDeliverContractResponse(connectionId, false, packet.Token, "That contract isn't available any more");
                    return;
                }

                if (contract.State != ContractState.Active)
                {
                    sendDeliverContractResponse(connectionId, false, packet.Token, "That contract hasn't been accepted yet");
                    return;
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now < NextWindowOpens(contract))
                {
                    sendDeliverContractResponse(connectionId, false, packet.Token, "The next delivery isn't due yet");
                    return;
                }

                // Too late. The once-a-minute check will end the contract shortly, so just refuse for now
                if (now >= NextDeliveryDue(contract))
                {
                    sendDeliverContractResponse(connectionId, false, packet.Token, "This delivery was due already, so the contract is ending");
                    return;
                }

                // Same matching rules as bounties: right items, enough of them, good enough quality
                if (!ItemsSatisfy(contract.Goods, packet.Items, out string reason))
                {
                    sendDeliverContractResponse(connectionId, false, packet.Token, reason);
                    return;
                }

                contract.DeliveriesMade++;
                rememberToken(packet.Uuid, packet.Token);
                finished = contract.DeliveriesMade >= contract.TotalDeliveries;

                // "Delivery 2 of 5", or a note that it's all done
                string detail = finished
                    ? "That was the final delivery, so the contract is complete."
                    : string.Format("Delivery {0} of {1}.", contract.DeliveriesMade, contract.TotalDeliveries);

                // Goods to the buyer, one delivery's payment to the supplier
                deliver(contract.BuyerUuid, contract.ContractId, BountyDeliveryReason.ContractGoods, contract.SupplierUuid, packet.Items, detail);
                deliver(contract.SupplierUuid, contract.ContractId, BountyDeliveryReason.ContractPayment, contract.BuyerUuid, takeOneDeliveryOfPayment(contract), detail);

                if (finished)
                {
                    // Nothing should be left over, but if it somehow is (say a rounding mistake in a future change),
                    // it goes back to the buyer rather than vanishing
                    endContract(contract, "The contract is complete.", null);
                }
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} made delivery {1}/{2} on contract {3}", packet.Uuid.Highlight(HighlightType.UUID), contract.DeliveriesMade, contract.TotalDeliveries, contract.ContractId)));

            sendDeliverContractResponse(connectionId, true, packet.Token, "");
            syncContractsFor(contract.BuyerUuid, contract.SupplierUuid);
        }

        #endregion

        #region Payment

        /// <summary>
        /// Checks the held payment covers one delivery's payment, <paramref name="times"/> times over.
        /// </summary>
        /// <remarks>
        /// Public and static so the client can show the same total before anything leaves the map.
        /// Quality doesn't matter for payment: the buyer chose those exact items themselves.
        /// </remarks>
        /// <param name="held">The real items being held</param>
        /// <param name="perDelivery">What one delivery pays</param>
        /// <param name="times">How many deliveries</param>
        /// <param name="reason">What's missing, if anything</param>
        public static bool PaymentCovers(IEnumerable<ProtoThing> held, IEnumerable<ProtoThing> perDelivery, int times, out string reason)
        {
            List<ProtoThing> heldList = held.ToList();

            // Add up each def's amount, in case the same item was listed twice. Python: collections.Counter
            Dictionary<string, int> needed = totalsByDef(perDelivery);

            if (heldList.Any(item => !needed.ContainsKey(item.DefName)))
            {
                reason = "Some of the payment items aren't part of the deal";
                return false;
            }

            foreach (KeyValuePair<string, int> line in needed)
            {
                int have = heldList.Where(item => item.DefName == line.Key).Sum(item => item.StackCount);
                if (have < line.Value * times)
                {
                    reason = string.Format("Not enough {0} to pay for every delivery (needs {1}, got {2})", line.Key, line.Value * times, have);
                    return false;
                }
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// Adds up amounts by def name, e.g. [10 steel, 5 steel, 2 gold] gives {steel: 15, gold: 2}.
        /// </summary>
        private static Dictionary<string, int> totalsByDef(IEnumerable<ProtoThing> items)
        {
            // GroupBy is like itertools.groupby (but without needing to sort first)
            return items.GroupBy(item => item.DefName).ToDictionary(group => group.Key, group => group.Sum(item => item.StackCount));
        }

        /// <summary>
        /// Takes one delivery's worth of payment out of the contract's held payment and returns it.
        /// Must be called while holding <see cref="bountiesLock"/>.
        /// </summary>
        /// <remarks>
        /// The held payment is a list of real stacks, e.g. one stack of 500 silver for five deliveries of 100. We take
        /// stacks off the front until we have enough, splitting the last one if it's too big: 500 becomes a 100 we
        /// hand over and a 400 that stays behind.
        /// <para>
        /// Items that can't stack (like a gun) are always a stack of 1, so they never need splitting.
        /// </para>
        /// </remarks>
        private List<ProtoThing> takeOneDeliveryOfPayment(Contract contract)
        {
            List<ProtoThing> payment = new List<ProtoThing>();

            foreach (KeyValuePair<string, int> line in totalsByDef(contract.PaymentPerDelivery))
            {
                int stillNeeded = line.Value;

                // Walk the held stacks by index, since we may remove some as we go
                for (int i = 0; i < contract.HeldPayment.Count && stillNeeded > 0; )
                {
                    ProtoThing stack = contract.HeldPayment[i];
                    if (stack.DefName != line.Key)
                    {
                        i++;
                        continue;
                    }

                    if (stack.StackCount <= stillNeeded)
                    {
                        // Take the whole stack. Don't move i on: the next stack has just slid into this position
                        payment.Add(stack);
                        contract.HeldPayment.RemoveAt(i);
                        stillNeeded -= stack.StackCount;
                    }
                    else
                    {
                        // Split it: hand over a copy with just what we need, and shrink the original
                        ProtoThing part = stack.Clone();
                        part.StackCount = stillNeeded;
                        stack.StackCount -= stillNeeded;
                        payment.Add(part);
                        stillNeeded = 0;
                    }
                }
            }

            return payment;
        }

        #endregion

        #region Ending, expiry and syncing

        /// <summary>
        /// Removes a contract, sends any leftover payment back to the buyer, and optionally tells the supplier.
        /// Must be called while holding <see cref="bountiesLock"/>.
        /// </summary>
        /// <param name="contract">Contract to end</param>
        /// <param name="buyerDetail">Explanation for the buyer's letter</param>
        /// <param name="supplierDetail">Explanation for the supplier's letter, or null to not send them one</param>
        private void endContract(Contract contract, string buyerDetail, string supplierDetail)
        {
            contracts.Remove(contract.ContractId);

            if (contract.HeldPayment.Count > 0)
            {
                deliver(contract.BuyerUuid, contract.ContractId, BountyDeliveryReason.ContractRefund, contract.SupplierUuid, contract.HeldPayment, buyerDetail);
            }

            if (supplierDetail != null)
            {
                // No items, just the letter. deliver() still handles the "they're offline" case for us
                deliver(contract.SupplierUuid, contract.ContractId, BountyDeliveryReason.ContractEnded, contract.BuyerUuid, new ProtoThing[0], supplierDetail);
            }
        }

        /// <summary>
        /// Ends proposals nobody answered and active contracts whose supplier missed a delivery.
        /// Runs on the same once-a-minute timer as bounty expiry (see expireBounties).
        /// </summary>
        private void checkContracts()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<Contract> ended = new List<Contract>();

            lock (bountiesLock)
            {
                // ToList() copies, because endContract removes from the dictionary we'd otherwise be looping over
                foreach (Contract contract in contracts.Values.ToList())
                {
                    if (contract.State == ContractState.Proposed && !contractsEnabled)
                    {
                        // Left over from before contracts were turned off. Nobody can accept it now, so give the payment back
                        endContract(contract, "Contracts were turned off on this server, so your proposal was cancelled.", null);
                        ended.Add(contract);
                    }
                    else if (contract.State == ContractState.Proposed && contract.ProposedTime + (long) bountyLifetime.TotalSeconds <= now)
                    {
                        endContract(contract, nameOf(contract.SupplierUuid) + " didn't answer the proposal in time.", null);
                        ended.Add(contract);
                    }
                    else if (contract.State == ContractState.Active && NextDeliveryDue(contract) <= now)
                    {
                        endContract(contract,
                            nameOf(contract.SupplierUuid) + " missed a delivery, so the contract has ended.",
                            "You missed a delivery, so the contract has ended. You keep the payment for the deliveries you made.");
                        ended.Add(contract);
                    }
                }
            }

            foreach (Contract contract in ended)
            {
                RaiseLogEntry(new LogEventArgs(string.Format("Contract {0} ended (expired or missed delivery)", contract.ContractId)));
                syncContractsFor(contract.BuyerUuid, contract.SupplierUuid);
            }
        }

        /// <summary>
        /// How many contracts a player is part of, on either side. Must be called while holding <see cref="bountiesLock"/>.
        /// </summary>
        private int contractCount(string uuid)
        {
            return contracts.Values.Count(contract => contract.BuyerUuid == uuid || contract.SupplierUuid == uuid);
        }

        /// <summary>
        /// A player's display name with formatting stripped, for letters. Falls back to "Someone".
        /// </summary>
        private string nameOf(string uuid)
        {
            return userManager.TryGetDisplayName(uuid, out string name) ? TextHelper.StripRichText(name) : "Someone";
        }

        /// <summary>
        /// Sends each of the given players (if online) the contracts they're part of.
        /// </summary>
        /// <remarks>
        /// The "params" keyword lets callers pass any number of UUIDs, like *args in Python.
        /// </remarks>
        private void syncContractsFor(params string[] uuids)
        {
            foreach (string uuid in uuids.Distinct())
            {
                if (userManager.TryGetConnection(uuid, out string connectionId)) sendSyncContracts(connectionId, uuid);
            }
        }

        /// <summary>
        /// Sends one player their contracts. Also called on login from ServerAgora.cs.
        /// </summary>
        private void sendSyncContracts(string connectionId, string uuid)
        {
            SyncContractsPacket packet = new SyncContractsPacket { Enabled = contractsEnabled };
            lock (bountiesLock)
            {
                packet.Contracts.AddRange(contracts.Values.Where(contract => contract.BuyerUuid == uuid || contract.SupplierUuid == uuid));
            }
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        private void sendProposeContractResponse(string connectionId, bool success, string token, string failureMessage)
        {
            ProposeContractResponsePacket packet = new ProposeContractResponsePacket { Success = success, Token = token ?? "", FailureMessage = failureMessage };
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        private void sendDeliverContractResponse(string connectionId, bool success, string token, string failureMessage)
        {
            DeliverContractResponsePacket packet = new DeliverContractResponsePacket { Success = success, Token = token ?? "", FailureMessage = failureMessage };
            netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        #endregion
    }
}
