using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Trading;
using Utils;

namespace Agora
{
    // The bounty half of ClientAgora. See the note on "partial" at the top of ClientAgora.cs.
    //
    // This file only does networking: it keeps a copy of the bounty board, sends the three bounty requests, and raises
    // events when the server answers. Taking items off the map and dropping deliveries in is game work, so it lives in
    // the client mod (PhinixClient's Client.cs and BountyEscrow.cs), which has access to RimWorld. This module can't
    // see RimWorld at all, because the server uses it too.
    public partial class ClientAgora
    {
        /// <summary>
        /// Raised whenever a new copy of the bounty board arrives.
        /// </summary>
        public event EventHandler OnBountiesChanged;

        /// <summary>
        /// Raised when the server answers one of our bounty posts.
        /// </summary>
        public event EventHandler<PostBountyResponsePacket> OnPostBountyResponse;

        /// <summary>
        /// Raised when the server answers one of our attempts to fulfil a bounty.
        /// </summary>
        public event EventHandler<FulfilBountyResponsePacket> OnFulfilBountyResponse;

        /// <summary>
        /// Raised when items arrive from a bounty: what we asked for, a reward, or a reward coming back.
        /// </summary>
        public event EventHandler<BountyDeliveryPacket> OnBountyDelivery;

        /// <summary>
        /// Our copy of the bounty board, replaced whenever the server sends a new one.
        /// </summary>
        private List<Bounty> bounties = new List<Bounty>();
        /// <summary>
        /// Lock object protecting <see cref="bounties"/>, since packets arrive on the network thread while the UI reads
        /// the board on the game thread.
        /// </summary>
        private readonly object bountiesLock = new object();

        /// <summary>
        /// Returns a copy of every bounty on the board, newest first.
        /// </summary>
        public Bounty[] GetBounties()
        {
            lock (bountiesLock)
            {
                return bounties.OrderByDescending(bounty => bounty.PostedTime).ToArray();
            }
        }

        /// <summary>
        /// Asks the server to put a bounty up. The reward items must already be off the map.
        /// The answer arrives through <see cref="OnPostBountyResponse"/> carrying the same <paramref name="token"/>.
        /// </summary>
        /// <param name="token">Random ID matching this request to its answer</param>
        /// <param name="wantedItems">What we want. Each item's Quality is the lowest we'll accept</param>
        /// <param name="rewardItems">What we'll pay</param>
        /// <param name="note">Short message to go with the bounty</param>
        /// <returns>Whether the request went out. If false, the caller should put the reward back</returns>
        public bool PostBounty(string token, IEnumerable<ProtoThing> wantedItems, IEnumerable<ProtoThing> rewardItems, string note)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            Bounty bounty = new Bounty { Note = note ?? "" };
            bounty.WantedItems.AddRange(wantedItems);
            bounty.RewardItems.AddRange(rewardItems);

            send(new PostBountyPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                Bounty = bounty
            });
            return true;
        }

        /// <summary>
        /// Asks the server to take one of our bounties down. The reward comes back through <see cref="OnBountyDelivery"/>.
        /// </summary>
        /// <param name="bountyId">ID of the bounty to cancel</param>
        public void CancelBounty(string bountyId)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return;

            send(new CancelBountyPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                BountyId = bountyId
            });
        }

        /// <summary>
        /// Hands in items for a bounty. The items must already be off the map.
        /// The answer arrives through <see cref="OnFulfilBountyResponse"/> carrying the same <paramref name="token"/>.
        /// </summary>
        /// <param name="token">Random ID matching this request to its answer</param>
        /// <param name="bountyId">ID of the bounty being fulfilled</param>
        /// <param name="items">Items being handed in</param>
        /// <returns>Whether the request went out. If false, the caller should put the items back</returns>
        public bool FulfilBounty(string token, string bountyId, IEnumerable<ProtoThing> items)
        {
            if (!authenticator.Authenticated || !userManager.LoggedIn) return false;

            FulfilBountyPacket packet = new FulfilBountyPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                BountyId = bountyId
            };
            packet.Items.AddRange(items);

            send(packet);
            return true;
        }

        /// <summary>
        /// Packs and sends any packet to the server, saving the same two lines in every method above.
        /// </summary>
        /// <param name="packet">Packet to send</param>
        private void send(IMessage packet)
        {
            Any packedPacket = ProtobufPacketHelper.Pack(packet);
            netClient.Send(MODULE_NAME, packedPacket.ToByteArray());
        }

        /// <summary>
        /// Replaces our copy of the bounty board with the one the server sent.
        /// </summary>
        private void syncBountiesPacketHandler(SyncBountiesPacket packet)
        {
            lock (bountiesLock)
            {
                bounties = packet.Bounties.ToList();
            }

            OnBountiesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forgets the bounty board. Called when we disconnect.
        /// </summary>
        private void clearBounties()
        {
            lock (bountiesLock)
            {
                bounties.Clear();
            }

            OnBountiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
