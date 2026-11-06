using System;
using System.Collections.Generic;
using System.Linq;
using Trading;

namespace Agora
{
    // The contract part of ClientAgora. Like ClientAgora.Bounties.cs, this only does networking: it keeps our copy of
    // our contracts, sends requests, and raises events with the server's answers. Moving items on and off the map
    // happens in the client mod.
    public partial class ClientAgora
    {
        /// <summary>
        /// Raised whenever a new copy of our contracts arrives.
        /// </summary>
        public event EventHandler OnContractsChanged;

        /// <summary>
        /// Raised when the server answers one of our proposals. The token says which held payment it's about.
        /// </summary>
        public event EventHandler<ProposeContractResponsePacket> OnProposeContractResponse;

        /// <summary>
        /// Raised when the server answers one of our deliveries. The token says which held goods it's about.
        /// </summary>
        public event EventHandler<DeliverContractResponsePacket> OnDeliverContractResponse;

        /// <summary>
        /// Contracts we're part of, as buyer or supplier.
        /// </summary>
        private List<Contract> contracts = new List<Contract>();
        private readonly object contractsLock = new object();

        /// <summary>
        /// Whether the server lets us make new contracts. Comes from the server in every contract sync, so the mod
        /// never needs its own setting for this. False until the first sync arrives.
        /// </summary>
        public bool ContractsEnabled { get; private set; }

        /// <summary>
        /// Returns a copy of our contracts, newest proposal first.
        /// </summary>
        public Contract[] GetContracts()
        {
            lock (contractsLock)
            {
                return contracts.OrderByDescending(contract => contract.ProposedTime).ToArray();
            }
        }

        /// <summary>
        /// Proposes a contract to a supplier. The whole payment must already be off the map.
        /// </summary>
        /// <param name="token">Random ID matching this request to its answer</param>
        /// <param name="supplierUuid">Who we want to supply us</param>
        /// <param name="goods">What we want each delivery. Each item's Quality is the lowest we'll accept</param>
        /// <param name="paymentPerDelivery">What we pay each delivery (def names and amounts)</param>
        /// <param name="heldPayment">The real payment items for every delivery, already taken off the map</param>
        /// <param name="totalDeliveries">How many deliveries</param>
        /// <param name="intervalHours">Hours between deliveries</param>
        /// <param name="note">Short message for the supplier</param>
        /// <returns>Whether the request went out. If false, put the payment back</returns>
        public bool ProposeContract(string token, string supplierUuid, IEnumerable<ProtoThing> goods, IEnumerable<ProtoThing> paymentPerDelivery, IEnumerable<ProtoThing> heldPayment, int totalDeliveries, int intervalHours, string note)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            Contract contract = new Contract
            {
                SupplierUuid = supplierUuid,
                TotalDeliveries = totalDeliveries,
                IntervalHours = intervalHours,
                Note = note ?? ""
            };
            contract.Goods.AddRange(goods);
            contract.PaymentPerDelivery.AddRange(paymentPerDelivery);
            contract.HeldPayment.AddRange(heldPayment);

            send(new ProposeContractPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                Contract = contract
            });
            return true;
        }

        /// <summary>
        /// Accepts a contract someone proposed to us.
        /// </summary>
        public void AcceptContract(string contractId)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;
            send(new AcceptContractPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, ContractId = contractId });
        }

        /// <summary>
        /// Declines, withdraws or cancels a contract, depending on its state and which side we're on.
        /// </summary>
        public void CancelContract(string contractId)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;
            send(new CancelContractPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid, ContractId = contractId });
        }

        /// <summary>
        /// Makes one delivery on a contract we supply. The goods must already be off the map.
        /// </summary>
        /// <returns>Whether the request went out. If false, put the goods back</returns>
        public bool DeliverContract(string token, string contractId, IEnumerable<ProtoThing> items)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            DeliverContractPacket packet = new DeliverContractPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                ContractId = contractId
            };
            packet.Items.AddRange(items);

            send(packet);
            return true;
        }

        /// <summary>
        /// Raised when the server answers <see cref="CheckTokens"/>.
        /// </summary>
        public event EventHandler<TokenResultsPacket> OnTokenResults;

        /// <summary>
        /// Asks the server which of these held-item tokens it accepted. Used after logging back in, to settle items
        /// that were mid-hand-over when the connection dropped (see Packets/EscrowPackets.proto).
        /// </summary>
        /// <param name="tokens">Tokens we're still holding items for</param>
        /// <summary>
        /// Tells the server these deliveries are in our colony, so it can stop holding them, and which of their things
        /// this game couldn't use, so it can send them back. Calling it with nothing right after logging in tells the
        /// server we confirm, so it keeps our deliveries until we do.
        /// </summary>
        /// <param name="acks">One per delivery</param>
        public void AckDeliveries(IEnumerable<DeliveryAck> acks)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            AckDeliveriesPacket packet = new AckDeliveriesPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid };
            packet.Acks.AddRange(acks);
            send(packet);
        }

        public void CheckTokens(IEnumerable<string> tokens)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            CheckTokensPacket packet = new CheckTokensPacket { SessionId = authenticator.SessionId, Uuid = userManager.Uuid };
            packet.Tokens.AddRange(tokens);
            if (packet.Tokens.Count == 0) return;  // Nothing to ask about

            send(packet);
        }

        /// <summary>
        /// Replaces our copy of our contracts with the one the server sent.
        /// </summary>
        private void syncContractsPacketHandler(SyncContractsPacket packet)
        {
            lock (contractsLock)
            {
                contracts = packet.Contracts.ToList();
                ContractsEnabled = packet.Enabled;
            }
            OnContractsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forgets our contracts on disconnect. Called from disconnectHandler in ClientAgora.cs.
        /// </summary>
        private void clearContracts()
        {
            lock (contractsLock)
            {
                contracts.Clear();
                ContractsEnabled = false;
            }
            OnContractsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
