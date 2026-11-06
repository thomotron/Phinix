using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hermes;
using PhinixClient.GUI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PhinixClient
{
    /// <summary>
    /// Sharing screenshots in chat: taking one, sending it to the server, turning the tag in a chat message into a link,
    /// and fetching the picture when someone clicks it.
    /// </summary>
    /// <remarks>
    /// Only a screenshot Phime takes on the spot can be shared, never a picture from the player's computer. The server
    /// keeps it for a couple of hours at most (see ServerScreenshots), and the chat message carries a tag like
    /// "[Phime screenshot 0123456789abcdef]", which players on the original Phinix see as plain text.
    /// <para>
    /// Everything here runs on the main thread, except <see cref="Received"/>, which the network thread calls.
    /// </para>
    /// </remarks>
    public static class ScreenshotSharing
    {
        /// <summary>
        /// Finds the screenshot tag in a chat message. The ID is the one the server picked.
        /// </summary>
        private static readonly Regex TagRegex = new Regex(@"\[Phime screenshot ([0-9a-f]{16})\]", RegexOptions.Compiled);

        /// <summary>
        /// Longest caption that can go with a screenshot, in characters.
        /// </summary>
        public const int MAX_CAPTION_LENGTH = 200;

        /// <summary>
        /// How many fetched screenshots to keep in memory, so opening one again doesn't fetch it again.
        /// </summary>
        private const int CACHE_SIZE = 10;

        /// <summary>
        /// JPEG qualities to try, best first, until the picture fits under <see cref="ScreenshotImage.MAX_BYTES"/>.
        /// </summary>
        private static readonly int[] JpegQualities = { 80, 65, 50, 35 };

        /// <summary>
        /// A screenshot we know about: fetched from the server, or one of our own.
        /// </summary>
        public class Entry
        {
            /// <summary>False when the server said it has expired or never existed.</summary>
            public bool Found;
            public string OwnerUuid;
            public byte[] Jpeg;
            /// <summary>Unix seconds.</summary>
            public long ExpiresTime;
        }

        /// <summary>
        /// Screenshots by ID, at most <see cref="CACHE_SIZE"/>, oldest first in <see cref="cacheOrder"/>. Protected by
        /// <see cref="cacheLock"/>, since <see cref="Received"/> fills it from the network thread.
        /// </summary>
        private static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        private static readonly List<string> cacheOrder = new List<string>();
        private static readonly object cacheLock = new object();

        /// <summary>
        /// One of our screenshots on its way to the server, and where to post the link once it has it.
        /// </summary>
        private class PendingShare
        {
            public byte[] Jpeg;
            public string Caption;
            /// <summary>Who to send it to as a private message, or null for the chat.</summary>
            public string RecipientUuid;
        }

        /// <summary>
        /// Our screenshots on their way to the server, by upload token.
        /// </summary>
        private static readonly Dictionary<string, PendingShare> uploading = new Dictionary<string, PendingShare>();

        /// <summary>
        /// Whether a screenshot is being taken right now.
        /// </summary>
        private static bool capturing;

        /// <summary>
        /// The tag that goes in a chat message for a screenshot.
        /// </summary>
        public static string Tag(string screenshotId) => "[Phime screenshot " + screenshotId + "]";

        /// <summary>
        /// Finds the first screenshot tag in a chat message.
        /// </summary>
        /// <param name="text">The message</param>
        /// <param name="screenshotId">The screenshot's ID</param>
        /// <returns>Whether the message has one</returns>
        public static bool TryFindId(string text, out string screenshotId)
        {
            Match match = text == null ? null : TagRegex.Match(text);
            screenshotId = match != null && match.Success ? match.Groups[1].Value : null;
            return screenshotId != null;
        }

        /// <summary>
        /// Replaces every screenshot tag in a message with the link text players see.
        /// </summary>
        /// <param name="text">The message</param>
        /// <param name="colour">Whether to colour the link (only when the message shows formatting)</param>
        public static string ShowLinks(string text, bool colour)
        {
            if (text == null || text.IndexOf("[Phime screenshot ", StringComparison.Ordinal) < 0) return text;

            string link = "Phinix_screenshot_link".Translate().ToString();
            if (colour) link = link.Colorize(PhimeStyle.Gold);
            return TagRegex.Replace(text, link);
        }

        /// <summary>
        /// Closes the Phinix window, takes a screenshot once it's gone, and opens the window to share it.
        /// </summary>
        /// <param name="recipientUuid">Who to send it to as a private message, or null to share it in the chat</param>
        public static void TakeAndShare(string recipientUuid = null)
        {
            if (capturing || !Client.Instance.ScreenshotsAvailable || Current.ProgramState != ProgramState.Playing) return;
            if (!UnityImaging.Ready)
            {
                Messages.Message("Phinix_screenshot_captureFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            capturing = true;
            Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            Find.Root.StartCoroutine(capture(recipientUuid));
        }

        /// <summary>
        /// Opens the Phinix window again: on whatever tab it was on, or on the conversation the screenshot was for.
        /// </summary>
        /// <param name="recipientUuid">The other player in that conversation, or null</param>
        public static void ReopenPhinix(string recipientUuid = null)
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (recipientUuid != null)
            {
                ServerTab.OpenConversation(recipientUuid);
                return;
            }
            MainButtonDef chatButton = DefDatabase<MainButtonDef>.GetNamed("Chat");
            if (Find.MainTabsRoot.OpenTab != chatButton) Find.MainTabsRoot.SetCurrentTab(chatButton, playSound: false);
        }

        /// <summary>
        /// Waits for a frame drawn without the Phinix window, reads the screen at the end of it, then shrinks it and
        /// opens <see cref="ShareScreenshotWindow"/>.
        /// </summary>
        /// <param name="recipientUuid">Who it's for as a private message, or null for the chat</param>
        private static IEnumerator capture(string recipientUuid)
        {
            // The frame the Phinix window closed in may already be partly drawn, so skip it
            yield return null;

            // The screen can only be read once a frame has been drawn. If a read comes back empty, try the next frame
            Texture2D shot = null;
            Color32[] pixels = null;
            for (int attempt = 0; attempt < 3 && pixels == null; attempt++)
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    shot = UnityImaging.CaptureScreen();
                    Color32[] read = shot?.GetPixels32();
                    if (read != null && looksCaptured(read)) pixels = read;
                    else DestroyTexture(ref shot);
                }
                catch (Exception e)
                {
                    Log.Warning("Phinix couldn't read the screen: " + e.Message);
                    DestroyTexture(ref shot);
                }
            }

            capturing = false;
            if (pixels == null)
            {
                Messages.Message("Phinix_screenshot_captureFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                ReopenPhinix(recipientUuid);
                yield break;
            }

            Texture2D preview = null;
            try
            {
                preview = shrink(pixels, shot.width, shot.height);
                byte[] jpeg = encode(preview);
                if (jpeg == null)
                {
                    DestroyTexture(ref preview);
                    Messages.Message("Phinix_screenshot_captureFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    ReopenPhinix(recipientUuid);
                }
                else
                {
                    Find.WindowStack.Add(new ShareScreenshotWindow(preview, jpeg, recipientUuid));
                }
            }
            catch (Exception e)
            {
                Log.Error("Phinix couldn't prepare a screenshot: " + e);
                DestroyTexture(ref preview);
                Messages.Message("Phinix_screenshot_captureFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                ReopenPhinix(recipientUuid);
            }
            finally
            {
                DestroyTexture(ref shot);
            }
        }

        /// <summary>
        /// Whether the screen was really read. A failed read on 1.7 comes back as an empty picture, all zeroes, so this
        /// looks for anything at all in a spread of pixels.
        /// </summary>
        private static bool looksCaptured(Color32[] pixels)
        {
            int step = Math.Max(1, pixels.Length / 257);
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color32 pixel = pixels[i];
                if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0 || pixel.a != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Scales a screenshot down to <see cref="ScreenshotImage.TARGET_WIDTH"/> wide (never up), averaging the pixels
        /// each new one covers so text stays readable.
        /// </summary>
        /// <param name="source">Pixels, bottom row first, as Unity gives them</param>
        /// <param name="width">Source width</param>
        /// <param name="height">Source height</param>
        /// <returns>A new texture with the smaller picture</returns>
        private static Texture2D shrink(Color32[] source, int width, int height)
        {
            double scale = Math.Min(1.0, Math.Min((double)ScreenshotImage.TARGET_WIDTH / width, (double)ScreenshotImage.MAX_SIDE / height));
            int newWidth = Math.Max(1, (int)Math.Round(width * scale));
            int newHeight = Math.Max(1, (int)Math.Round(height * scale));

            Color32[] result = new Color32[newWidth * newHeight];
            for (int y = 0; y < newHeight; y++)
            {
                int y0 = y * height / newHeight;
                int y1 = Math.Max(y0 + 1, (y + 1) * height / newHeight);
                for (int x = 0; x < newWidth; x++)
                {
                    int x0 = x * width / newWidth;
                    int x1 = Math.Max(x0 + 1, (x + 1) * width / newWidth);

                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                    {
                        int row = sy * width;
                        for (int sx = x0; sx < x1; sx++)
                        {
                            Color32 pixel = source[row + sx];
                            r += pixel.r;
                            g += pixel.g;
                            b += pixel.b;
                            count++;
                        }
                    }
                    result[y * newWidth + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }

            Texture2D texture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(result);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Turns a picture into a JPEG small enough for the server, lowering the quality until it fits.
        /// </summary>
        /// <returns>The JPEG, or null if it couldn't be made small enough</returns>
        private static byte[] encode(Texture2D texture)
        {
            foreach (int quality in JpegQualities)
            {
                byte[] jpeg = UnityImaging.EncodeToJpg(texture, quality);
                if (jpeg != null && jpeg.Length > 0 && jpeg.Length <= ScreenshotImage.MAX_BYTES) return jpeg;
            }
            return null;
        }

        /// <summary>
        /// Sends a screenshot to the server. Once it has it, the message with the link goes out (see
        /// <see cref="UploadAnswered"/>).
        /// </summary>
        /// <param name="jpeg">The screenshot</param>
        /// <param name="caption">What to say with it</param>
        /// <param name="recipientUuid">Who to send it to as a private message, or null for the chat</param>
        public static void Share(byte[] jpeg, string caption, string recipientUuid = null)
        {
            string token = Guid.NewGuid().ToString();
            uploading[token] = new PendingShare { Jpeg = jpeg, Caption = caption ?? "", RecipientUuid = recipientUuid };
            if (!Client.Instance.UploadScreenshot(token, jpeg))
            {
                uploading.Remove(token);
                Messages.Message("Phinix_screenshot_shareFailed".Translate("Phinix_screenshot_unavailable".Translate()), MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        /// <summary>
        /// Handles the server's answer to one of our screenshots: posts the message with its link (in the chat, or to
        /// the player it was for), or says why it wasn't kept. Main thread only.
        /// </summary>
        public static void UploadAnswered(UploadScreenshotResponsePacket answer)
        {
            if (!uploading.TryGetValue(answer.Token, out PendingShare share)) return;
            uploading.Remove(answer.Token);

            if (!answer.Success)
            {
                Messages.Message("Phinix_screenshot_shareFailed".Translate(answer.FailureMessage), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // We already have the picture, so opening our own link doesn't fetch it again
            remember(answer.ScreenshotId, new Entry
            {
                Found = true,
                OwnerUuid = Client.Instance.Uuid,
                Jpeg = share.Jpeg,
                ExpiresTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Client.Instance.ScreenshotLifetimeMinutes * 60L
            });

            string caption = share.Caption.Trim();
            string text = caption.Length > 0 ? caption + " " + Tag(answer.ScreenshotId) : Tag(answer.ScreenshotId);
            if (share.RecipientUuid != null) Client.Instance.SendDirectMessage(share.RecipientUuid, text);
            else Client.Instance.SendMessage(text);
        }

        /// <summary>
        /// Opens a screenshot from a chat link, fetching it first if we don't have it.
        /// </summary>
        /// <param name="screenshotId">The screenshot's ID</param>
        public static void Open(string screenshotId)
        {
            bool requested = false;
            if (!TryGet(screenshotId, out _)) requested = Client.Instance.RequestScreenshot(screenshotId);
            Find.WindowStack.Add(new ScreenshotViewerWindow(screenshotId, requested));
        }

        /// <summary>
        /// Gets a screenshot we have, or one the server has said is gone.
        /// </summary>
        public static bool TryGet(string screenshotId, out Entry entry)
        {
            lock (cacheLock)
            {
                return cache.TryGetValue(screenshotId, out entry);
            }
        }

        /// <summary>
        /// Stores a screenshot the server sent. Called on the network thread. Anything that isn't a JPEG of a sensible size
        /// is treated as gone, so it's never decoded.
        /// </summary>
        public static void Received(ScreenshotPacket packet)
        {
            byte[] jpeg = packet.Found ? packet.Jpeg.ToByteArray() : null;
            bool usable = jpeg != null && ScreenshotImage.IsAcceptableJpeg(jpeg, out _, out _);
            remember(packet.ScreenshotId, new Entry
            {
                Found = usable,
                OwnerUuid = packet.OwnerUuid,
                Jpeg = usable ? jpeg : null,
                ExpiresTime = packet.ExpiresTime
            });
        }

        /// <summary>
        /// Adds a screenshot to <see cref="cache"/>, letting the oldest go if it's full.
        /// </summary>
        private static void remember(string screenshotId, Entry entry)
        {
            if (string.IsNullOrEmpty(screenshotId)) return;
            lock (cacheLock)
            {
                cacheOrder.Remove(screenshotId);
                cacheOrder.Add(screenshotId);
                cache[screenshotId] = entry;
                while (cacheOrder.Count > CACHE_SIZE)
                {
                    cache.Remove(cacheOrder[0]);
                    cacheOrder.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Destroys a texture we made, if there is one.
        /// </summary>
        public static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        /// <summary>
        /// The few Unity functions for reading the screen and converting pictures. They live in Unity modules the mod
        /// isn't built against (and in Waylith's stand-in for Unity on 1.7), so they're looked up by name once.
        /// </summary>
        public static class UnityImaging
        {
            private static readonly MethodInfo captureScreen = method("UnityEngine.ScreenCapture", "UnityEngine.ScreenCaptureModule", "CaptureScreenshotAsTexture", new Type[0]);
            private static readonly MethodInfo encodeToJpg = method("UnityEngine.ImageConversion", "UnityEngine.ImageConversionModule", "EncodeToJPG", new[] { typeof(Texture2D), typeof(int) });
            private static readonly MethodInfo loadImage = method("UnityEngine.ImageConversion", "UnityEngine.ImageConversionModule", "LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

            /// <summary>
            /// Whether all of them were found.
            /// </summary>
            public static bool Ready => captureScreen != null && encodeToJpg != null && loadImage != null;

            /// <summary>
            /// Reads the screen. Only works at the end of a drawn frame (after <see cref="WaitForEndOfFrame"/>).
            /// </summary>
            public static Texture2D CaptureScreen() => (Texture2D)captureScreen.Invoke(null, new object[0]);

            /// <summary>
            /// Encodes a texture as a JPEG.
            /// </summary>
            public static byte[] EncodeToJpg(Texture2D texture, int quality) => (byte[])encodeToJpg.Invoke(null, new object[] { texture, quality });

            /// <summary>
            /// Decodes a PNG or JPEG into a texture, resizing it to fit.
            /// </summary>
            public static bool LoadImage(Texture2D texture, byte[] data) => (bool)loadImage.Invoke(null, new object[] { texture, data });

            private static MethodInfo method(string typeName, string unityModule, string methodName, Type[] parameters)
            {
                try
                {
                    Type type = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(assembly => assembly.GetType(typeName, false))
                        .FirstOrDefault(candidate => candidate != null)
                        // Unity only loads a module once something uses it, so ask for it by name if it isn't loaded yet
                        ?? Type.GetType(typeName + ", " + unityModule, false);
                    MethodInfo found = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
                    if (found == null) Log.Warning(string.Format("Phinix couldn't find {0}.{1}, so screenshots can't be shared", typeName, methodName));
                    return found;
                }
                catch (Exception e)
                {
                    Log.Warning(string.Format("Phinix couldn't look up {0}.{1}: {2}", typeName, methodName, e.Message));
                    return null;
                }
            }
        }
    }
}
