// Decompiled with JetBrains decompiler
// Type: Sitecore.ContentSearch.LuceneProvider.LuceneSearchFieldConfiguration
// Assembly: Sitecore.ContentSearch.LuceneProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B719F69-8579-4D19-90A9-D118C717CF98
// Assembly location: C:\inetpub\wwwroot\sc81rev151207\Website\bin\Sitecore.ContentSearch.LuceneProvider.dll

using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureSearchFieldConfiguration : AbstractSearchFieldConfiguration
    {
        private Type type;
        private Analyzer analyzer;

        public float Boost { get; set; }

        public bool IsKey { get; set; }

        public bool IsSearchable { get; set; }

        public bool IsFilterable { get; set; }

        public bool IsSortable { get; set; }

        public bool IsFacetable { get; set; }

        public bool IsRetrievable { get; set; }

        public Type Type
        {
            get
            {
                return this.type ?? typeof(string);
            }
            set
            {
                this.type = value;
            }
        }

        public AzureSearchFieldConfiguration()
        {
            this.Boost = 1f;
        }

        public AzureSearchFieldConfiguration(string storageType, string indexType, string vectorType, string boost)
        {
            this.Boost = this.ParseBoost(boost);
        }

        public AzureSearchFieldConfiguration(string storageType, string indexType, string vectorType, float boost)
        {
            this.Boost = boost;
        }

        public AzureSearchFieldConfiguration(string name, string fieldTypeName, IDictionary<string, string> attributes, XmlNode configNode)
          : this(name, (string)null, fieldTypeName, attributes, configNode)
        {
        }

        public AzureSearchFieldConfiguration(string name, string fieldID, string fieldTypeName, IDictionary<string, string> attributes, XmlNode configNode)
          : base(name, fieldID, fieldTypeName, attributes, configNode)
        {
            foreach (KeyValuePair<string, string> keyValuePair in (IEnumerable<KeyValuePair<string, string>>)attributes)
            {
                switch (keyValuePair.Key)
                {
                    case "IsRetrievable":
                        this.SetBool(this.IsRetrievable, keyValuePair.Value);
                        continue;
                    case "IsFacetable":
                        this.SetBool(this.IsFacetable, keyValuePair.Value);
                        continue;
                    case "IsSortable":
                        this.SetBool(this.IsFacetable, keyValuePair.Value);
                        continue;
                    case "IsFilterable":
                        this.SetBool(this.IsFilterable, keyValuePair.Value);
                        continue;
                    case "IsSearchable":
                        this.SetBool(this.IsSearchable, keyValuePair.Value);
                        continue;
                    case "IsKey":
                        this.SetBool(this.IsKey, keyValuePair.Value);
                        continue;
                    case "type":
                        this.SetType(keyValuePair.Value);
                        continue;
                    case "nullValue":
                        this.SetNullPlaceholderValue(keyValuePair.Value);
                        continue;
                    case "emptyString":
                        this.SetEmptyStringPlaceholderValue(keyValuePair.Value);
                        continue;
                    case "boost":
                        this.Boost = this.ParseBoost(keyValuePair.Value);
                        continue;
                    default:
                        continue;
                }
            }
            if (configNode == null)
                return;
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
            if (!string.IsNullOrEmpty(type))
                this.Type = Type.GetType(type);
            else
                this.Type = typeof(string);
        }

        private void SetBool(bool prop, string boolVal)
        {
            var _isTrue = false;
            bool.TryParse(boolVal, out _isTrue);
            prop = _isTrue;
        }

        protected float ParseBoost(string value)
        {
            float result;
            if (!float.TryParse(value, out result))
                return 1f;
            return result;
        }
    }
}
