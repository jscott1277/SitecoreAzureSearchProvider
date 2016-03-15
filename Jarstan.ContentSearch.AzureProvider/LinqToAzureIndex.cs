using Jarstan.ContentSearch.Linq.Azure;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Pipelines.GetFacets;
using Sitecore.ContentSearch.Pipelines.ProcessFacets;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Azure.Search.Models;
using Lucene.Net.Search;
using Jarstan.ContentSearch.Linq.Methods;
using Jarstan.ContentSearch.Linq;
using Jarstan.ContentSearch.SearchTypes;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class LinqToAzureIndex<TItem> : AzureIndex<TItem>
    {
        private readonly AzureSearchContext context;
        private readonly IContentSearchConfigurationSettings settings;
        private readonly ICorePipeline pipeline;

        public LinqToAzureIndex(AzureSearchContext context)
       : this(context, (IExecutionContext[])null)
        {
        }

        public LinqToAzureIndex(AzureSearchContext context, IExecutionContext executionContext)
        : this(context, new IExecutionContext[1]
            {
            executionContext
            })
        {
        }
        public LinqToAzureIndex(AzureSearchContext context, IExecutionContext[] executionContexts)
        : base(new AzureIndexParameters(context.Index.Configuration.IndexFieldStorageValueFormatter, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator, (fieldName => context.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName) as IAzureSearchFieldConfiguration), executionContexts, context.Index.Configuration.FieldMap, context.ConvertQueryDatesToUtc))
        {
            Assert.ArgumentNotNull(context, "context");
            settings = context.Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
            pipeline = context.Index.Locator.GetInstance<ICorePipeline>();
            this.context = context;
        }

        public override TResult Execute<TResult>(AzureQuery query)
        {
            if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(query))
                return EnumerableLinq.ExecuteEnumerableLinqQuery<TResult>(query);
            if (!DoExecuteSearch(query))
                return ExecuteScalarMethod<TResult>(query);
            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(SearchResults<>))
            {
                var topDocs = ExecuteQueryAgainstAzure(query);
                var type = typeof(TResult).GetGenericArguments()[0];
                var methodInfo1 = GetType().GetMethod("ApplySearchMethods", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);
                var methodInfo2 = GetType().GetMethod("ApplyScalarMethods", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(TResult), type);
                var obj = methodInfo1.Invoke(this, new object[2]
                {
                  query,
                  topDocs
                });
                return (TResult)methodInfo2.Invoke(this, new object[3]
                {
                    query,
                    obj,
                    topDocs
                });
            }
            var topDocs1 = ExecuteQueryAgainstAzure(query);
            var processedResults = ApplySearchMethods<TResult>(query, topDocs1);
            return ApplyScalarMethods<TResult, TResult>(query, processedResults, topDocs1);
        }

        public override IEnumerable<TElement> FindElements<TElement>(AzureQuery query)
        {
            if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(query))
                return EnumerableLinq.ExecuteEnumerableLinqQuery<IEnumerable<TElement>>(query);
            var searchHits = ExecuteQueryAgainstAzure(query);
            return ApplySearchMethods<TElement>(query, searchHits).GetSearchResults();
        }

        protected virtual Sort GetSorting(AzureQuery query)
        {
            Assert.ArgumentNotNull(query, "query");
            Sort sort = null;
            if (query.Methods != null)
            {
                var sortFieldArray = Enumerable.ToArray(Enumerable.Reverse(Enumerable.Select(Enumerable.Where(query.Methods, m => m.MethodType == QueryMethodType.OrderBy), (m => new SortField(((OrderByMethod)m).Field, GetSortFieldType((OrderByMethod)m), ((OrderByMethod)m).SortDirection == SortDirection.Descending)))));
                if (sortFieldArray.Length > 0)
                {
                    sort = new Sort(sortFieldArray);
                    if (settings.EnableSearchDebug())
                    {
                        var stringBuilder = new StringBuilder();
                        foreach (var sortField in sortFieldArray)
                            stringBuilder.Append(sortField.Field.Replace("sortfield", string.Empty) + " ");
                        SearchLog.Log.Debug(string.Format("Sorting Azure query by{0}", stringBuilder), null);
                    }
                }
            }
            return sort;
        }

        private AzureSearchResults<TElement> ApplySearchMethods<TElement>(AzureQuery query, DocumentSearchResult searchHits)
        {
            var list = query.Methods != null ? new List<QueryMethod>(query.Methods) : new List<QueryMethod>();
            list.Reverse();
            SelectMethod selectMethod = null;
            foreach (var queryMethod in list)
            {
                if (queryMethod.MethodType == QueryMethodType.Select)
                    selectMethod = (SelectMethod)queryMethod;
            }
            int startIndex = 0;
            int endIndex = searchHits.Results.Count - 1;
            return new AzureSearchResults<TElement>(context, query, searchHits, startIndex, endIndex, selectMethod, query.ExecutionContexts, query.VirtualFieldProcessors, FieldNameTranslator);
        }

        private bool DoExecuteSearch(AzureQuery query)
        {
            var retVal = true;
            if (Enumerable.First(query.Methods).MethodType == QueryMethodType.GetFacets)
                retVal = false;

            if (query.Methods.FirstOrDefault().MethodType == QueryMethodType.All)
            {
                var customMethod = query.Methods.FirstOrDefault() as CustomMethod;
                if (customMethod != null)
                    retVal = false;
            }

            return retVal;
        }

        private TResult ApplyScalarMethods<TResult, TDocument>(AzureQuery query, AzureSearchResults<TDocument> processedResults, DocumentSearchResult results)
        {
            var queryMethod = query.Methods.FirstOrDefault();
            object obj;
            switch (queryMethod.MethodType)
            {
                case QueryMethodType.All:
                    obj = true;
                    //Check for CustomMethod
                    var customMethod = queryMethod as CustomMethod;
                    if (customMethod != null)
                    {
                        switch (customMethod.CustomMethodType)
                        {
                            case Linq.Nodes.CustomQueryMethodTypes.GetHightlights:
                                obj = ExecuteGetHighlightResults(query);
                                break;
                        }
                    }
                    break;
                case QueryMethodType.Any:
                    obj = (processedResults.Any() ? 1 : 0);
                    break;
                case QueryMethodType.Count:
                    obj = query.Methods.Any(m =>
                    {
                        if (m.MethodType != QueryMethodType.Skip)
                            return m.MethodType == QueryMethodType.Take;
                        return true;
                    }) ? processedResults.Count() : results.Count;
                    break;
                case QueryMethodType.ElementAt:
                    obj = !((ElementAtMethod)queryMethod).AllowDefaultValue ? processedResults.ElementAt(((ElementAtMethod)queryMethod).Index) : processedResults.ElementAtOrDefault(((ElementAtMethod)queryMethod).Index);
                    break;
                case QueryMethodType.First:
                    obj = !((FirstMethod)queryMethod).AllowDefaultValue ? processedResults.First() : processedResults.FirstOrDefault();
                    break;
                case QueryMethodType.Last:
                    obj = !((LastMethod)queryMethod).AllowDefaultValue ? processedResults.Last() : processedResults.LastOrDefault();
                    break;
                case QueryMethodType.Single:
                    obj = !((SingleMethod)queryMethod).AllowDefaultValue ? processedResults.Single() : processedResults.SingleOrDefault();
                    break;
                case QueryMethodType.GetResults:
                    obj = ExecuteGetResults<TDocument>(query, processedResults, results);
                    break;
                case QueryMethodType.GetFacets:
                    obj = ExecuteGetFacets(query);
                    break;
                default:
                    throw new InvalidOperationException("Invalid query method: " + queryMethod.MethodType);
            }
            return (TResult)Convert.ChangeType(obj, typeof(TResult));
        }

        private TResult ExecuteScalarMethod<TResult>(AzureQuery query)
        {
            var queryMethod = Enumerable.First(query.Methods);
            if (queryMethod.MethodType == QueryMethodType.GetFacets)
                return (TResult)Convert.ChangeType(ExecuteGetFacets(query), typeof(TResult));

            var customMethod = queryMethod as CustomMethod;
            if (customMethod != null)
            {
                switch (customMethod.CustomMethodType)
                {
                    case Linq.Nodes.CustomQueryMethodTypes.GetHightlights:
                        return (TResult)Convert.ChangeType(ExecuteGetHighlightResults(query), typeof(TResult));
                    default:
                        throw new InvalidOperationException("Invalid query method: " + customMethod.CustomMethodType);
                }
            }

            throw new InvalidOperationException("Invalid query method: " + queryMethod.MethodType);
        }

        private SearchResults<TDocument> ExecuteGetResults<TDocument>(AzureQuery query, AzureSearchResults<TDocument> processedResults, DocumentSearchResult results)
        {
            var searchHits = processedResults.GetSearchHits();
            Sitecore.ContentSearch.Linq.FacetResults facets = null;
            if (query.FacetQueries != null && query.FacetQueries.Count > 0)
                facets = ExecuteGetFacets(query);
            return new SearchResults<TDocument>(searchHits, (int)results.Count, facets);
        }

        private Sitecore.ContentSearch.Linq.FacetResults ExecuteGetFacets(AzureQuery query)
        {
            var query1 = query;
            var list = new List<FacetQuery>(query.FacetQueries);
            var facetQueries = GetFacetsPipeline.Run(pipeline, new GetFacetsArgs(null, query.FacetQueries, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator)).FacetQueries;
            var facets = new Dictionary<string, ICollection<KeyValuePair<string, int>>>();
            foreach (var facetQuery in facetQueries)
            {
                foreach (var keyValuePair in GetFacets(query1, facetQuery.FieldNames, new int?(1), Enumerable.Cast<string>(facetQuery.FilterValues), new bool?(), null, facetQuery.MinimumResultCount))
                    facets[facetQuery.CategoryName] = keyValuePair.Value;
            }
            var dictionary = ProcessFacetsPipeline.Run(pipeline, new ProcessFacetsArgs(facets, query.FacetQueries, list, context.Index.Configuration.VirtualFields, context.Index.FieldNameTranslator));
            foreach (var facetQuery in list)
            {
                var originalQuery = facetQuery;
                if (originalQuery.FilterValues != null && Enumerable.Any(originalQuery.FilterValues) && dictionary.ContainsKey(originalQuery.CategoryName))
                {
                    var collection = dictionary[originalQuery.CategoryName];
                    dictionary[originalQuery.CategoryName] = Enumerable.ToList(Enumerable.Where(collection, (cv => Enumerable.Contains(originalQuery.FilterValues, cv.Key))));
                }
            }
            var facetResults = new Sitecore.ContentSearch.Linq.FacetResults();
            foreach (var keyValuePair in dictionary)
            {
                IEnumerable<FacetValue> values = Enumerable.Select(keyValuePair.Value, v => new FacetValue(v.Key, v.Value));
                facetResults.Categories.Add(new FacetCategory(keyValuePair.Key, values));
            }
            return facetResults;
        }

        private HighlightSearchResults<AzureSearchResultItem> ExecuteGetHighlightResults(AzureQuery query)
        {
            var results = ExecuteQueryAgainstAzure(query, null, query.Highlights);
            var hits = ApplySearchMethods<AzureSearchResultItem>(query, results).GetSearchHits().ToList();

            if (query.MergeHighlights)
            {
                for (var i = 0; i < hits.Count; i++)
                {
                    foreach (var highlight in hits[i].HighlightResults)
                    {
                        hits[i].Document.SetValueByIndexFieldName(highlight.Name, highlight.Values.FirstOrDefault());
                    }
                }
            }

            var azureResults = new HighlightSearchResults<AzureSearchResultItem>(hits, (int)results.Count);
            return azureResults;
        }

        private IDictionary<string, ICollection<KeyValuePair<string, int>>> GetFacets(AzureQuery query, IEnumerable<string> facetFields, int? minResultCount, IEnumerable<string> filters, bool? sort, string prefix, int? limit)
        {
            Assert.ArgumentNotNull(query, "query");
            Assert.ArgumentNotNull(facetFields, "facetFields");
            
            SearchLog.Log.Info(string.Format("GetFacets : {0} : {1}{2}", string.Join(",", facetFields), query, filters != null ? (" Filters: " + string.Join(",", filters)) : string.Empty), null);
            var dictionary = new Dictionary<string, ICollection<KeyValuePair<string, int>>>();
            var searchResult = ExecuteQueryAgainstAzure(query, facetFields);

            var minCount = limit.HasValue ? limit.Value : 0;

            foreach (var facetResult in searchResult.Facets)
            {
                var vals = facetResult.Value.Where(s => s.Count.Value >= minCount).Select(s => new KeyValuePair<string, int>(s.Value.ToString(), (int)s.Count.Value)).ToList();              
                dictionary.Add(facetResult.Key, vals);
            }

            return dictionary;
        }

        private DocumentSearchResult ExecuteQueryAgainstAzure(AzureQuery query, IEnumerable<string> facetFields = null, IEnumerable<string> highlightFields = null)
        {
            if (settings.EnableSearchDebug())
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Executing azure query: " + query);
                foreach (var queryMethod in query.Methods)
                    stringBuilder.AppendLine("    - " + queryMethod);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(AzureQueryLogger.Trace(query.Query));
                stringBuilder.AppendLine("Rewritten Lucene Query:");
                stringBuilder.AppendLine();
                //stringBuilder.AppendLine(AzureQueryLogger.Trace(query.Query.Rewrite(Enumerable.First<IAzureProviderSearchable>(context.Searchables).CreateSearcher(AzureIndexAccess.ReadOnlyCached).IndexReader)));
                SearchLog.Log.Debug(stringBuilder.ToString(), null);
            }

            var getResultsMethod = Enumerable.FirstOrDefault(query.Methods, m => m.MethodType == QueryMethodType.GetResults) as GetResultsMethod;
            bool trackDocScores = getResultsMethod != null && (getResultsMethod.Options & GetResultsOptions.GetScores) == GetResultsOptions.GetScores;

            var indexClient = ((AzureIndex)context.Index).AzureSearchClient;
            var searchParams = new SearchParameters();
            searchParams.IncludeTotalResultCount = true;
            searchParams.SearchMode = SearchMode.Any;
            searchParams.QueryType = QueryType.Full;

            var azureIndexSchema = context.Index.Schema as AzureIndexSchema;
            if (azureIndexSchema != null && azureIndexSchema.ContainsDefaultScoringProfile())
            {
                searchParams.ScoringProfile = ((AzureIndex)context.Index).AzureConfiguration.AzureDefaultScoringProfileName;
            }

            var sorting = GetSorting(query);
            if (sorting != null)
            {
                searchParams.OrderBy = new List<string>();
                foreach (var sort in sorting.GetSort())
                {
                    searchParams.OrderBy.Add(sort.Field + (sort.Reverse ? " desc" : " asc"));
                }
            }

            if (query.Filter != null)
            {
                searchParams.Filter = query.Filter.ToString();
            }

            if (facetFields != null && facetFields.Any())
            {
                searchParams.Facets = new List<string>();
                foreach (var facetField in facetFields)
                {
                    searchParams.Facets.Add(facetField);
                }
            }

            if (highlightFields != null && highlightFields.Any())
            {
                searchParams.HighlightPreTag = query.HighlightPreTag;
                searchParams.HighlightPostTag = query.HighlightPostTag;
                searchParams.HighlightFields = new List<string>();
                foreach (var highlightField in highlightFields)
                {
                    searchParams.HighlightFields.Add(highlightField);
                }
            }

            //var weight = context.Searcher.CreateWeight(query.Query);
            
            var skip = GetSkip(query);
            if (skip.HasValue)
            {
                searchParams.Skip = skip.Value;
            }
            var take = GetTake(query);
            if (take.HasValue)
            {
                searchParams.Top = take.Value;
            }
            else
            {
                searchParams.Top = 1000; //Max Azure Search allows per request.
            }
            
            //TODO:  Figure out how to fix the '+=' issue in AzureQueryMapper
            var strQuery = query.ToString().Replace("+-", "-");

            SearchLog.Log.Info(string.Format("ExecuteQueryAgainstAzure ({0}): {1} - Filter : {2} - Facets : {3} - Highlights : {4}", context.Index.Name, strQuery, query.Filter != null ? query.Filter.ToString() : string.Empty, query.FacetQueries != null ? string.Join(", ", query.FacetQueries) : string.Empty, query.Highlights != null ? string.Join(", ", query.Highlights) : string.Empty), null);

            var responseTask = indexClient.Documents.SearchWithHttpMessagesAsync(strQuery, searchParams);
            responseTask.Wait();
            var response = responseTask.Result.Body;

            if (settings.EnableSearchDebug())
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat("fieldSortDoTrackScores: {0}\n", (trackDocScores ? 1 : 0));
                stringBuilder.AppendFormat("Collector TotalHits:    {0}\n", response.Count);
                stringBuilder.AppendFormat("Docs TotalHits:      {0}\n", response.Count);
                SearchLog.Log.Debug(stringBuilder.ToString());
            }

            return response;
        }

        private int GetSortFieldType(OrderByMethod orderByMethod)
        {
            if (orderByMethod.FieldType == typeof(string))
                return 3;
            if (AzureFieldBuilder.IsNumericField(orderByMethod.FieldType))
                return 6;
            return AzureFieldBuilder.IsFloatingPointField(orderByMethod.FieldType) ? 7 : 3;
        }

        private int GetMaxHits(AzureQuery query, int maxDoc)
        {
            var list = query.Methods != null ? new List<QueryMethod>(query.Methods) : new List<QueryMethod>();
            list.Reverse();
            var modifierScalarMethod = GetMaxHitsModifierScalarMethod(query.Methods);
            int num1 = 0;
            int num2 = maxDoc - 1;
            int num3 = num2;
            int num4 = num2;
            foreach (var queryMethod in list)
            {
                switch (queryMethod.MethodType)
                {
                    case QueryMethodType.Skip:
                        int count = ((SkipMethod)queryMethod).Count;
                        if (count > 0)
                        {
                            num1 += count;
                            continue;
                        }
                        continue;
                    case QueryMethodType.Take:
                        int num5 = ((TakeMethod)queryMethod).Count;
                        if (num5 <= 0)
                        {
                            num2 = num1++;
                            continue;
                        }
                        if (num5 > 1 && modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.First)
                            num5 = 1;
                        if (num5 > 1 && modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.Any)
                            num5 = 1;
                        if (num5 > 2 && modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.Single)
                            num5 = 2;
                        num2 = num1 + num5 - 1;
                        if (num2 > num3)
                        {
                            num2 = num3;
                            continue;
                        }
                        if (num3 < num2)
                        {
                            num3 = num2;
                            continue;
                        }
                        continue;
                    default:
                        continue;
                }
            }
            if (num4 == num2)
            {
                int num5 = -1;
                if (modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.First)
                    num5 = 1;
                if (modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.Any)
                    num5 = 1;
                if (modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.Single)
                    num5 = 2;
                if (num5 >= 0)
                {
                    num2 = num1 + num5 - 1;
                    if (num2 > num3)
                        num2 = num3;
                    else if (num3 < num2)
                        ;
                }
            }
            if (num4 == num2 && num1 == 0 && (modifierScalarMethod != null && modifierScalarMethod.MethodType == QueryMethodType.Count))
                num2 = -1;
            int num6 = num2 + 1;
            if (settings.EnableSearchDebug())
                SearchLog.Log.Debug(string.Format("Max hits: {0}", num6), null);
            return num6;
        }

        private QueryMethod GetMaxHitsModifierScalarMethod(List<QueryMethod> methods)
        {
            if (methods.Count == 0)
                return null;
            var queryMethod = Enumerable.First(methods);
            switch (queryMethod.MethodType)
            {
                case QueryMethodType.Any:
                case QueryMethodType.Count:
                case QueryMethodType.First:
                case QueryMethodType.Last:
                case QueryMethodType.Single:
                    return queryMethod;
                default:
                    return null;
            }
        }

        private int? GetSkip(AzureQuery query)
        {
            var skipMethod = GetQueryMethod(query, QueryMethodType.Skip);
            if (skipMethod != null)
            {
                return ((SkipMethod)skipMethod).Count;
            }

            return null;
        }

        private int? GetTake(AzureQuery query)
        { 
            var takeMethod = GetQueryMethod(query, QueryMethodType.Take);
            if (takeMethod != null)
            {
                return ((TakeMethod)takeMethod).Count;
            }

            return null;
        }

        private QueryMethod GetQueryMethod(AzureQuery query, QueryMethodType type)
        {
            foreach (var method in query.Methods)
            {
                if (method.MethodType == type)
                    return method;
            }

            return null;
        }

        private void GetPaging(AzureQuery query, int totalHits, out int startIdx, out int endIdx)
        {
            var list = query.Methods != null ? new List<QueryMethod>(query.Methods) : new List<QueryMethod>();
            list.Reverse();
            startIdx = 0;
            endIdx = totalHits - 1;
            int num = endIdx;
            foreach (QueryMethod queryMethod in list)
            {
                switch (queryMethod.MethodType)
                {
                    case QueryMethodType.Skip:
                        int count1 = ((SkipMethod)queryMethod).Count;
                        if (count1 > 0)
                        {
                            startIdx += count1;
                            continue;
                        }
                        continue;
                    case QueryMethodType.Take:
                        int count2 = ((TakeMethod)queryMethod).Count;
                        if (count2 <= 0)
                        {
                            endIdx = startIdx++;
                            continue;
                        }
                        endIdx = startIdx + count2 - 1;
                        if (endIdx > num)
                        {
                            endIdx = num;
                            continue;
                        }
                        if (num < endIdx)
                        {
                            num = endIdx;
                            continue;
                        }
                        continue;
                    default:
                        continue;
                }
            }
            if (!settings.EnableSearchDebug())
                return;
            SearchLog.Log.Debug(string.Format("Indexes: {0} - {1}", startIdx, endIdx), null);
        }
    }
}
