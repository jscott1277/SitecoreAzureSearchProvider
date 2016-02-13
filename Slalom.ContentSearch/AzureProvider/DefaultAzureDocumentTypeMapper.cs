using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.ContentSearch.AzureProvider
{
    public class DefaultAzureDocumentTypeMapper : DefaultDocumentMapper<Document>
    {
        [Obsolete]
        protected override void ReadDocumentFields<TElement>(Document document, IEnumerable<string> fieldNames, DocumentTypeMapInfo documentTypeMapInfo, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors, TElement result)
        {
        }

        protected override IEnumerable<string> GetDocumentFieldNames(Document document)
        {
            return (IEnumerable<string>)null;
        }

        protected override IDictionary<string, object> ReadDocumentFields(Document document, IEnumerable<string> fieldNames, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors)
        {
            Assert.ArgumentNotNull((object)document, "document");
            IDictionary<string, object> seed = (IDictionary<string, object>)new Dictionary<string, object>();
            if (fieldNames != null)
            {
                foreach (string name in fieldNames)
                {
                    var fields = document.Where(f => f.Key == name);
                    if (fields != null && fields.Any())
                    {
                        if (fields.Count() > 1)
                        {
                            //TODO: what should f.ToString() be?
                            string[] strArray = Enumerable.ToArray<string>(Enumerable.Select<Field, string>((IEnumerable<Field>)fields, (Func<Field, string>)(f => f.ToString())));
                            seed[name] = (object)strArray;
                        }
                        else if (fields.Count() == 1)
                        {
                            seed[fields.FirstOrDefault().Key] = (object)fields.FirstOrDefault().Value;
                        }
                    }
                }
            }
            else
            {
                foreach (IGrouping<string, Field> grouping in Enumerable.GroupBy<Field, string>((IEnumerable<Field>)document.ToList(), (Func<Field, string>)(f => f.Name)))
                {
                    if (Enumerable.Count<Field>((IEnumerable<Field>)grouping) > 1)
                    {
                        //TODO:  What should f.ToString() be?
                        string[] strArray = Enumerable.ToArray<string>(Enumerable.Select<Field, string>((IEnumerable<Field>)grouping, (Func<Field, string>)(f => f.ToString())));
                        seed[grouping.Key] = (object)strArray;
                    }
                    else
                    {
                        //TODO:  What should .ToString() be
                        seed[grouping.Key] = (object)Enumerable.First<Field>((IEnumerable<Field>)grouping).ToString();
                    }
                }
            }
            if (virtualFieldProcessors != null)
                seed = Enumerable.Aggregate<IFieldQueryTranslator, IDictionary<string, object>>(virtualFieldProcessors, seed, (Func<IDictionary<string, object>, IFieldQueryTranslator, IDictionary<string, object>>)((current, processor) => processor.TranslateFieldResult(current, (FieldNameTranslator)this.index.FieldNameTranslator)));
            return seed;
        }
    }
}
