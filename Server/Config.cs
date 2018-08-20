using System;
using System.IO;
using System.Net;
using System.Xml.Serialization;

namespace PhinixServer
{
    /// <summary>
    /// Server configuration class able to read from and write to a configuration file.
    /// </summary>
    [Serializable]
    public class Config
    {
        [XmlIgnore]
        public IPAddress Address = IPAddress.Any;
        public string AddressString = ""; // TODO: Have this field renamed to 'IP' or something
        public int Port = 16180;

        public string LogPath = "server.log";
        public Verbosity DisplayVerbosity = Verbosity.INFO;
        public Verbosity LogVerbosity = Verbosity.INFO;

        public static Config Load(string filePath)
        {
            // Give a fresh new config if the given file doesn't exist
            if (!File.Exists(filePath)) return new Config();

            Config result;

            using (FileStream stream = File.OpenRead(filePath))
            {
                result = new XmlSerializer(typeof(Config)).Deserialize(stream) as Config;
            }

            result.PostDeserialisation();

            return result;
        }

        public void Save(string filePath)
        {
            PreSerialisation();

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                new XmlSerializer(typeof(Config)).Serialize(stream, this);
            }
        }
        
        private void PreSerialisation()
        {
            this.AddressString = Address.ToString();
        }
        
        private void PostDeserialisation()
        {
            if (!IPAddress.TryParse(AddressString, out Address))
            {
                throw new ConfigItemDeserialisationException(typeof(string), typeof(IPAddress), nameof(AddressString));
            }
        }
    }
}
