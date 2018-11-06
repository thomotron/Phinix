using System.Runtime.Serialization;

namespace Connections
{
    /// <summary>
    /// A generic class that sets a standard format for client-server communication.
    /// </summary>
    public abstract class Packet : IExtensibleDataObject
    {
        // This will hold any excess data that doesn't fit in the current version of this class
        public ExtensionDataObject ExtensionData { get; set; }
    }
}