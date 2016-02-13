using Azure.ContentSearch.AzureProvider;
using Microsoft.Azure.Search.Models;
using Sitecore;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Converters;
using System;
using System.Text;

namespace Azure.ContentSearch.AzureProvider
{
    public static class AzureFieldBuilder
    {
        public static Field CreateField(string name, object value, AzureSearchFieldConfiguration fieldConfiguration, IIndexFieldStorageValueFormatter indexFieldStorageValueFormatter)
        {
            if (value == null)
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field {0} - value null", (object)name)));
                return (Field)null;
            }
            if (fieldConfiguration == null)
                throw new ArgumentNullException("fieldConfiguration");
            if (VerboseLogging.Enabled)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat("Field: {0}" + Environment.NewLine, (object)name);
                stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, (object)value.GetType());
                stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, value);
                //stringBuilder.AppendFormat(" - fieldConfiguration analyzer: {0}" + Environment.NewLine, fieldConfiguration.Analyzer != null ? (object)fieldConfiguration.Analyzer.GetType().ToString() : (object)"NULL");
                stringBuilder.AppendFormat(" - fieldConfiguration boost: {0}" + Environment.NewLine, (object)fieldConfiguration.Boost);
                stringBuilder.AppendFormat(" - fieldConfiguration fieldID: {0}" + Environment.NewLine, (object)fieldConfiguration.FieldID);
                stringBuilder.AppendFormat(" - fieldConfiguration FieldName: {0}" + Environment.NewLine, (object)fieldConfiguration.FieldName);
                stringBuilder.AppendFormat(" - fieldConfiguration FieldTypeName: {0}" + Environment.NewLine, (object)fieldConfiguration.FieldTypeName);
                //stringBuilder.AppendFormat(" - fieldConfiguration IndexType: {0}" + Environment.NewLine, (object)fieldConfiguration.IndexType);
                //stringBuilder.AppendFormat(" - fieldConfiguration StorageType: {0}" + Environment.NewLine, (object)fieldConfiguration.StorageType);
                //stringBuilder.AppendFormat(" - fieldConfiguration VectorType: {0}" + Environment.NewLine, (object)fieldConfiguration.VectorType);
                stringBuilder.AppendFormat(" - fieldConfiguration Type: {0}" + Environment.NewLine, (object)fieldConfiguration.Type);
                VerboseLogging.CrawlingLogDebug(new Func<string>(((object)stringBuilder).ToString));
            }
            if (AzureFieldBuilder.IsNumericField(fieldConfiguration.Type))
            {
                if (value is string && string.IsNullOrEmpty((string)value))
                {
                    VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field {0} - value or empty null", (object)name)));
                    return (Field)null;
                }
                long result;
                if (long.TryParse(value.ToString(), out result))
                {
                    var numericField = new Field(name, DataType.Int64);
                    //TODO: How to set value?
                    //numericField.((long)Convert.ChangeType(value, typeof(long)));
                    return numericField;
                }
            }
            if (AzureFieldBuilder.IsFloatingPointField(fieldConfiguration.Type))
            {
                if (value is string && string.IsNullOrEmpty((string)value))
                {
                    VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field {0} - value or empty null", (object)name)));
                    return (Field)null;
                }
                var numericField = new Field(name, DataType.Double);
                //numericField.SetDoubleValue((double)Convert.ChangeType(value, typeof(double), (IFormatProvider)LanguageUtil.GetCultureInfo()));
                return (Field)numericField;
            }
            string value_Renamed = indexFieldStorageValueFormatter.FormatValueForIndexStorage(value, name).ToString();
            if (VerboseLogging.Enabled)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat("Field: {0}" + Environment.NewLine, (object)name);
                stringBuilder.AppendFormat(" - formattedValue: {0}" + Environment.NewLine, (object)value_Renamed);
                VerboseLogging.CrawlingLogDebug(new Func<string>(((object)stringBuilder).ToString));
            }
            //TODO: How to set field value?
            return (Field)new Field(name, DataType.String /*, value_Renamed*/);
        }

        public static bool IsFloatingPointField(Type type)
        {
            return type.IsAssignableFrom(typeof(double)) || type.IsAssignableFrom(typeof(float));
        }

        public static bool IsNumericField(Type type)
        {
            if (type == (Type)null)
                throw new ArgumentNullException("type");
            return type.IsAssignableFrom(typeof(int)) || type.IsAssignableFrom(typeof(uint)) || (type.IsAssignableFrom(typeof(short)) || type.IsAssignableFrom(typeof(ushort))) || (type.IsAssignableFrom(typeof(long)) || type.IsAssignableFrom(typeof(ulong)) || (type.IsAssignableFrom(typeof(byte)) || type.IsAssignableFrom(typeof(sbyte))));
        }
    }
}
