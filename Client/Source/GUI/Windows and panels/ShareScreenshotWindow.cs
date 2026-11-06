using RimWorld;
using UnityEngine;
using Utils;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Pop-up after taking a screenshot: shows it, takes an optional caption, and shares it in chat or sends it to one
    /// player as a private message.
    /// </summary>
    /// <remarks>
    /// Opened by <see cref="ScreenshotSharing.TakeAndShare"/> with the screenshot it just took. Only that screenshot can
    /// be shared; Retake throws it away and takes a new one.
    /// </remarks>
    public class ShareScreenshotWindow : Window
    {
        private const float SPACING = 10f;
        private const float TITLE_HEIGHT = 35f;
        private const float CAPTION_HEIGHT = 30f;
        private const float NOTE_HEIGHT = 22f;
        private const float BUTTON_HEIGHT = 40f;
        private const float BUTTON_WIDTH = 120f;

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        /// <summary>
        /// The screenshot, shrunk, to show. Destroyed when the window closes.
        /// </summary>
        private Texture2D preview;

        /// <summary>
        /// The screenshot as it will be sent.
        /// </summary>
        private readonly byte[] jpeg;

        private string caption = "";

        /// <summary>
        /// Who it's for as a private message, or null for the chat.
        /// </summary>
        private readonly string recipientUuid;

        /// <summary>
        /// Set when Retake was clicked, so closing doesn't bring the Phinix window back in the way of the next screenshot.
        /// </summary>
        private bool retaking;

        /// <param name="preview">The screenshot to show. This window destroys it when it closes</param>
        /// <param name="jpeg">The same screenshot as a JPEG, to send</param>
        /// <param name="recipientUuid">Who to send it to as a private message, or null to share it in the chat</param>
        public ShareScreenshotWindow(Texture2D preview, byte[] jpeg, string recipientUuid = null)
        {
            this.preview = preview;
            this.jpeg = jpeg;
            this.recipientUuid = recipientUuid;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;   // Enter shares, see OnAcceptKeyPressed
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            string title = recipientUuid == null ? "Phinix_screenshot_shareTitle".Translate().ToString() : "Phinix_screenshot_sendTitle".Translate(recipientName).ToString();
            Widgets.Label(new Rect(inRect.x, y, inRect.width, TITLE_HEIGHT), title);
            Text.Font = GameFont.Small;
            y += TITLE_HEIGHT;

            // The screenshot, as big as fits
            float pictureHeight = inRect.yMax - y - SPACING - CAPTION_HEIGHT - 4f - NOTE_HEIGHT - SPACING - BUTTON_HEIGHT;
            Rect pictureRect = new Rect(inRect.x, y, inRect.width, pictureHeight);
            Widgets.DrawMenuSection(pictureRect);
            if (preview != null) UnityEngine.GUI.DrawTexture(pictureRect.ContractedBy(4f), preview, ScaleMode.ScaleToFit);
            y = pictureRect.yMax + SPACING;

            // Caption, with a placeholder while it's empty
            Rect captionRect = new Rect(inRect.x, y, inRect.width, CAPTION_HEIGHT);
            caption = Widgets.TextField(captionRect, caption);
            if (caption.Length > ScreenshotSharing.MAX_CAPTION_LENGTH) caption = caption.Substring(0, ScreenshotSharing.MAX_CAPTION_LENGTH);
            if (caption.Length == 0)
            {
                TextAnchor previousAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                UnityEngine.GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(new Rect(captionRect.x + 6f, captionRect.y, captionRect.width - 6f, captionRect.height), "Phinix_screenshot_captionPlaceholder".Translate());
                UnityEngine.GUI.color = Color.white;
                Text.Anchor = previousAnchor;
            }
            y += CAPTION_HEIGHT + 4f;

            Text.Font = GameFont.Tiny;
            UnityEngine.GUI.color = Color.gray;
            string note = recipientUuid == null
                ? "Phinix_screenshot_shareNote".Translate(Client.Instance.ScreenshotLifetimeMinutes).ToString()
                : "Phinix_screenshot_sendNote".Translate(recipientName, Client.Instance.ScreenshotLifetimeMinutes).ToString();
            Widgets.Label(new Rect(inRect.x, y, inRect.width, NOTE_HEIGHT), note);
            UnityEngine.GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect shareRect = new Rect(inRect.xMax - BUTTON_WIDTH, inRect.yMax - BUTTON_HEIGHT, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect cancelRect = new Rect(shareRect.x - SPACING - BUTTON_WIDTH, shareRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect retakeRect = new Rect(inRect.x, shareRect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(retakeRect, "Phinix_screenshot_retake".Translate())) retake();
            if (Widgets.ButtonText(cancelRect, "CancelButton".Translate())) Close();
            if (Widgets.ButtonText(shareRect, "Phinix_screenshot_shareButton".Translate(), active: Client.Instance.ScreenshotsAvailable)) share();
        }

        public override void OnAcceptKeyPressed()
        {
            if (Client.Instance.ScreenshotsAvailable) share();
        }

        /// <summary>
        /// The player it's for, without formatting.
        /// </summary>
        private string recipientName => Client.Instance.TryGetDisplayName(recipientUuid, out string name) ? TextHelper.StripRichText(name) : "???";

        /// <summary>
        /// Sends the screenshot and closes. The message with the link goes out once the server has it.
        /// </summary>
        private void share()
        {
            ScreenshotSharing.Share(jpeg, caption, recipientUuid);
            Close();
        }

        /// <summary>
        /// Throws this screenshot away and takes another.
        /// </summary>
        private void retake()
        {
            retaking = true;
            Close(doCloseSound: false);
            ScreenshotSharing.TakeAndShare(recipientUuid);
        }

        public override void PostClose()
        {
            base.PostClose();
            ScreenshotSharing.DestroyTexture(ref preview);
            if (!retaking) ScreenshotSharing.ReopenPhinix(recipientUuid);
        }
    }
}
