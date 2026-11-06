using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Hermes
{
    /// <summary>
    /// Server half of screenshot sharing. Keeps the screenshots players take for a short while and hands them to anyone
    /// who clicks the link in chat.
    /// </summary>
    /// <remarks>
    /// Screenshots are only kept in memory, never written to disk, so they're gone after <c>lifetime</c> or when the
    /// server restarts, whichever comes first. Its own module (see Packets/ScreenshotPackets.proto) so a server without it
    /// never answers and Phime hides the button there.
    /// </remarks>
    public class ServerScreenshots : ILoggable
    {
        /// <inheritdoc/>
        public event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Name packets are routed by.
        /// </summary>
        public const string MODULE_NAME = "Screenshots";

        /// <summary>
        /// Most screenshots one player can have kept at once. Sharing another lets the oldest go.
        /// </summary>
        private const int MAX_PER_USER = 3;

        /// <summary>
        /// Most memory all kept screenshots can take together. Past this, new ones are turned away until old ones expire.
        /// </summary>
        private const long MAX_TOTAL_BYTES = 64L * 1024 * 1024;

        /// <summary>
        /// Longest a screenshot can be kept, whatever server.conf says. They're meant for showing something now, not
        /// for storing pictures.
        /// </summary>
        private const int MAX_LIFETIME_MINUTES = 120;

        /// <summary>
        /// One kept screenshot.
        /// </summary>
        private class StoredScreenshot
        {
            public string Id;
            public string OwnerUuid;
            public byte[] Jpeg;
            public long TakenTime;
            public long ExpiresTime;
        }

        private NetServer netServer;
        private ServerAuthenticator authenticator;
        private ServerUserManager userManager;

        /// <summary>
        /// Kept screenshots by ID. Protected by <see cref="screenshotsLock"/>.
        /// </summary>
        private readonly Dictionary<string, StoredScreenshot> screenshots = new Dictionary<string, StoredScreenshot>();

        /// <summary>
        /// Lock protecting <see cref="screenshots"/>.
        /// </summary>
        private readonly object screenshotsLock = new object();

        /// <summary>
        /// Whether players can share screenshots (EnableScreenshots in server.conf).
        /// </summary>
        private readonly bool enabled;

        /// <summary>
        /// How long each screenshot is kept.
        /// </summary>
        private readonly TimeSpan lifetime;

        /// <summary>
        /// Stops anyone filling the server up with screenshots.
        /// </summary>
        private readonly RateLimiter uploadRateLimiter;

        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

        /// <summary>
        /// Initialises a new <see cref="ServerScreenshots"/> instance.
        /// </summary>
        /// <param name="netServer"><see cref="NetServer"/> to receive packets from</param>
        /// <param name="authenticator"><see cref="ServerAuthenticator"/> to check sessions with</param>
        /// <param name="userManager"><see cref="ServerUserManager"/> to check logins with</param>
        /// <param name="enabled">Whether players can share screenshots</param>
        /// <param name="lifetimeMinutes">How long each screenshot is kept</param>
        /// <param name="uploadRateLimitUploads">Most screenshots one player can share within <paramref name="uploadRateLimitSeconds"/></param>
        /// <param name="uploadRateLimitSeconds">Length of the upload rate limit window in seconds</param>
        public ServerScreenshots(NetServer netServer, ServerAuthenticator authenticator, ServerUserManager userManager, bool enabled = true, int lifetimeMinutes = 120, int uploadRateLimitUploads = 2, int uploadRateLimitSeconds = 60)
        {
            this.netServer = netServer;
            this.authenticator = authenticator;
            this.userManager = userManager;
            this.enabled = enabled;
            this.lifetime = TimeSpan.FromMinutes(Math.Max(1, Math.Min(lifetimeMinutes, MAX_LIFETIME_MINUTES)));
            this.uploadRateLimiter = new RateLimiter(uploadRateLimitUploads, TimeSpan.FromSeconds(uploadRateLimitSeconds));

            netServer.RegisterPacketHandler(MODULE_NAME, packetHandler);
            userManager.OnLogin += loginHandler;
        }

        /// <summary>
        /// Packs and sends a packet to one connection. Returns whether it went.
        /// </summary>
        private bool send(string connectionId, IMessage packet)
        {
            if (connectionId == null) return false;
            return netServer.TrySend(connectionId, MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        /// <summary>
        /// Handles incoming packets, sending each one to the right handler by its type.
        /// </summary>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ServerScreenshots).Namespace, MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            switch (typeUrl.Type)
            {
                case "UploadScreenshotPacket":
                    uploadPacketHandler(connectionId, message.Unpack<UploadScreenshotPacket>());
                    break;
                case "GetScreenshotPacket":
                    getPacketHandler(connectionId, message.Unpack<GetScreenshotPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }

        /// <summary>
        /// Tells a player their screenshot wasn't kept, and why.
        /// </summary>
        private void refuseUpload(string connectionId, UploadScreenshotPacket packet, string reason)
        {
            send(connectionId, new UploadScreenshotResponsePacket { Success = false, Token = packet.Token, FailureMessage = reason });
        }

        /// <summary>
        /// Handles a player sharing a screenshot.
        /// </summary>
        private void uploadPacketHandler(string connectionId, UploadScreenshotPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid))
            {
                refuseUpload(connectionId, packet, "Session is not valid");
                return;
            }
            if (!enabled)
            {
                refuseUpload(connectionId, packet, "Screenshots are turned off on this server");
                return;
            }

            byte[] jpeg = packet.Jpeg.ToByteArray();
            if (!ScreenshotImage.IsAcceptableJpeg(jpeg, out int width, out int height))
            {
                refuseUpload(connectionId, packet, "That screenshot is too big or isn't a picture");
                return;
            }
            if (!uploadRateLimiter.TryAct(packet.Uuid))
            {
                refuseUpload(connectionId, packet, "You are sharing screenshots too fast!");
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StoredScreenshot stored = new StoredScreenshot
            {
                Id = newId(),
                OwnerUuid = packet.Uuid,
                Jpeg = jpeg,
                TakenTime = now,
                ExpiresTime = now + (long)lifetime.TotalSeconds
            };

            lock (screenshotsLock)
            {
                removeExpired();

                // Sharing another lets this player's oldest go, so nobody can hold more than MAX_PER_USER
                List<StoredScreenshot> theirs = screenshots.Values.Where(s => s.OwnerUuid == packet.Uuid).OrderBy(s => s.TakenTime).ToList();
                for (int i = 0; i <= theirs.Count - MAX_PER_USER; i++) screenshots.Remove(theirs[i].Id);

                if (screenshots.Values.Sum(s => (long)s.Jpeg.Length) + jpeg.Length > MAX_TOTAL_BYTES)
                {
                    refuseUpload(connectionId, packet, "The server is holding too many screenshots right now. Try again in a little while");
                    return;
                }

                screenshots[stored.Id] = stored;
            }

            RaiseLogEntry(new LogEventArgs(string.Format("{0} shared a screenshot ({1}, {2}x{3}, {4} KB)", packet.Uuid.Highlight(HighlightType.UUID), stored.Id, width, height, jpeg.Length / 1024)));
            send(connectionId, new UploadScreenshotResponsePacket { Success = true, Token = packet.Token, ScreenshotId = stored.Id });
        }

        /// <summary>
        /// Handles a player asking for a screenshot. On a companion, players only see screenshots from players of their
        /// own main server, like everything else there.
        /// </summary>
        private void getPacketHandler(string connectionId, GetScreenshotPacket packet)
        {
            if (!authenticator.IsAuthenticated(connectionId, packet.SessionId) || !userManager.IsLoggedIn(connectionId, packet.Uuid)) return;

            StoredScreenshot stored;
            lock (screenshotsLock)
            {
                removeExpired();
                screenshots.TryGetValue(packet.ScreenshotId ?? "", out stored);
            }

            if (stored == null || !userManager.SameHomeServer(packet.Uuid, stored.OwnerUuid))
            {
                send(connectionId, new ScreenshotPacket { ScreenshotId = packet.ScreenshotId ?? "", Found = false });
                return;
            }

            send(connectionId, new ScreenshotPacket
            {
                ScreenshotId = stored.Id,
                Found = true,
                OwnerUuid = stored.OwnerUuid,
                Jpeg = ByteString.CopyFrom(stored.Jpeg),
                TakenTime = stored.TakenTime,
                ExpiresTime = stored.ExpiresTime
            });
        }

        /// <summary>
        /// Tells a player who just logged in that this server keeps screenshots.
        /// </summary>
        private void loginHandler(object sender, ServerLoginEventArgs args)
        {
            send(args.ConnectionId, new ScreenshotsInfoPacket
            {
                Enabled = enabled,
                MaxBytes = ScreenshotImage.MAX_BYTES,
                LifetimeMinutes = (int)lifetime.TotalMinutes
            });
        }

        /// <summary>
        /// Lets go of screenshots past their time. Must be called while holding <see cref="screenshotsLock"/>.
        /// </summary>
        private void removeExpired()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (string id in screenshots.Values.Where(s => s.ExpiresTime <= now).Select(s => s.Id).ToList())
            {
                screenshots.Remove(id);
            }
        }

        /// <summary>
        /// A new random screenshot ID: 16 hex digits, too many to guess.
        /// </summary>
        private string newId()
        {
            byte[] bytes = new byte[8];
            random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
