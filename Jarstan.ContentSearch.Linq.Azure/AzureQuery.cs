using Lucene.Net.Analysis;
using Lucene.Net.Search;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jarstan.ContentSearch.Linq.Azure
{
    public class AzureQuery : IQuery, IDumpable
    {
        public List<QueryMethod> Methods { get; protected set; }

        public Query Query { get; protected set; }

        public Query Filter { get; protected set; }

        public List<IFieldQueryTranslator> VirtualFieldProcessors { get; protected set; }

        public List<FacetQuery> FacetQueries { get; protected set; }

        public List<string> Highlights { get; set; }

        public string HighlightPreTag { get; set; }

        public string HighlightPostTag { get; set; }

        public bool MergeHighlights { get; set; }

        public List<Tuple<string, ComparisonType, Analyzer>> UsedAnalyzers { get; protected set; }

        public List<IExecutionContext> ExecutionContexts { get; protected set; }

        public AzureQuery(Query query, Query filter, IEnumerable<QueryMethod> methods, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors, IEnumerable<FacetQuery> facetQueries, List<string> highlights, string preTag, string postTag, bool mergeHighlights, IEnumerable<Tuple<string, ComparisonType, Analyzer>> usedAnalyzers)
         : this(query, filter, methods, virtualFieldProcessors, facetQueries, highlights, preTag, postTag, mergeHighlights, usedAnalyzers, new List<IExecutionContext>(0))
        {
            
        }
        public AzureQuery(Query query, Query filter, IEnumerable<QueryMethod> methods, IEnumerable<IFieldQueryTranslator> virtualFieldProcessors, IEnumerable<FacetQuery> facetQueries, List<string> highlights, string preTag, string postTag, bool mergeHighlights, IEnumerable<Tuple<string, ComparisonType, Analyzer>> usedAnalyzers, IEnumerable<IExecutionContext> executionContexts)
        {
            this.Query = query;
            this.Filter = filter;
            this.Methods = Enumerable.ToList(methods);
            this.VirtualFieldProcessors = Enumerable.ToList(virtualFieldProcessors);
            this.FacetQueries = Enumerable.ToList(facetQueries);
            this.UsedAnalyzers = Enumerable.ToList(Enumerable.Distinct(usedAnalyzers));
            this.ExecutionContexts = executionContexts != null ? Enumerable.ToList(executionContexts) : new List<IExecutionContext>(0);
            this.Highlights = highlights;
            HighlightPreTag = preTag;
            HighlightPostTag = postTag;
            MergeHighlights = mergeHighlights;
        }

        public override string ToString()
        {
            return ((object)this.Query ?? "[NULL]").ToString();
        }

        void IDumpable.WriteTo(TextWriter writer)
        {
            writer.WriteLine("Query: {0}", this.Query);
            if (this.Filter != null)
                writer.WriteLine("Filter: {0}", this.Filter);
            if (this.UsedAnalyzers.Count > 0)
            {
                writer.WriteLine("Analyzers used:");
                foreach (Tuple<string, ComparisonType, Analyzer> tuple in this.UsedAnalyzers)
                    writer.WriteLine("\tField: {0}, Comparisson: {1}, Analyzer: {2}", tuple.Item1, tuple.Item2, tuple.Item3);
            }
            writer.WriteLine("Method count: {0}", this.Methods.Count);
            for (int index = 0; index < this.Methods.Count; ++index)
                writer.WriteLine("  Method[{0}]: {1}: {2}", index, this.Methods[index].MethodType, this.Methods[index]);
            if (this.FacetQueries.Count > 0)
            {
                writer.WriteLine("Facet query count: {0}", this.FacetQueries.Count);
                foreach (FacetQuery facetQuery in this.FacetQueries)
                    writer.WriteLine("  FacetQuery: {0}", facetQuery);
            }
            if (this.ExecutionContexts.Count <= 0)
                return;
            writer.WriteLine("Execution context count: {0}", this.ExecutionContexts.Count);
            foreach (IExecutionContext executionContext in this.ExecutionContexts)
                writer.WriteLine("  ExecutionContext: {0}", executionContext);
        }
    }
}
