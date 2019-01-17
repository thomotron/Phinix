using Verse;

namespace PhinixClient
{
    public class UnknownItem : Thing
    {
        public override string Label => generateLabel();

        public string OriginalLabel;

        private string generateLabel()
        {
            // Return the original item's label in brackets if it is set, otherwise the default label
            return OriginalLabel != null
                ? string.Format("{0} ({1})", def.label, OriginalLabel)
                : def.label;
        }
    }
}