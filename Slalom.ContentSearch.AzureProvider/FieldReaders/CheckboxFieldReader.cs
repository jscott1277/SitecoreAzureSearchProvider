using Sitecore.ContentSearch;
using Sitecore.ContentSearch.FieldReaders;
using Sitecore.Data.Fields;

namespace Slalom.ContentSearch.AzureProvider.FieldReaders
{
    public class CheckboxFieldReader : FieldReader
    {
        public override object GetFieldValue(IIndexableDataField field)
        {
            Field field1 = (Field)(field as SitecoreItemDataField);
            if (field1 != null)
            {
                CheckboxField checkboxField = FieldTypeManager.GetField(field1) as CheckboxField;
                return (checkboxField == null ? 0 : (checkboxField.Checked ? 1 : 0));
            }
            if (field.Value is bool)
                return field.Value;
            return field.Value;
        }
    }
}
