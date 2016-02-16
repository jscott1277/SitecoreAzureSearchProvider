using Sitecore.ContentSearch;
using Sitecore.ContentSearch.FieldReaders;
using Sitecore.Diagnostics;

namespace Jarstan.ContentSearch.AzureProvider.FieldReaders
{
    public class NumericFieldReader : FieldReader
    {
        public override object GetFieldValue(IIndexableDataField field)
        {
            Assert.ArgumentNotNull((object)field, "field");
            if (field.Value is string)
            {
                string s = (string)field.Value;
                long result;
                if (!string.IsNullOrEmpty(s) && long.TryParse(s, out result))
                    return (object)result;
            }
            else if (field.Value is long || field.Value is ulong || (field.Value is short || field.Value is ushort) || (field.Value is int || field.Value is uint || field.Value is byte))
                return field.Value;
            return (object)null;
        }
    }
}
