using Sitecore.Search;
using Slalom.ContentSearch.Linq.Azure;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Pipelines.GetFacets;
using Sitecore.ContentSearch.Pipelines.ProcessFacets;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Microsoft.Azure.Search.Models;
using Lucene.Net.Search;
using System.Threading;
using System.IO;
using Lucene.Net.QueryParsers;
using System.Text.RegularExpressions;

namespace Slalom.ContentSearch.AzureProvider
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
            this.settings = context.Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
            this.pipeline = context.Index.Locator.GetInstance<ICorePipeline>();
            this.context = context;
        }

        public override TResult Execute<TResult>(AzureQuery query)
        {
            if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(query))
                return EnumerableLinq.ExecuteEnumerableLinqQuery<TResult>(query);
            if (!this.DoExecuteSearch(query))
                return this.ExecuteScalarMethod<TResult>(query);
            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(SearchResults<>))
            {
                var topDocs = this.ExecuteQueryAgainstAzure(query);
                Type type = typeof(TResult).GetGenericArguments()[0];
                MethodInfo methodInfo1 = this.GetType().GetMethod("ApplySearchMethods", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(type);
                MethodInfo methodInfo2 = this.GetType().GetMethod("ApplyScalarMethods", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(TResult), type);
                object obj = methodInfo1.Invoke((object)this, new object[2]
                {
                  query,
                  topDocs
                });
                return (TResult)methodInfo2.Invoke((object)this, new object[3]
                {
                    query,
                    obj,
                    topDocs
                });
            }
            var topDocs1 = this.ExecuteQueryAgainstAzure(query);
            AzureSearchResults<TResult> processedResults = this.ApplySearchMethods<TResult>(query, topDocs1);
            return this.ApplyScalarMethods<TResult, TResult>(query, processedResults, topDocs1);
        }

        public override IEnumerable<TElement> FindElements<TElement>(AzureQuery query)
        {
            if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery((IQuery)query))
                return EnumerableLinq.ExecuteEnumerableLinqQuery<IEnumerable<TElement>>((IQuery)query);
            var searchHits = this.ExecuteQueryAgainstAzure(query);
            return this.ApplySearchMethods<TElement>(query, searchHits).GetSearchResults();
        }

        protected virtual Sort GetSorting(AzureQuery query)
        {
            Assert.ArgumentNotNull(query, "query");
            Sort sort = null;
            if (query.Methods != null)
            {
                SortField[] sortFieldArray = Enumerable.ToArray(Enumerable.Reverse(Enumerable.Select(Enumerable.Where(query.Methods, m => m.MethodType == QueryMethodType.OrderBy), (m => new SortField(((OrderByMethod)m).Field, this.GetSortFieldType((OrderByMethod)m), ((OrderByMethod)m).SortDirection == SortDirection.Descending)))));
                if (sortFieldArray.Length > 0)
                {
                    sort = new Sort(sortFieldArray);
                    if (this.settings.EnableSearchDebug())
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        foreach (SortField sortField in sortFieldArray)
                            stringBuilder.Append(sortField.Field.Replace("sortfield", string.Empty) + " ");
                        SearchLog.Log.Debug(string.Format("Sorting Azure query by{0}", (object)stringBuilder), (Exception)null);
                    }
                }
            }
            return sort;
        }

        private AzureSearchResults<TElement> ApplySearchMethods<TElement>(AzureQuery query, DocumentSearchResult searchHits)
        {
            List<QueryMethod> list = query.Methods != null ? new List<QueryMethod>((IEnumerable<QueryMethod>)query.Methods) : new List<QueryMethod>();
            list.Reverse();
            SelectMethod selectMethod = null;
            foreach (QueryMethod queryMethod in list)
            {
                if (queryMethod.MethodType == QueryMethodType.Select)
                    selectMethod = (SelectMethod)queryMethod;
            }
            int startIndex = 0;
            int endIndex = searchHits.Results.Count - 1;
            return new AzureSearchResults<TElement>(this.context, query, searchHits, startIndex, endIndex, selectMethod, query.ExecutionContexts, query.VirtualFieldProcessors, this.FieldNameTranslator);
        }

        private bool DoExecuteSearch(AzureQuery query)
        {
            return Enumerable.First(query.Methods).MethodType != QueryMethodType.GetFacets;
        }

        private TResult ApplyScalarMethods<TResult, TDocument>(AzureQuery query, AzureSearchResults<TDocument> processedResults, DocumentSearchResult results)
        {
            QueryMethod queryMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)query.Methods);
            object obj;
            switch (queryMethod.MethodType)
            {
                case QueryMethodType.All:
                    obj = (object)true;
                    break;
                case QueryMethodType.Any:
                    obj = (processedResults.Any() ? 1 : 0);
                    break;
                case QueryMethodType.Count:
                    obj = Enumerable.Any<QueryMethod>((IEnumerable<QueryMethod>)query.Methods, (Func<QueryMethod, bool>)(m =>
                    {
                        if (m.MethodType != QueryMethodType.Skip)
                            return m.MethodType == QueryMethodType.Take;
                        return true;
                    })) ? (object)processedResults.Count() : results.Count;
                    break;
                case QueryMethodType.ElementAt:
                    obj = !((ElementAtMethod)queryMethod).AllowDefaultValue ? (object)processedResults.ElementAt(((ElementAtMethod)queryMethod).Index) : (object)processedResults.ElementAtOrDefault(((ElementAtMethod)queryMethod).Index);
                    break;
                case QueryMethodType.First:
                    obj = !((FirstMethod)queryMethod).AllowDefaultValue ? (object)processedResults.First() : (object)processedResults.FirstOrDefault();
                    break;
                case QueryMethodType.Last:
                    obj = !((LastMethod)queryMethod).AllowDefaultValue ? (object)processedResults.Last() : (object)processedResults.LastOrDefault();
                    break;
                case QueryMethodType.Single:
                    obj = !((SingleMethod)queryMethod).AllowDefaultValue ? (object)processedResults.Single() : (object)processedResults.SingleOrDefault();
                    break;
                case QueryMethodType.GetResults:
                    obj = (object)this.ExecuteGetResults<TDocument>(query, processedResults, results);
                    break;
                case QueryMethodType.GetFacets:
                    obj = (object)this.ExecuteGetFacets(query);
                    break;
                default:
                    throw new InvalidOperationException("Invalid query method: " + (object)queryMethod.MethodType);
            }
            return (TResult)Convert.ChangeType(obj, typeof(TResult));
        }

        private TResult ExecuteScalarMethod<TResult>(AzureQuery query)
        {
            QueryMethod queryMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)query.Methods);
            if (queryMethod.MethodType == QueryMethodType.GetFacets)
                return (TResult)Convert.ChangeType((object)this.ExecuteGetFacets(query), typeof(TResult));
            throw new InvalidOperationException("Invalid query method: " + (object)queryMethod.MethodType);
        }

        private SearchResults<TDocument> ExecuteGetResults<TDocument>(AzureQuery query, AzureSearchResults<TDocument> processedResults, DocumentSearchResult results)
        {
            IEnumerable<SearchHit<TDocument>> searchHits = processedResults.GetSearchHits();
            Sitecore.ContentSearch.Linq.FacetResults facets = null;
            if (query.FacetQueries != null && query.FacetQueries.Count > 0)
                facets = this.ExecuteGetFacets(query);
            return new SearchResults<TDocument>(searchHits, (int)results.Count, facets);
        }

        private Sitecore.ContentSearch.Linq.FacetResults ExecuteGetFacets(AzureQuery query)
        {
            AzureQuery query1 = query;
            List<FacetQuery> list = new List<FacetQuery>((IEnumerable<FacetQuery>)query.FacetQueries);
            IEnumerable<FacetQuery> facetQueries = GetFacetsPipeline.Run(this.pipeline, new GetFacetsArgs((IQueryable)null, (IEnumerable<FacetQuery>)query.FacetQueries, (IDictionary<string, IVirtualFieldProcessor>)this.context.Index.Configuration.VirtualFields, (FieldNameTranslator)this.context.Index.FieldNameTranslator)).FacetQueries;
            Dictionary<string, ICollection<KeyValuePair<string, int>>> facets = new Dictionary<string, ICollection<KeyValuePair<string, int>>>();
            foreach (FacetQuery facetQuery in facetQueries)
            {
                foreach (KeyValuePair<string, ICollection<KeyValuePair<string, int>>> keyValuePair in (IEnumerable<KeyValuePair<string, ICollection<KeyValuePair<string, int>>>>)this.GetFacets(query1, facetQuery.FieldNames, new int?(1), Enumerable.Cast<string>((IEnumerable)facetQuery.FilterValues), new bool?(), (string)null, facetQuery.MinimumResultCount))
                    facets[facetQuery.CategoryName] = keyValuePair.Value;
            }
            IDictionary<string, ICollection<KeyValuePair<string, int>>> dictionary = ProcessFacetsPipeline.Run(this.pipeline, new ProcessFacetsArgs(facets, (IEnumerable<FacetQuery>)query.FacetQueries, (IEnumerable<FacetQuery>)list, (IDictionary<string, IVirtualFieldProcessor>)this.context.Index.Configuration.VirtualFields, (FieldNameTranslator)this.context.Index.FieldNameTranslator));
            foreach (FacetQuery facetQuery in list)
            {
                FacetQuery originalQuery = facetQuery;
                if (originalQuery.FilterValues != null && Enumerable.Any<object>(originalQuery.FilterValues) && dictionary.ContainsKey(originalQuery.CategoryName))
                {
                    ICollection<KeyValuePair<string, int>> collection = dictionary[originalQuery.CategoryName];
                    dictionary[originalQuery.CategoryName] = (ICollection<KeyValuePair<string, int>>)Enumerable.ToList<KeyValuePair<string, int>>(Enumerable.Where<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)collection, (Func<KeyValuePair<string, int>, bool>)(cv => Enumerable.Contains<object>(originalQuery.FilterValues, (object)cv.Key))));
                }
            }
            var facetResults = new Sitecore.ContentSearch.Linq.FacetResults();
            foreach (KeyValuePair<string, ICollection<KeyValuePair<string, int>>> keyValuePair in (IEnumerable<KeyValuePair<string, ICollection<KeyValuePair<string, int>>>>)dictionary)
            {
                IEnumerable<FacetValue> values = Enumerable.Select<KeyValuePair<string, int>, FacetValue>((IEnumerable<KeyValuePair<string, int>>)keyValuePair.Value, (Func<KeyValuePair<string, int>, FacetValue>)(v => new FacetValue(v.Key, v.Value)));
                facetResults.Categories.Add(new FacetCategory(keyValuePair.Key, values));
            }
            return facetResults;
        }

        private IDictionary<string, ICollection<KeyValuePair<string, int>>> GetFacets(AzureQuery query, IEnumerable<string> facetFields, int? minResultCount, IEnumerable<string> filters, bool? sort, string prefix, int? limit)
        {
            
            Assert.ArgumentNotNull((object)query, "query");
            Assert.ArgumentNotNull((object)facetFields, "facetFields");
            var query1 = query.Query;
            var queryParsed = query1;
            if (query.Filter != null)
                queryParsed = new BooleanQuery()
        {
          {
            query1,
            Occur.MUST
          },
          {
            query.Filter,
            Occur.MUST
          }
        };
            string[] facetFieldArray = Enumerable.ToArray<string>(facetFields);
            string[] filtersArray = filters != null ? Enumerable.ToArray<string>(filters) : (string[])null;
            SearchLog.Log.Info(string.Format("GetFacets : {0} : {1}{2}", (object)string.Join(",", facetFieldArray), (object)query1, filtersArray != null ? (object)(" Filters: " + string.Join(",", filtersArray)) : (object)string.Empty), (Exception)null);
            Dictionary<string, ICollection<KeyValuePair<string, int>>> dictionary = new Dictionary<string, ICollection<KeyValuePair<string, int>>>(facetFieldArray.Length);
            return dictionary;

      //      List<SimpleFacetedSearch.Hits> shardHits = new List<SimpleFacetedSearch.Hits>(Enumerable.Count<ILuceneProviderSearchable>(this.context.Searchables));
      //      Parallel.ForEach<ILuceneProviderSearchable>(this.context.Searchables, (Action<ILuceneProviderSearchable>)(searchable =>
      //      {
      //          using (IndexSearcher searcher = searchable.CreateSearcher(LuceneIndexAccess.ReadOnly | LuceneIndexAccess.ReadOnlyCached))
      //          {
      //              try
      //              {
      //                  SimpleFacetedSearch.Hits hits = (filtersArray == null || filtersArray.Length <= 0 ? new SimpleFacetedSearch(searcher.IndexReader, facetFieldArray) : new SimpleFacetedSearch(searcher.IndexReader, facetFieldArray[0], filtersArray)).Search(queryParsed, 10);
      //                  lock (shardHits)
      //                    shardHits.Add(hits);
      //              }
      //              catch (ArgumentException ex)
      //              {
      //                  if (ex.Message.EndsWith("does not have any term position data stored") || ex.Message.EndsWith("does not have term vector offsets data stored"))
      //                      SearchLog.Log.Warn(ex.Message + ". The facet query will not match any documents - Update the fieldmap configuration for the field to vector type WITH_OFFSETS or WITH_POSITIONS_OFFSETS", (Exception)ex);
      //                  else
      //                      throw;
      //              }
      //              catch (Exception ex)
      //              {
      //                  throw;
      //              }
      //          }
      //      }));
      //      string index = string.Join(",", facetFieldArray);
      //      List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
      //      foreach (\u003C\u003Ef__AnonymousType0<string, long> fAnonymousType0 in Enumerable.Select(Enumerable.GroupBy<SimpleFacetedSearch.HitsPerFacet, string>(Enumerable.Where<SimpleFacetedSearch.HitsPerFacet>(Enumerable.SelectMany<SimpleFacetedSearch.Hits, SimpleFacetedSearch.HitsPerFacet>((IEnumerable<SimpleFacetedSearch.Hits>)shardHits, (Func<SimpleFacetedSearch.Hits, IEnumerable<SimpleFacetedSearch.HitsPerFacet>>)(sh => (IEnumerable<SimpleFacetedSearch.HitsPerFacet>)sh.HitsPerFacet)), (Func<SimpleFacetedSearch.HitsPerFacet, bool>)(facet =>
      //      {
      //          long hitCount = facet.HitCount;
      //          int? nullable = minResultCount;
      //          if (hitCount >= (long)nullable.GetValueOrDefault())
      //              return nullable.HasValue;
      //          return false;
      //      })), (Func<SimpleFacetedSearch.HitsPerFacet, string>)(facet => facet.Name.ToString())), grouping => new
      //      {
      //          Name = grouping.Key,
      //          HitCount = Enumerable.Sum<SimpleFacetedSearch.HitsPerFacet>((IEnumerable<SimpleFacetedSearch.HitsPerFacet>)grouping, (Func<SimpleFacetedSearch.HitsPerFacet, long>)(facet => facet.HitCount))
      //      }))
      //{
      //          string name = fAnonymousType0.Name;
      //          long hitCount = fAnonymousType0.HitCount;
      //          long num = hitCount;
      //          int? nullable = minResultCount;
      //          if ((num < (long)nullable.GetValueOrDefault() ? 0 : (nullable.HasValue ? 1 : 0)) != 0)
      //              list.Add(new KeyValuePair<string, int>(name, (int)hitCount));
      //      }
      //      dictionary[index] = (ICollection<KeyValuePair<string, int>>)list;
      //      return (IDictionary<string, ICollection<KeyValuePair<string, int>>>)dictionary;
        }

        private DocumentSearchResult ExecuteQueryAgainstAzure(AzureQuery query)
        {
            if (this.settings.EnableSearchDebug())
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Executing azure query: " + (object)query);
                foreach (QueryMethod queryMethod in query.Methods)
                    stringBuilder.AppendLine("    - " + (object)queryMethod);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(AzureQueryLogger.Trace(query.Query));
                stringBuilder.AppendLine("Rewritten Lucene Query:");
                stringBuilder.AppendLine();
                //stringBuilder.AppendLine(AzureQueryLogger.Trace(query.Query.Rewrite(Enumerable.First<IAzureProviderSearchable>(this.context.Searchables).CreateSearcher(AzureIndexAccess.ReadOnlyCached).IndexReader)));
                SearchLog.Log.Debug(stringBuilder.ToString(), (Exception)null);
            }

            var getResultsMethod = Enumerable.FirstOrDefault<QueryMethod>((IEnumerable<QueryMethod>)query.Methods, (Func<QueryMethod, bool>)(m => m.MethodType == QueryMethodType.GetResults)) as GetResultsMethod;
            bool trackDocScores = getResultsMethod != null && (getResultsMethod.Options & GetResultsOptions.GetScores) == GetResultsOptions.GetScores;

            var indexClient = ((AzureIndex)this.context.Index).AzureSearchClient;
            var searchParams = new SearchParameters();

            //Filter filter = (Filter)null;
            var sorting = this.GetSorting(query);
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

            //var weight = this.context.Searcher.CreateWeight(query.Query);
            
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
            searchParams.IncludeTotalResultCount = true;
            searchParams.SearchMode = SearchMode.Any;
            searchParams.QueryType = QueryType.Full;

            var strQuery = query.ToString();

            //TESTING
            //if (strQuery.Contains("Query("))
            //{
            //    var SpecialCharactersRx = new Regex(@"\+|\-|\&\&|\|\||\(|\)|\{|\}|\[|\]|\^|\~|\*|\?|\:|\\|\/", RegexOptions.Compiled);
            //    strQuery = SpecialCharactersRx.Replace(query.ToString(), @"\$0");
            //}

            //+(s_name:"Download Brochure" s_name:Download) +(+s_language:en +(+(s_name:/.*load/ s_name:/.*brochure/) +(+s_name:/Downl.*/ +s_name:/.*ownloa.*/)))

            //+(s_language:en s_language:da) +(+s_templatename:Image +(-s_name:"Windows Phone Landscape"))
            //+(s_language:en s_language:da) +(+s_templatename:Image -s_name:"Windows Phone Landscape")
            //var secQuery = "+(s_language:en s_language:da) +s_templatename:Image -s_name:Windows";
            //var secresponseTask = indexClient.Documents.SearchWithHttpMessagesAsync(secQuery, searchParams);
            //secresponseTask.Wait();
            //var secresponse = secresponseTask.Result.Body;
            //END TESTING

            //TODO:  Figure out how to fix this in AzureQueryMapper
            strQuery = strQuery.Replace("+-", "-");

            SearchLog.Log.Info(string.Format("ExecuteQueryAgainstAzure ({0}): {1} - Filter : {2}", this.context.Index.Name, strQuery, query.Filter != null ? query.Filter.ToString() : string.Empty), null);

            var responseTask = indexClient.Documents.SearchWithHttpMessagesAsync(strQuery, searchParams);
            responseTask.Wait();
            var response = responseTask.Result.Body;

            if (this.settings.EnableSearchDebug())
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
            List<QueryMethod> list = query.Methods != null ? new List<QueryMethod>((IEnumerable<QueryMethod>)query.Methods) : new List<QueryMethod>();
            list.Reverse();
            QueryMethod modifierScalarMethod = this.GetMaxHitsModifierScalarMethod(query.Methods);
            int num1 = 0;
            int num2 = maxDoc - 1;
            int num3 = num2;
            int num4 = num2;
            foreach (QueryMethod queryMethod in list)
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
            if (this.settings.EnableSearchDebug())
                SearchLog.Log.Debug(string.Format("Max hits: {0}", (object)num6), (Exception)null);
            return num6;
        }

        private QueryMethod GetMaxHitsModifierScalarMethod(List<QueryMethod> methods)
        {
            if (methods.Count == 0)
                return (QueryMethod)null;
            QueryMethod queryMethod = Enumerable.First<QueryMethod>((IEnumerable<QueryMethod>)methods);
            switch (queryMethod.MethodType)
            {
                case QueryMethodType.Any:
                case QueryMethodType.Count:
                case QueryMethodType.First:
                case QueryMethodType.Last:
                case QueryMethodType.Single:
                    return queryMethod;
                default:
                    return (QueryMethod)null;
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
            List<QueryMethod> list = query.Methods != null ? new List<QueryMethod>((IEnumerable<QueryMethod>)query.Methods) : new List<QueryMethod>();
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
            if (!this.settings.EnableSearchDebug())
                return;
            SearchLog.Log.Debug(string.Format("Indexes: {0} - {1}", (object)startIdx, (object)endIdx), (Exception)null);
        }
    }
}
