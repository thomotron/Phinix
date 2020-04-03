namespace Utils
{
    /// <summary>
    /// Interface for persistent modules that need to save and load data when starting and stopping.
    /// </summary>
    public interface IPersistent
    {
        /// <summary>
        /// Saves the module state to a file at the given path.
        /// </summary>
        /// <param name="path">State file path</param>
        void Save(string path);

        /// <summary>
        /// Loads the module state from a file at the given path.
        /// </summary>
        /// <param name="path">State file path</param>
        void Load(string path);
    }
}