using System;
using System.IO;

namespace Utils
{
    /// <summary>
    /// Saves and loads the server's data files so that a crash or power cut in the middle of saving can't wipe them.
    /// </summary>
    /// <remarks>
    /// A save writes the new data to "file.tmp" first, makes sure it has really reached the disk, then swaps it in for
    /// "file" in one step, keeping the version it replaced as "file.bak". So at every moment there's a complete copy on
    /// disk: the old file, the finished .tmp, or both. Loading tries the file, then the .tmp, then the .bak.
    /// </remarks>
    public static class SafeFile
    {
        /// <summary>
        /// Writes data to a file without ever leaving it half-written or missing.
        /// </summary>
        /// <param name="path">File to write</param>
        /// <param name="bytes">Its new contents</param>
        public static void WriteAllBytes(string path, byte[] bytes)
        {
            string tempPath = path + ".tmp";

            // Flush(true) asks Windows to put it on the disk itself, not just in its write cache, before we swap it in
            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(path))
            {
                // One step: the .tmp becomes the file and the old file becomes the .bak
                File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        /// <summary>
        /// Reads and parses the newest good copy of a file: the file itself, else a finished .tmp from a save that was
        /// cut off, else the .bak.
        /// </summary>
        /// <param name="path">File to read</param>
        /// <param name="parse">Turns the file's bytes into the data, throwing if they're not valid</param>
        /// <param name="result">The data, if any copy could be read</param>
        /// <param name="usedPath">Which copy it came from</param>
        /// <returns>
        /// True if a copy was read. False if there are no copies at all (a brand new server).
        /// </returns>
        /// <exception cref="InvalidDataException">Copies exist but none of them can be read. The caller must not start
        /// over with empty data, since saving that would overwrite the only copies of everyone's items</exception>
        public static bool TryRead<T>(string path, Func<byte[], T> parse, out T result, out string usedPath)
        {
            bool anyFound = false;
            foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                anyFound = true;

                try
                {
                    result = parse(File.ReadAllBytes(candidate));
                    usedPath = candidate;
                    return true;
                }
                catch (Exception)
                {
                    // Damaged, try the next copy
                }
            }

            if (anyFound) throw new InvalidDataException(string.Format("Couldn't read {0} or its .tmp/.bak copies. Not starting with an empty one, so nothing gets overwritten. Restore the file from a backup or move it aside.", path));

            result = default(T);
            usedPath = null;
            return false;
        }
    }
}
