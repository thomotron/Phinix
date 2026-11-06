namespace Hermes
{
    /// <summary>
    /// Limits for shared screenshots, and a check that some bytes really are a JPEG of a sensible size. Both the server
    /// (before keeping a picture) and the client (before decoding one) use it.
    /// </summary>
    public static class ScreenshotImage
    {
        /// <summary>
        /// Largest picture, in bytes. Phime shrinks its screenshots to well under this.
        /// </summary>
        public const int MAX_BYTES = 512 * 1024;

        /// <summary>
        /// Widest and tallest picture, in pixels. Phime shrinks screenshots to <see cref="TARGET_WIDTH"/> wide, so this
        /// only stops a doctored picture claiming to be enormous, which would use up memory when decoded.
        /// </summary>
        public const int MAX_SIDE = 2048;

        /// <summary>
        /// How wide Phime makes its screenshots before sending them.
        /// </summary>
        public const int TARGET_WIDTH = 1280;

        /// <summary>
        /// Checks that <paramref name="data"/> is a JPEG no bigger than <see cref="MAX_BYTES"/> and
        /// <see cref="MAX_SIDE"/>, by reading its header. Nothing is decoded.
        /// </summary>
        /// <param name="data">The picture</param>
        /// <param name="width">Its width in pixels</param>
        /// <param name="height">Its height in pixels</param>
        /// <returns>Whether it's a JPEG within the limits</returns>
        public static bool IsAcceptableJpeg(byte[] data, out int width, out int height)
        {
            width = height = 0;
            if (data == null || data.Length < 4 || data.Length > MAX_BYTES) return false;
            if (data[0] != 0xFF || data[1] != 0xD8) return false;  // Every JPEG starts with FF D8

            // The rest is a list of blocks, each starting with FF and a marker byte. The size is in the "start of frame"
            // block, which comes before the picture data itself ("start of scan", DA)
            int i = 2;
            while (i + 3 < data.Length)
            {
                if (data[i] != 0xFF) return false;
                byte marker = data[i + 1];
                if (marker == 0xFF) { i++; continue; }  // Padding
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }  // No length
                if (marker == 0xDA || marker == 0xD9) return false;  // Picture data or the end, with no size before it

                int length = (data[i + 2] << 8) | data[i + 3];
                if (length < 2) return false;

                bool startOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (startOfFrame)
                {
                    if (i + 8 >= data.Length) return false;
                    height = (data[i + 5] << 8) | data[i + 6];
                    width = (data[i + 7] << 8) | data[i + 8];
                    return width > 0 && height > 0 && width <= MAX_SIDE && height <= MAX_SIDE;
                }

                i += 2 + length;
            }

            return false;
        }
    }
}
