// Decompiled with JetBrains decompiler
// Type: Sitecore.ContentSearch.LuceneProvider.LuceneFieldNameTranslator
// Assembly: Sitecore.ContentSearch.LuceneProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B719F69-8579-4D19-90A9-D118C717CF98
// Assembly location: C:\Users\jscott\Downloads\Sitecore 8.1 rev. 151207\Sitecore 8.1 rev. 151207\SearchDlls\Sitecore.ContentSearch.LuceneProvider.dll

using Sitecore.ContentSearch;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Slalom.ContentSearch.AzureProvider
{
    public class AzureFieldNameTranslator : AbstractFieldNameTranslator
    {
        private IAzureProviderIndex index;

        public AzureFieldNameTranslator(IAzureProviderIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            this.index = index;
        }

        public override string GetIndexFieldName(MemberInfo member)
        {
            IIndexFieldNameFormatterAttribute formatterAttribute = this.GetIndexFieldNameFormatterAttribute(member);
            if (formatterAttribute != null)
                return this.GetIndexFieldName(formatterAttribute.GetIndexFieldName(member.Name));
            return this.GetIndexFieldName(member.Name);
        }

        public override string GetIndexFieldName(string fieldName)
        {
            fieldName = fieldName.Replace(" ", "_");

            if (fieldName.StartsWith("_"))
            {
                fieldName = "s" + fieldName;
            }

            return fieldName.ToLowerInvariant();
        }

        public override string GetIndexFieldName(string fieldName, Type returnType)
        {
            return this.GetIndexFieldName(fieldName);
        }

        public override Dictionary<string, List<string>> MapDocumentFieldsToType(Type type, MappingTargetType target, IEnumerable<string> documentFieldNames)
        {
            if (target == MappingTargetType.Indexer)
                return this.MapDocumentFieldsToTypeIndexer(type, documentFieldNames);
            if (target == MappingTargetType.Properties)
                return this.MapDocumentFieldsToTypeProperties(type, documentFieldNames);
            throw new ArgumentException("Invalid mapping target type: " + (object)target, "target");
        }

        public override IEnumerable<string> GetTypeFieldNames(string fieldName)
        {
            yield return fieldName;
            if (!fieldName.StartsWith("_"))
                yield return Regex.Replace(fieldName, "(?<!\\.)_", " ").Trim();
        }

        private Dictionary<string, List<string>> MapDocumentFieldsToTypeIndexer(Type type, IEnumerable<string> documentFieldNames)
        {
            Dictionary<string, List<string>> dictionary = Enumerable.ToDictionary<string, string, List<string>>(documentFieldNames, (Func<string, string>)(f => f), (Func<string, List<string>>)(f => Enumerable.ToList<string>(this.GetTypeFieldNames(f))));
            foreach (PropertyInfo propertyInfo in this.GetProperties(type))
            {
                IIndexFieldNameFormatterAttribute formatterAttribute = this.GetIndexFieldNameFormatterAttribute((MemberInfo)propertyInfo);
                if (formatterAttribute != null)
                {
                    string indexFieldName = this.GetIndexFieldName(formatterAttribute.GetIndexFieldName(propertyInfo.Name));
                    if (dictionary.ContainsKey(indexFieldName))
                        dictionary[indexFieldName].Add(formatterAttribute.GetTypeFieldName(propertyInfo.Name));
                }
            }
            return dictionary;
        }

        private Dictionary<string, List<string>> MapDocumentFieldsToTypeProperties(Type type, IEnumerable<string> documentFieldNames)
        {
            Dictionary<string, List<string>> dictionary = Enumerable.ToDictionary<string, string, List<string>>(documentFieldNames, (Func<string, string>)(f => f), (Func<string, List<string>>)(f => Enumerable.ToList<string>(this.GetTypeFieldNames(f))));
            Dictionary<string, List<string>> mappedProperties = new Dictionary<string, List<string>>();
            this.ProcessProperties(type, (IDictionary<string, List<string>>)dictionary, ref mappedProperties, "", "");
            return mappedProperties;
        }
    }
}
