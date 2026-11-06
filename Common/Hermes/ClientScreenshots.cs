using System;
using Authentication;
using Connections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using UserManagement;
using Utils;

namespace Hermes
{
    /// <summary>
    /// Client half of screenshot sharing: sends our screenshots to the server and asks for other people's.
    /// </summary>
    /// <remarks>
    /// Taking, showing and linking screenshots happens in the mod (ScreenshotSharing). This only carries the bytes.
    /// </remarks>
    public class ClientScreenshots : ILoggable
    {
        /// <inheritdoc/>
        public event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc/>
        public void RaiseLogEntry(LogEventArgs e) => OnLogEntry?.Invoke(this, e);

        /// <summary>
        /// Raised when the server answers one of our uploads. The token says which.
        /// </summary>
        public event EventHandler<UploadScreenshotResponsePacket> OnUploadResponse;

        /// <summary>
        /// Raised when a screenshot we asked for arrives, or the server says it's gone.
        /// </summary>
        public event EventHandler<ScreenshotPacket> OnScreenshotReceived;

        /// <summary>
        /// Whether this server keeps screenshots. Only a server with this module says so after login, so it stays
        /// false on an original Phinix server.
        /// </summary>
        public bool Available { get; private set; }

        /// <summary>
        /// How long the server keeps each screenshot, in minutes. Comes with <see cref="Available"/>.
        /// </summary>
        public int LifetimeMinutes { get; private set; }

        private NetClient netClient;
        private ClientAuthenticator authenticator;
        private ClientUserManager userManager;

        /// <summary>
        /// Initialises a new <see cref="ClientScreenshots"/> instance.
        /// </summary>
        public ClientScreenshots(NetClient netClient, ClientAuthenticator authenticator, ClientUserManager userManager)
        {
            this.netClient = netClient;
            this.authenticator = authenticator;
            this.userManager = userManager;

            netClient.RegisterPacketHandler(ServerScreenshots.MODULE_NAME, packetHandler);
            netClient.OnDisconnect += (sender, args) => Available = false;
        }

        /// <summary>
        /// Sends a screenshot to the server to keep. The answer arrives through <see cref="OnUploadResponse"/>.
        /// </summary>
        /// <param name="token">Matches the answer to this upload</param>
        /// <param name="jpeg">The picture</param>
        /// <returns>Whether the request went out</returns>
        public bool Upload(string token, byte[] jpeg)
        {
            if (!Available || !authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new UploadScreenshotPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                Token = token,
                Jpeg = ByteString.CopyFrom(jpeg)
            });
            return true;
        }

        /// <summary>
        /// Asks the server for a screenshot. It arrives through <see cref="OnScreenshotReceived"/>.
        /// </summary>
        /// <param name="screenshotId">The ID from the chat link</param>
        /// <returns>Whether the request went out</returns>
        public bool Request(string screenshotId)
        {
            if (!Available || !authenticator.Authenticated || !userManager.LoggedIn) return false;

            send(new GetScreenshotPacket
            {
                SessionId = authenticator.SessionId,
                Uuid = userManager.Uuid,
                ScreenshotId = screenshotId
            });
            return true;
        }

        /// <summary>
        /// Packs and sends a packet to the server.
        /// </summary>
        private void send(IMessage packet)
        {
            netClient.Send(ServerScreenshots.MODULE_NAME, ProtobufPacketHelper.Pack(packet).ToByteArray());
        }

        /// <summary>
        /// Handles incoming packets, sending each one to the right handler by its type.
        /// </summary>
        private void packetHandler(string module, string connectionId, byte[] data)
        {
            if (!ProtobufPacketHelper.ValidatePacket(typeof(ClientScreenshots).Namespace, ServerScreenshots.MODULE_NAME, module, data, out Any message, out TypeUrl typeUrl)) return;

            switch (typeUrl.Type)
            {
                case "ScreenshotsInfoPacket":
                    ScreenshotsInfoPacket info = message.Unpack<ScreenshotsInfoPacket>();
                    LifetimeMinutes = info.LifetimeMinutes;
                    Available = info.Enabled;
                    break;
                case "UploadScreenshotResponsePacket":
                    OnUploadResponse?.Invoke(this, message.Unpack<UploadScreenshotResponsePacket>());
                    break;
                case "ScreenshotPacket":
                    OnScreenshotReceived?.Invoke(this, message.Unpack<ScreenshotPacket>());
                    break;
                default:
                    RaiseLogEntry(new LogEventArgs("Got an unknown packet type (" + typeUrl.Type + "), discarding...", LogLevel.DEBUG));
                    break;
            }
        }
    }
}
