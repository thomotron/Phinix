using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Xml;

namespace PhinixServer
{
    /// <summary>
    /// Server configuration class able to read from and write to a configuration file.
    /// </summary>
    [DataContract]
    public class Config : IExtensibleDataObject
    {
        // This will hold any excess data that doesn't fit in the current version of this class
        public ExtensionDataObject ExtensionData { get; set; }

        [IgnoreDataMember]
        public IPAddress Address = IPAddress.Any;

        [DataMember(Name = "IPAddress")]
        private string addressString = "";

        [DataMember(Name = "Port")]
        public int Port = 16180;

        [DataMember(Name = "LogFile")]
        public string LogPath = "server.log";

        [DataMember(Name = "DisplayVerbosity")]
        public Verbosity DisplayVerbosity = Verbosity.INFO;

        [DataMember(Name = "LogVerbosity")]
        public Verbosity LogVerbosity = Verbosity.INFO;

        public static Config Load(string filePath)
        {
            // Give a fresh new config if the given file doesn't exist
            if (!File.Exists(filePath)) return new Config();

            Config result;

            using (XmlReader reader = XmlReader.Create(filePath))
            {
                result = new DataContractSerializer(typeof(Config)).ReadObject(reader) as Config;
            }

            return result;
        }

        public void Save(string filePath)
        {
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                new DataContractSerializer(typeof(Config)).WriteObject(writer, this);
            }
        }
        
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            this.addressString = Address.ToString();
        }
        
        [OnDeserialized]
        private void OnDeserialised(StreamingContext context)
        {
            if (!IPAddress.TryParse(addressString, out Address))
            {
                throw new ConfigItemDeserialisationException(typeof(string), typeof(IPAddress), nameof(addressString));
            }
        }
    }
}
