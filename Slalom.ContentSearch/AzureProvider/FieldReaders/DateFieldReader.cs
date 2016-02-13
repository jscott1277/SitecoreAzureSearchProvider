using Sitecore.ContentSearch;
using Sitecore.ContentSearch.FieldReaders;
using Sitecore.Data.Fields;
using System;

namespace Azure.ContentSearch.AzureProvider.FieldReaders
{
    public class DateFieldReader : FieldReader
    {
        public override object GetFieldValue(IIndexableDataField field)
        {
            Field field1 = (Field)(field as SitecoreItemDataField);
            if (field1 != null)
            {
                if (string.IsNullOrEmpty(field1.Value))
                    return (object)null;
                if (FieldTypeManager.GetField(field1) is DateField)
                {
                    DateField dateField = new DateField(field1);
                    if (dateField.DateTime > DateTime.MinValue)
                        return (object)dateField.DateTime;
                }
            }
            else if (field.Value is DateTime)
                return (object)(DateTime)field.Value;
            return (object)null;
        }
    }
}
