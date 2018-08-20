using System;

namespace PhinixServer
{
    public class ConfigItemDeserialisationException : Exception
    {
        public override string Message => string.Format("Failed to convert type {0} to type {1} when deserialising config item {2}", SerialisedType, DeserialisedType, ItemName);

        public readonly Type SerialisedType;
        public readonly Type DeserialisedType;
        public readonly string ItemName;

        public ConfigItemDeserialisationException(Type serialisedType, Type deserialisedType, string itemName)
        {
            this.SerialisedType = serialisedType;
            this.DeserialisedType = deserialisedType;
            this.ItemName = itemName;
        }
    }
}