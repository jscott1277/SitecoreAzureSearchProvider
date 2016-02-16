using Microsoft.Azure.Search.Models;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureSearchFieldConfiguration : AbstractSearchFieldConfiguration
    {
        //private Analyzer analyzer;

        public float Boost { get; set; }

        public bool IsKey { get; set; }

        public bool IsSearchable { get; set; }

        public bool IsFilterable { get; set; }

        public bool IsSortable { get; set; }

        public bool IsFacetable { get; set; }

        public bool IsRetrievable { get; set; }

        public DataType Type { get; set; }

        public AzureSearchFieldConfiguration()
        {
            this.Boost = 1f;
        }

        public AzureSearchFieldConfiguration(string storageType, string indexType, string vectorType, float boost)
        {
            this.Boost = boost;
        }

        public AzureSearchFieldConfiguration(string name, string fieldID, string fieldTypeName, IDictionary<string, string> attributes, XmlNode configNode)
          : base(name, fieldID, fieldTypeName, attributes, configNode)
        {
            foreach (var keyValuePair in attributes)
            {
                switch (keyValuePair.Key)
                {
                    case "IsRetrievable":
                        SetBool(IsRetrievable, keyValuePair.Value);
                        break;
                    case "IsFacetable":
                        SetBool(IsFacetable, keyValuePair.Value);
                        break;
                    case "IsSortable":
                        SetBool(IsFacetable, keyValuePair.Value);
                        break;
                    case "IsFilterable":
                        SetBool(IsFilterable, keyValuePair.Value);
                        break;
                    case "IsSearchable":
                        SetBool(IsSearchable, keyValuePair.Value);
                        break;
                    case "IsKey":
                        SetBool(IsKey, keyValuePair.Value);
                        break;
                    case "type":
                        SetType(keyValuePair.Value);
                        break;
                    case "nullValue":
                        SetNullPlaceholderValue(keyValuePair.Value);
                        break;
                    case "emptyString":
                        SetEmptyStringPlaceholderValue(keyValuePair.Value);
                        break;
                    case "boost":
                        Boost = ParseBoost(keyValuePair.Value);
                        break;
                    default:
                        break;
                }
            }
        }

        public void SetValues()
        {
            foreach (var keyValuePair in base.Attributes)
            {
                switch (keyValuePair.Key)
                {
                    case "IsRetrievable":
                        this.IsRetrievable = GetBool(keyValuePair.Value, true);
                        break;
                    case "IsFacetable":
                        this.IsFacetable = GetBool(keyValuePair.Value);
                        break;
                    case "IsSortable":
                        IsSortable = GetBool(keyValuePair.Value);
                        break;
                    case "IsFilterable":
                        IsFilterable = GetBool(keyValuePair.Value);
                        break;
                    case "IsSearchable":
                        IsSearchable = GetBool(keyValuePair.Value, true);
                        break;
                    case "IsKey":
                        IsKey = GetBool(keyValuePair.Value);
                        break;
                    case "type":
                        Type = GetType(keyValuePair.Value);
                        break;
                    case "nullValue":
                        SetNullPlaceholderValue(keyValuePair.Value);
                        break;
                    case "emptyString":
                        SetEmptyStringPlaceholderValue(keyValuePair.Value);
                        break;
                    case "boost":
                        Boost = ParseBoost(keyValuePair.Value);
                        break;
                    default:
                        break;
                }
            }
        }

        public bool GetBool(string boolVal, bool defaultValue = false)
        {
            var _isTrue = false;
            if (bool.TryParse(boolVal, out _isTrue))
            {
                return _isTrue;
            }
            return defaultValue;
        }

        public DataType GetType(string type)
        {
            switch (type)
            {
                case "System.List":
                    return DataType.Collection(DataType.String);
                case "System.Double":
                    return DataType.Double;
                case "System.Boolean":
                    return DataType.Boolean;
                case "System.DateTime":
                    return DataType.DateTimeOffset;
                case "GeographyPoint":
                    return DataType.GeographyPoint;
                case "System.Int32":
                    return DataType.Int32;
                case "System.Int64":
                    return DataType.Int64;
                case "System.String":
                case "System.GUID":
                default:
                    return DataType.String;
            }
        }

        public void SetNullPlaceholderValue(string value)
        {
            if (!string.IsNullOrEmpty(value))
                this.NullValue = value;
            else
                this.NullValue = (string)null;
        }

        public void SetEmptyStringPlaceholderValue(string value)
        {
            if (!string.IsNullOrEmpty(value))
                this.EmptyString = value;
            else
                this.EmptyString = (string)null;
        }

        public void SetType(string type)
        {
            Type = GetType(type);
        }

        public void SetBool(bool prop, string boolVal)
        {
            prop = GetBool(boolVal);
        }

        public float ParseBoost(string value)
        {
            float result;
            if (!float.TryParse(value, out result))
                return 1f;
            return result;
        }
    }
}
