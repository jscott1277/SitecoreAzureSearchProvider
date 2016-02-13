using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slalom.ContentSearch.AzureProvider
{
    public class DefaultAzureDocumentTypeMapper : DefaultDocumentMapper<Document>
    {
        [Obsolete]
        protected override void ReadDocumentFields<TElement>(Document document, IEnumerable<string> fieldNames, DocumentTypeMapInfo documentTypeMapInfo, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors, TElement result)
        {
        }

        protected override IEnumerable<string> GetDocumentFieldNames(Document document)
        {
            return document.Keys;
        }

        protected override IDictionary<string, object> ReadDocumentFields(Document document, IEnumerable<string> fieldNames, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors)
        {
            Assert.ArgumentNotNull(document, "document");
            IDictionary<string, object> dictionary = new Dictionary<string, object>();
            if (fieldNames != null)
            {
                foreach (var fieldName in fieldNames)
                {
                    object val;
                    document.TryGetValue(fieldName, out val);
                    dictionary.Add(fieldName, val);
                }
            }
            else
            {
                var keys = document.Keys;
                foreach (var key in keys)
                {
                    object val;
                    document.TryGetValue(key, out val);
                    dictionary.Add(key, val);
                }
            }
        
            if (virtualFieldProcessors != null)
            {
                dictionary = virtualFieldProcessors.Aggregate(dictionary, (IDictionary<string, object> current, IFieldQueryTranslator processor) => processor.TranslateFieldResult(current, this.index.FieldNameTranslator));
            }
            return dictionary;
        }
    }
}
