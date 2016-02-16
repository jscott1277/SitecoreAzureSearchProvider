using Microsoft.Azure.Search.Models;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Jarstan.ContentSearch.Linq.Azure;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Pipelines.IndexingFilters;
using Sitecore.ContentSearch.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarstan.ContentSearch.AzureProvider
{
    internal struct AzureSearchResults<TElement>
    {
        private readonly AzureSearchContext context;
        private readonly AzureQuery query;
        private readonly DocumentSearchResult searchHits;
        private readonly int startIndex;
        private readonly int endIndex;
        private readonly AzureIndexConfiguration configuration;
        private readonly SelectMethod selectMethod;
        private readonly IEnumerable<IFieldQueryTranslator> virtualFieldProcessors;
        private readonly FieldNameTranslator fieldNameTranslator;
        private readonly IEnumerable<IExecutionContext> executionContexts;
        private readonly IIndexDocumentPropertyMapper<Document> mapper;
        //private MapFieldSelector fieldSelector;

        public AzureSearchResults(AzureSearchContext context, AzureQuery query, DocumentSearchResult searchHits, int startIndex, int endIndex, SelectMethod selectMethod, IEnumerable<IExecutionContext> executionContexts, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors, FieldNameTranslator fieldNameTranslator)
        {
            this.context = context;
            this.query = query;
            this.searchHits = searchHits;
            this.startIndex = startIndex;
            this.endIndex = endIndex;
            this.selectMethod = selectMethod;
            this.virtualFieldProcessors = virtualFieldProcessors;
            this.fieldNameTranslator = fieldNameTranslator;
            this.configuration = (AzureIndexConfiguration)context.Index.Configuration;
            //this.fieldSelector = (MapFieldSelector)null;
            this.executionContexts = executionContexts;
            var executionContext = this.executionContexts != null ? Enumerable.FirstOrDefault<IExecutionContext>(this.executionContexts, (Func<IExecutionContext, bool>)(c => c is OverrideExecutionContext<IIndexDocumentPropertyMapper<Document>>)) as OverrideExecutionContext<IIndexDocumentPropertyMapper<Document>> : (OverrideExecutionContext<IIndexDocumentPropertyMapper<Document>>)null;
            this.mapper = (executionContext != null ? executionContext.OverrideObject : null) ?? this.configuration.IndexDocumentPropertyMapper;
            //if (selectMethod != null && selectMethod.FieldNames != null && selectMethod.FieldNames.Length > 0)
            //{
            //    this.fieldSelector = this.GetMapFieldSelector(context, (IEnumerable<string>)selectMethod.FieldNames);
            //}
            //else
            //{
            //    if (this.selectMethod != null)
            //        return;
            //    IEnumerable<string> documentFieldsToRead = this.mapper.GetDocumentFieldsToRead<TElement>(executionContexts);
            //    this.fieldSelector = this.GetMapFieldSelector(context, documentFieldsToRead);
            //}
        }

        //private MapFieldSelector GetMapFieldSelector(LuceneSearchContext context, IEnumerable<string> fieldNames)
        //{
        //    if (fieldNames == null)
        //        return (MapFieldSelector)null;
        //    HashSet<string> hashSet = new HashSet<string>(fieldNames);
        //    if (hashSet.Count == 0)
        //        return (MapFieldSelector)null;
        //    hashSet.Add("_uniqueid");
        //    hashSet.Add("_datasource");
        //    string[] strArray = Enumerable.ToArray<string>((IEnumerable<string>)hashSet);
        //    if (strArray.Length == 0)
        //        return (MapFieldSelector)null;
        //    return new MapFieldSelector(strArray);
        //}

        public TElement ElementAt(int index)
        {
            if (this.startIndex > this.endIndex || index < 0 || index > this.endIndex - this.startIndex)
                throw new IndexOutOfRangeException();
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), index);
        }

        public TElement ElementAtOrDefault(int index)
        {
            if (this.startIndex > this.endIndex || index < 0 || index > this.endIndex - this.startIndex)
                return default(TElement);
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), index);
        }

        public IEnumerable<SearchHit<TElement>> GetSearchHits()
        {
            for (int idx = this.startIndex; idx <= this.endIndex; ++idx)
            {
                //Document doc = this.context.Searcher.Doc(this.searchHits.ScoreDocs[idx].Doc, (FieldSelector)this.fieldSelector);
                var doc = this.searchHits.Results[idx].Document;
                if (!this.context.SecurityOptions.HasFlag((Enum)SearchSecurityOptions.DisableSecurityCheck))
                {
                    object secTokenFieldValue;
                    object dataSourceFieldValue;
                    doc.TryGetValue("s_uniqueid", out secTokenFieldValue);
                    doc.TryGetValue("s_datasource", out dataSourceFieldValue);
                    string secToken = secTokenFieldValue != null ? secTokenFieldValue.ToString() : null;
                    string dataSource = dataSourceFieldValue != null ? dataSourceFieldValue.ToString() : null;
                    if (!string.IsNullOrEmpty(secToken))
                    {
                        bool isExcluded = OutboundIndexFilterPipeline.CheckItemSecurity(this.context.Index.Locator.GetInstance<ICorePipeline>(), this.context.Index.Locator.GetInstance<IAccessRight>(), new OutboundIndexFilterArgs(secToken, dataSource));
                        if (!isExcluded)
                            yield return new SearchHit<TElement>(0f, this.mapper.MapToType<TElement>(doc, this.selectMethod, this.virtualFieldProcessors, this.executionContexts, this.context.SecurityOptions));
                    }
                }
                else
                    yield return new SearchHit<TElement>(0f, this.mapper.MapToType<TElement>(doc, this.selectMethod, this.virtualFieldProcessors, this.executionContexts, this.context.SecurityOptions));
            }
        }

        public IEnumerable<TElement> GetSearchResults()
        {
            return this.GetSearchResults(this.startIndex, this.endIndex);
        }

        public IEnumerable<TElement> GetSearchResults(int startIndex, int endIndex)
        {
            for (int idx = startIndex; idx <= endIndex; ++idx)
            {
                //Document doc = this.context.Searcher.Doc(this.searchHits.ScoreDocs[idx].Doc, (FieldSelector)this.fieldSelector);
                var doc = this.searchHits.Results.Skip(idx).FirstOrDefault().Document;
                if (!this.context.SecurityOptions.HasFlag((Enum)SearchSecurityOptions.DisableSecurityCheck))
                {
                    object secTokenFieldValue;
                    object dataSourceFieldValue;
                    doc.TryGetValue("s_uniqueid", out secTokenFieldValue);
                    doc.TryGetValue("s_datasource", out dataSourceFieldValue);
                    string secToken = secTokenFieldValue != null ? secTokenFieldValue.ToString() : null;
                    string dataSource = dataSourceFieldValue != null ? dataSourceFieldValue.ToString() : null;
                    if (!string.IsNullOrEmpty(secToken))
                    {
                        bool isExcluded = OutboundIndexFilterPipeline.CheckItemSecurity(this.context.Index.Locator.GetInstance<ICorePipeline>(), this.context.Index.Locator.GetInstance<IAccessRight>(), new OutboundIndexFilterArgs(secToken, dataSource));
                        if (!isExcluded)
                            yield return this.mapper.MapToType<TElement>(doc, this.selectMethod, this.virtualFieldProcessors, this.executionContexts, this.context.SecurityOptions);
                    }
                }
                else
                    yield return this.mapper.MapToType<TElement>(doc, this.selectMethod, this.virtualFieldProcessors, this.executionContexts, this.context.SecurityOptions);
            }
        }

        public bool Any()
        {
            return this.startIndex <= this.endIndex;
        }

        public long Count()
        {
            if (this.startIndex > this.endIndex)
                return 0;
            return (long)(this.endIndex - this.startIndex + 1);
        }

        public TElement First()
        {
            if (this.Count() < 1L)
                throw new InvalidOperationException("Sequence contains no elements");
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), 0);
        }

        public TElement FirstOrDefault()
        {
            if (this.Count() < 1L)
                return default(TElement);
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), 0);
        }

        public TElement Last()
        {
            if (this.Count() < 1L)
                throw new InvalidOperationException("Sequence contains no elements");
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), this.endIndex);
        }

        public TElement LastOrDefault()
        {
            if (this.Count() < 1L)
                return default(TElement);
            return Enumerable.ElementAt<TElement>(this.GetSearchResults(), this.endIndex);
        }

        public TElement Single()
        {
            if (this.Count() < 1L)
                throw new InvalidOperationException("Sequence contains no elements");
            if (this.Count() > 1L)
                throw new InvalidOperationException("Sequence contains more than one element");
            return Enumerable.Single<TElement>(this.GetSearchResults());
        }

        public TElement SingleOrDefault()
        {
            if (this.Count() == 0L)
                return default(TElement);
            if (this.Count() == 1L)
                return Enumerable.Single<TElement>(this.GetSearchResults());
            throw new InvalidOperationException("Sequence contains more than one element");
        }
    }
}
