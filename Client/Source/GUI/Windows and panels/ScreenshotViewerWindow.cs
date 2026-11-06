using System;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Pop-out window showing a screenshot someone shared in chat. It can be moved and resized, and the game carries on
    /// behind it.
    /// </summary>
    /// <remarks>
    /// Opened by <see cref="ScreenshotSharing.Open"/>, which has already asked the server for the picture if we didn't
    /// have it. Screenshots from blocked players are never shown, even through someone else's link.
    /// </remarks>
    public class ScreenshotViewerWindow : Window
    {
        private const float HEADER_HEIGHT = 28f;

        /// <summary>
        /// How long to wait for the server before saying the screenshot didn't come.
        /// </summary>
        private const float TIMEOUT_SECONDS = 30f;

        public override Vector2 InitialSize => new Vector2(960f, 600f);

        private readonly string screenshotId;

        /// <summary>
        /// Whether we asked the server for it. False when we already had it, or when screenshots don't work here.
        /// </summary>
        private readonly bool requested;

        private readonly float openedAt;

        /// <summary>
        /// The decoded picture, made the first time it's drawn. Destroyed when the window closes.
        /// </summary>
        private Texture2D texture;

        /// <summary>
        /// Set if the picture couldn't be decoded, so it isn't tried every frame.
        /// </summary>
        private bool decodeFailed;

        /// <param name="screenshotId">The screenshot's ID</param>
        /// <param name="requested">Whether <see cref="ScreenshotSharing.Open"/> asked the server for it</param>
        public ScreenshotViewerWindow(string screenshotId, bool requested)
        {
            this.screenshotId = screenshotId;
            this.requested = requested;
            openedAt = Time.realtimeSinceStartup;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            closeOnClickedOutside = false;
            draggable = true;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width - 30f, HEADER_HEIGHT);
            Rect pictureRect = new Rect(inRect.x, inRect.y + HEADER_HEIGHT, inRect.width, inRect.height - HEADER_HEIGHT);
            Widgets.DrawMenuSection(pictureRect);

            if (!ScreenshotSharing.TryGet(screenshotId, out ScreenshotSharing.Entry entry))
            {
                // Still waiting, or never asked because screenshots don't work on our server
                string waiting = !requested ? "Phinix_screenshot_unavailable".Translate().ToString()
                    : Time.realtimeSinceStartup - openedAt > TIMEOUT_SECONDS ? "Phinix_screenshot_noAnswer".Translate().ToString()
                    : "Phinix_screenshot_loading".Translate().ToString();
                Widgets.NoneLabelCenteredVertically(pictureRect, waiting);
                return;
            }

            if (!entry.Found)
            {
                Widgets.NoneLabelCenteredVertically(pictureRect, "Phinix_screenshot_gone".Translate());
                return;
            }

            // Checked every frame, so blocking someone while their screenshot is open hides it straight away
            if (Client.Instance.IsBlocked(entry.OwnerUuid))
            {
                ScreenshotSharing.DestroyTexture(ref texture);
                Widgets.NoneLabelCenteredVertically(pictureRect, "Phinix_screenshot_blocked".Translate());
                return;
            }

            // "From Lumen · gone in 85 min"
            string owner = Client.Instance.TryGetDisplayName(entry.OwnerUuid, out string name) ? TextHelper.StripRichText(name) : "???";
            long minutesLeft = Math.Max(0, (entry.ExpiresTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 59) / 60);
            UnityEngine.GUI.color = Color.gray;
            Widgets.Label(headerRect, "Phinix_screenshot_from".Translate(owner, minutesLeft));
            UnityEngine.GUI.color = Color.white;

            if (texture == null && !decodeFailed) decode(entry.Jpeg);
            if (texture == null)
            {
                Widgets.NoneLabelCenteredVertically(pictureRect, "Phinix_screenshot_gone".Translate());
                return;
            }

            UnityEngine.GUI.DrawTexture(pictureRect.ContractedBy(4f), texture, ScaleMode.ScaleToFit);
        }

        /// <summary>
        /// Turns the JPEG into a texture to draw. The bytes were already checked to be a JPEG of a sensible size
        /// (<see cref="ScreenshotSharing.Received"/>).
        /// </summary>
        private void decode(byte[] jpeg)
        {
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ScreenshotSharing.UnityImaging.LoadImage(texture, jpeg))
                {
                    ScreenshotSharing.DestroyTexture(ref texture);
                    decodeFailed = true;
                }
            }
            catch (Exception e)
            {
                Log.Warning("Phinix couldn't show a screenshot: " + e.Message);
                ScreenshotSharing.DestroyTexture(ref texture);
                decodeFailed = true;
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            ScreenshotSharing.DestroyTexture(ref texture);
        }
    }
}
