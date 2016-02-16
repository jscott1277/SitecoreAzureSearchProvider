using Jarstan.ContentSearch.AzureProvider;
using Microsoft.Azure.Search.Models;
using Sitecore;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Converters;
using System;
using System.Text;
using System.Collections.Generic;

namespace Jarstan.ContentSearch.AzureProvider
{
    public static class AzureFieldBuilder
    {
        public static AzureField CreateField(string name, object value, AzureSearchFieldConfiguration fieldConfiguration, IIndexFieldStorageValueFormatter indexFieldStorageValueFormatter)
        {
            if (value == null)
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field {0} - value null", (object)name)));
                return null;
            }
            if (fieldConfiguration == null)
                throw new ArgumentNullException("fieldConfiguration");
            if (VerboseLogging.Enabled)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat("Field: {0}" + Environment.NewLine, name);
                stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, value.GetType());
                stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, value);
                //stringBuilder.AppendFormat(" - fieldConfiguration analyzer: {0}" + Environment.NewLine, fieldConfiguration.Analyzer != null ? (object)fieldConfiguration.Analyzer.GetType().ToString() : (object)"NULL");
                stringBuilder.AppendFormat(" - fieldConfiguration boost: {0}" + Environment.NewLine, fieldConfiguration.Boost);
                stringBuilder.AppendFormat(" - fieldConfiguration fieldID: {0}" + Environment.NewLine, fieldConfiguration.FieldID);
                stringBuilder.AppendFormat(" - fieldConfiguration FieldName: {0}" + Environment.NewLine, fieldConfiguration.FieldName);
                stringBuilder.AppendFormat(" - fieldConfiguration FieldTypeName: {0}" + Environment.NewLine, fieldConfiguration.FieldTypeName);
                //stringBuilder.AppendFormat(" - fieldConfiguration IndexType: {0}" + Environment.NewLine, (object)fieldConfiguration.IndexType);
                //stringBuilder.AppendFormat(" - fieldConfiguration StorageType: {0}" + Environment.NewLine, (object)fieldConfiguration.StorageType);
                //stringBuilder.AppendFormat(" - fieldConfiguration VectorType: {0}" + Environment.NewLine, (object)fieldConfiguration.VectorType);
                stringBuilder.AppendFormat(" - fieldConfiguration Type: {0}" + Environment.NewLine, fieldConfiguration.Type);
                VerboseLogging.CrawlingLogDebug(new Func<string>(((object)stringBuilder).ToString));
            }

            return new AzureField(name, value, BuildField(name, fieldConfiguration), fieldConfiguration.Type);
        }

        private static Field BuildField(string name, AzureSearchFieldConfiguration fieldConfiguration)
        {
            var fld = new Field();
            fieldConfiguration.SetValues();
            fld.IsFacetable = fieldConfiguration.IsFacetable;
            fld.IsFilterable = fieldConfiguration.IsFilterable;
            fld.IsKey = fieldConfiguration.IsKey;
            fld.IsRetrievable = fieldConfiguration.IsRetrievable;
            fld.IsSearchable = fieldConfiguration.IsSearchable;
            fld.IsSortable = fieldConfiguration.IsSortable;
            fld.Name = name;
            fld.Type = fieldConfiguration.Type ?? DataType.String;
            return fld;
        }

        public static Field BuildField(string name, object val, IAzureProviderIndex index)
        {
            var fld = index.AzureConfiguration.FieldMap.GetFieldConfiguration(name) as AzureSearchFieldConfiguration;
            if (fld != null)
            {
                return BuildField(name, fld);
            }

            var dataType = DataType.String;
            dataType = AzureDocumentBuilder.GetDataType(val);
            var searchable = dataType == DataType.String;

            //Build new Field
            var field = new Field()
            {
                IsFacetable = false,
                IsFilterable = false,
                IsKey = false,
                IsRetrievable = true,
                IsSearchable = searchable,
                IsSortable = true,
                Name = name,
                Type = dataType
            };

            return field;
        }

        public static bool IsBool(string boolVal, bool defaultValue)
        {
            var _isTrue = false;
            if (bool.TryParse(boolVal, out _isTrue))
            {
                return _isTrue;
            }
            else
            {
                return defaultValue;
            }
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
