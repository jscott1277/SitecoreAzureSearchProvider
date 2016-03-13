using Azure.ContentSearch.Linq.Lucene;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Extensions;
using Sitecore.ContentSearch.Linq.Helpers;
using Jarstan.ContentSearch.Linq.Azure.Queries.Range;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Jarstan.ContentSearch.Linq.Azure.Queries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jarstan.ContentSearch.Linq.Lucene.Queries;
using Jarstan.ContentSearch.Linq.Methods;

namespace Jarstan.ContentSearch.Linq.Azure
{
    public class AzureQueryMapper : QueryMapper<AzureQuery>
    {
        private readonly QueryParser queryParser;
        private readonly RangeQueryBuilder rangeQueryBuilder;

        public AzureQueryMapper(AzureIndexParameters parameters)
        {
            Parameters = parameters;
            this.ValueFormatter = parameters.ValueFormatter;

            this.queryParser = new QueryParser(global::Lucene.Net.Util.Version.LUCENE_30, null, null)
            {
                AllowLeadingWildcard = true,
                EnablePositionIncrements = true
            };
            this.rangeQueryBuilder = new RangeQueryBuilder();
        }

        public AzureIndexParameters Parameters { get; private set; }

        public QueryParser QueryParser
        {
            get
            {
                return this.queryParser;
            }
        }

        public override AzureQuery MapQuery(IndexQuery query)
        {
            var mappingState = new AzureQueryMapperState(this.Parameters.ExecutionContexts);
            return new AzureQuery(this.Visit(query.RootNode, mappingState), mappingState.FilterQueries, mappingState.AdditionalQueryMethods, mappingState.VirtualFieldProcessors, mappingState.FacetQueries, mappingState.Highlights, mappingState.HighlightPreTag, mappingState.HighlightPostTag, mappingState.MergeHighlights, mappingState.UsedAnalyzers, mappingState.ExecutionContexts);
        }

        protected virtual Query GetFieldQuery(string field, string queryText, AzureQueryMapperState mappingState)
        {
            Analyzer analyzer;
            var fieldTerms = this.GetFieldTerms(field, queryText, out analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(field, ComparisonType.Equal, analyzer));
            if (fieldTerms.Count == 0)
                return new MatchNoDocsQuery();
            if (fieldTerms.Count == 1)
                return new TermQuery(new Term(field, fieldTerms[0]));
            return this.GetPhraseQuery(field, fieldTerms);
        }

        protected virtual List<string> GetFieldTerms(string field, string queryText, out Analyzer analyzer)
        {
            var fieldConfiguration = this.Parameters.GetFieldConfiguration(field);
            analyzer = new KeywordAnalyzer();
            return this.GetFieldTerms(field, queryText, analyzer);
        }

        protected virtual List<string> GetFieldTerms(string field, string queryText, Analyzer analyzer)
        {
            using (var tokenStream = analyzer.TokenStream(field, new StringReader(queryText)))
            {
                var termAttribute = tokenStream.AddAttribute<ITermAttribute>();
                var list = new List<string>();
                while (tokenStream.IncrementToken())
                {
                    string term = termAttribute.Term;
                    list.Add(term);
                }
                tokenStream.End();
                return list;
            }
        }

        protected virtual PhraseQuery GetPhraseQuery(string field, List<string> terms)
        {
            var phraseQuery = new PhraseQuery();
            int position = 0;
            foreach (string txt in terms)
            {
                phraseQuery.Add(new Term(field, txt), position);
                ++position;
            }
            return phraseQuery;
        }

        protected virtual List<KeyValuePair<string, int>> GetTermsWithPositions(string field, string queryText)
        {
            using (TokenStream tokenStream = this.GetAnalyzer(field).TokenStream(field, (TextReader)new StringReader(queryText)))
            {
                var termAttribute = tokenStream.AddAttribute<ITermAttribute>();
                var incrementAttribute = tokenStream.AddAttribute<IPositionIncrementAttribute>();
                var list = new List<KeyValuePair<string, int>>();
                int num = 0;
                bool flag = true;
                while (tokenStream.IncrementToken())
                {
                    var term = termAttribute.Term;
                    if (flag)
                    {
                        num = incrementAttribute.PositionIncrement - 1;
                        flag = false;
                    }
                    else
                        num += incrementAttribute.PositionIncrement;
                    list.Add(new KeyValuePair<string, int>(term, num));
                }
                tokenStream.End();
                return list;
            }
        }

        protected virtual Analyzer GetAnalyzer(string fieldName)
        {
            var fieldConfiguration = this.Parameters.GetFieldConfiguration(fieldName);
            return new KeywordAnalyzer();
        }

        protected virtual Query GetEqualsQuery(string field, string queryText, AzureQueryMapperState mappingState)
        {
            var fieldConfiguration = this.Parameters.GetFieldConfiguration(field);
            var analyzer = this.GetAnalyzer(field);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(field, ComparisonType.Equal, analyzer));
            //if (fieldConfiguration != null && (fieldConfiguration.IndexType == Field.Index.NOT_ANALYZED || fieldConfiguration.IndexType == Field.Index.NOT_ANALYZED_NO_NORMS))
            //    return (Query)new TermQuery(new Term(field, queryText));

            var termsWithPositions = this.GetTermsWithPositions(field, queryText);
            if (termsWithPositions.Count == 0)
                return new MatchNoDocsQuery();
            if (termsWithPositions.Count == 1)
                return new TermQuery(new Term(field, termsWithPositions[0].Key));
            var multiPhraseQuery = new MultiPhraseQuery();
            foreach (var fAnonymousType0 in Enumerable.Select(Enumerable.GroupBy(termsWithPositions, (term => term.Value)), g => new
            {
                Position = g.Key,
                Terms = Enumerable.ToArray(Enumerable.Select(g, (pair => new Term(field, pair.Key))))
            }))
                multiPhraseQuery.Add(fAnonymousType0.Terms, fAnonymousType0.Position);
            return multiPhraseQuery;
        }

        protected virtual void StripAll(AllNode node, List<QueryMethod> methods)
        {
            methods.Add(new AllMethod());
        }

        protected virtual void StripAny(AnyNode node, List<QueryMethod> methods)
        {
            methods.Add(new AnyMethod());
        }

        protected virtual void StripCast(CastNode node, List<QueryMethod> methods)
        {
            methods.Add(new CastMethod(node.TargetType));
        }

        protected virtual void StripCount(CountNode node, List<QueryMethod> methods)
        {
            methods.Add(new CountMethod(node.IsLongCount));
        }

        protected virtual void StripElementAt(ElementAtNode node, List<QueryMethod> methods)
        {
            methods.Add(new ElementAtMethod(node.Index, node.AllowDefaultValue));
        }

        protected virtual void StripFirst(FirstNode node, List<QueryMethod> methods)
        {
            methods.Add(new FirstMethod(node.AllowDefaultValue));
        }

        protected virtual void StripMax(MaxNode node, List<QueryMethod> methods)
        {
            methods.Add(new MaxMethod(node.AllowDefaultValue));
        }

        protected virtual void StripMin(MinNode node, List<QueryMethod> methods)
        {
            methods.Add(new MinMethod(node.AllowDefaultValue));
        }

        protected virtual void StripLast(LastNode node, List<QueryMethod> methods)
        {
            methods.Add(new LastMethod(node.AllowDefaultValue));
        }

        protected virtual void StripOrderBy(OrderByNode node, AzureQueryMapperState mappingState)
        {
            Query query;
            if (this.ProcessAsVirtualField(node.Field, node.SortDirection, 1f, ComparisonType.OrderBy, mappingState, out query))
                return;
            mappingState.AdditionalQueryMethods.Add(new OrderByMethod(node.Field, node.FieldType, node.SortDirection));
        }

        protected virtual void StripSingle(SingleNode node, List<QueryMethod> methods)
        {
            methods.Add(new SingleMethod(node.AllowDefaultValue));
        }

        protected virtual void StripSkip(SkipNode node, List<QueryMethod> methods)
        {
            methods.Add(new SkipMethod(node.Count));
        }

        protected virtual void StripTake(TakeNode node, List<QueryMethod> methods)
        {
            methods.Add(new TakeMethod(node.Count));
        }

        protected virtual void StripSelect(SelectNode node, List<QueryMethod> methods)
        {
            methods.Add(new SelectMethod(node.Lambda, node.FieldNames));
        }

        protected virtual void StripGetResults(GetResultsNode node, List<QueryMethod> methods)
        {
            methods.Add(new GetResultsMethod(node.Options));
        }

        protected virtual void StripGetFacets(GetFacetsNode node, List<QueryMethod> methods)
        {
            methods.Add(new GetFacetsMethod());
        }

        protected virtual void StripGetHighlightResults(GetHighlightResultsNode node, List<QueryMethod> methods, AzureQueryMapperState state)
        {
            methods.Add(new GetHighlightResultsMethod());
            state.HighlightPreTag = node.PreTag;
            state.HighlightPostTag = node.PostTag;
            state.MergeHighlights = node.MergeHighlights;
        }

        protected virtual void StripFacetOn(FacetOnNode node, AzureQueryMapperState state)
        {
            this.ProcessAsVirtualField(node.Field, state);
            state.FacetQueries.Add(new FacetQuery(node.Field, new string[1]
            {
                node.Field
            }, new int?(node.MinimumNumberOfDocuments), node.FilterValues));
        }

        protected virtual void StripFacetPivotOn(FacetPivotOnNode node, AzureQueryMapperState state)
        {
            foreach (string fieldName in node.Fields)
                this.ProcessAsVirtualField(fieldName, state);
            state.FacetQueries.Add(new FacetQuery(null, node.Fields, new int?(node.MinimumNumberOfDocuments), node.FilterValues));
        }

        protected virtual void StripJoin(JoinNode node, AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add(new JoinMethod(node.GetOuterQueryable(), node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
        }

        protected virtual void StripGroupJoin(GroupJoinNode node, AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add(new GroupJoinMethod(node.GetOuterQueryable(), node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
        }

        protected virtual void StripSelfJoin(SelfJoinNode node, AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add(new SelfJoinMethod(node.GetOuterQueryable(), node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression));
        }

        protected virtual void StripSelectMany(SelectManyNode node, AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add(new SelectManyMethod(node.GetSourceQueryable(), node.CollectionSelectorExpression, node.ResultSelectorExpression));
        }

        protected virtual Query Visit(QueryNode node, AzureQueryMapperState mappingState)
        {
            switch (node.NodeType)
            {
                case QueryNodeType.All:
                    this.StripAll((AllNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((AllNode)node).SourceNode, mappingState);
                case QueryNodeType.And:
                    return this.VisitAnd((AndNode)node, mappingState);
                case QueryNodeType.Any:
                    this.StripAny((AnyNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((AnyNode)node).SourceNode, mappingState);
                case QueryNodeType.Between:
                    mappingState.FilterQueries.Add((BaseFilterQuery)this.VisitFilter((FilterNode)node, mappingState));
                    return this.Visit(((FilterNode)node).SourceNode, mappingState);
                //return this.VisitBetween((BetweenNode)node, mappingState);
                case QueryNodeType.Cast:
                    this.StripCast((CastNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((CastNode)node).SourceNode, mappingState);
                case QueryNodeType.Contains:
                    return this.VisitContains((ContainsNode)node, mappingState);
                case QueryNodeType.Count:
                    this.StripCount((CountNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((CountNode)node).SourceNode, mappingState);
                case QueryNodeType.ElementAt:
                    this.StripElementAt((ElementAtNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((ElementAtNode)node).SourceNode, mappingState);
                case QueryNodeType.EndsWith:
                    return this.VisitEndsWith((EndsWithNode)node, mappingState);
                case QueryNodeType.Equal:
                    return this.VisitEqual((EqualNode)node, mappingState);
                case QueryNodeType.Field:
                    return this.VisitField((FieldNode)node, mappingState);
                case QueryNodeType.First:
                    this.StripFirst((FirstNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((FirstNode)node).SourceNode, mappingState);
                case QueryNodeType.GreaterThan:
                    return this.VisitGreaterThan((GreaterThanNode)node, mappingState);
                case QueryNodeType.GreaterThanOrEqual:
                    return this.VisitGreaterThanOrEqual((GreaterThanOrEqualNode)node, mappingState);
                case QueryNodeType.Last:
                    this.StripLast((LastNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((LastNode)node).SourceNode, mappingState);
                case QueryNodeType.LessThan:
                    return this.VisitLessThan((LessThanNode)node, mappingState);
                case QueryNodeType.LessThanOrEqual:
                    return this.VisitLessThanOrEqual((LessThanOrEqualNode)node, mappingState);
                case QueryNodeType.MatchAll:
                    return this.VisitMatchAll((MatchAllNode)node, mappingState);
                case QueryNodeType.MatchNone:
                    return this.VisitMatchNone((MatchNoneNode)node, mappingState);
                case QueryNodeType.Max:
                    this.StripMax((MaxNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((MaxNode)node).SourceNode, mappingState);
                case QueryNodeType.Min:
                    this.StripMin((MinNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((MinNode)node).SourceNode, mappingState);
                case QueryNodeType.Not:
                    return this.VisitNot((NotNode)node, mappingState);
                case QueryNodeType.Or:
                    return this.VisitOr((OrNode)node, mappingState);
                case QueryNodeType.OrderBy:
                    this.StripOrderBy((OrderByNode)node, mappingState);
                    return this.Visit(((OrderByNode)node).SourceNode, mappingState);
                case QueryNodeType.Select:
                    this.StripSelect((SelectNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((SelectNode)node).SourceNode, mappingState);
                case QueryNodeType.Single:
                    this.StripSingle((SingleNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((SingleNode)node).SourceNode, mappingState);
                case QueryNodeType.Skip:
                    this.StripSkip((SkipNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((SkipNode)node).SourceNode, mappingState);
                case QueryNodeType.StartsWith:
                    return this.VisitStartsWith((StartsWithNode)node, mappingState);
                case QueryNodeType.Take:
                    this.StripTake((TakeNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((TakeNode)node).SourceNode, mappingState);
                case QueryNodeType.Where:
                    return this.VisitWhere((WhereNode)node, mappingState);
                case QueryNodeType.Matches:
                    return this.VisitMatches((MatchesNode)node, mappingState);
                case QueryNodeType.Filter:
                    mappingState.FilterQueries.Add((BaseFilterQuery)this.VisitFilter((FilterNode)node, mappingState));
                    return this.Visit(((FilterNode)node).SourceNode, mappingState);
                case QueryNodeType.GetResults:
                    this.StripGetResults((GetResultsNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((GetResultsNode)node).SourceNode, mappingState);
                case QueryNodeType.GetFacets:
                    this.StripGetFacets((GetFacetsNode)node, mappingState.AdditionalQueryMethods);
                    return this.Visit(((GetFacetsNode)node).SourceNode, mappingState);
                case QueryNodeType.FacetOn:
                    this.StripFacetOn((FacetOnNode)node, mappingState);
                    return this.Visit(((FacetOnNode)node).SourceNode, mappingState);
                case QueryNodeType.FacetPivotOn:
                    this.StripFacetPivotOn((FacetPivotOnNode)node, mappingState);
                    return this.Visit(((FacetPivotOnNode)node).SourceNode, mappingState);
                case QueryNodeType.WildcardMatch:
                    return this.VisitWildcardMatch((WildcardMatchNode)node, mappingState);
                case QueryNodeType.Like:
                    return this.VisitLike((LikeNode)node, mappingState);
                case QueryNodeType.Join:
                    this.StripJoin((JoinNode)node, mappingState);
                    return null;
                case QueryNodeType.GroupJoin:
                    this.StripGroupJoin((GroupJoinNode)node, mappingState);
                    return null;
                case QueryNodeType.SelfJoin:
                    this.StripSelfJoin((SelfJoinNode)node, mappingState);
                    return null;
                case QueryNodeType.SelectMany:
                    this.StripSelectMany((SelectManyNode)node, mappingState);
                    return null;
                case QueryNodeType.Custom:
                    var customNode = node as CustomNode;
                    if (customNode != null)
                    {
                        switch(customNode.CustomNodeType)
                        {
                            case CustomQueryNodeTypes.HighlightOn:
                                StripHighlightOn((HighlightOnNode)customNode, mappingState);
                                return this.Visit(((HighlightOnNode)customNode).SourceNode, mappingState);
                            case CustomQueryNodeTypes.GetHighlightResults:
                                this.StripGetHighlightResults((GetHighlightResultsNode)node, mappingState.AdditionalQueryMethods, mappingState);
                                return this.Visit(((GetHighlightResultsNode)customNode).SourceNode, mappingState);
                        }
                    }
                    throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", node.NodeType));
                default:
                    throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", node.NodeType));
            }
        }

        protected virtual void StripHighlightOn(HighlightOnNode node, AzureQueryMapperState state)
        {
            ProcessAsVirtualField(node.Field, state);
            state.Highlights.Add(node.Field);
        }

        protected virtual Query VisitField(FieldNode node, AzureQueryMapperState mappingState)
        {
            if (node.FieldType != typeof(bool))
                throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", (object)node.NodeType));
            var obj = this.ValueFormatter.FormatValueForIndexStorage((object)true, node.FieldKey);
            return this.GetFieldQuery(node.FieldKey, LinqStringExtensions.ToStringOrEmpty(obj), mappingState);
        }

        protected virtual Query VisitAnd(AndNode node, AzureQueryMapperState mappingState)
        {
            var booleanQuery1 = new BooleanQuery();
            var query1 = this.Visit(node.LeftNode, mappingState);
            var query2 = this.Visit(node.RightNode, mappingState);
            var booleanQuery2 = query1 as BooleanQuery;
            if (booleanQuery2 != null && node.LeftNode.NodeType != QueryNodeType.Boost && booleanQuery2.Clauses.TrueForAll((Predicate<BooleanClause>)(clause => clause.Occur == Occur.MUST)))
            {
                foreach (Query query3 in Enumerable.Select<BooleanClause, Query>((IEnumerable<BooleanClause>)booleanQuery2.Clauses, (Func<BooleanClause, Query>)(o => o.Query)))
                    booleanQuery1.Add(query3, Occur.MUST);
            }
            else
            {
                booleanQuery1.Add(query1, Occur.MUST);
            }
            booleanQuery1.Add(query2, Occur.MUST);
            return booleanQuery1;
        }

        protected virtual Query VisitBetween(BetweenNode node, AzureQueryMapperState mappingState)
        {
            var includeLower = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Lower;
            var includeUpper = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Upper;
            var query = this.VisitBetween(node.Field, node.From, node.To, includeLower, includeUpper);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitBetweenFilter(BetweenNode node)
        {
            var query = new BetweenFilterQuery(node.Field, node.From, node.To, node.Inclusion);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitBetween(string fieldName, object fieldFromValue, object fieldToValue, bool includeLower, bool includeUpper)
        {
            return this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            {
                FieldName = fieldName,
                FieldFromValue = fieldFromValue,
                FieldToValue = fieldToValue,
                IncludeLower = includeLower,
                IncludeUpper = includeUpper
            }, this, true);
        }

        protected virtual Query VisitContains(ContainsNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var query = new Queries.RegexQuery(fieldNode.FieldKey, queryText, Queries.RegexQuery.RegexQueryTypes.Contains);
            query.Boost = node.Boost;
            return query;
        }

        private SpanQuery BuildSpanQuery(SpanSubQuery[] subqueries)
        {
            SpanQuery spanQuery1 = null;
            var list = new List<SpanQuery>();
            var spanSubQueryArray = subqueries;
            for (int index = 0; index < spanSubQueryArray.Length; ++index)
            {
                int slop = index > 0 ? spanSubQueryArray[index].Position - spanSubQueryArray[index - 1].Position - 1 : 0;
                var spanQuery2 = spanSubQueryArray[index].CreatorMethod();
                if (slop > 0)
                {
                    SpanNearQuery spanNearQuery1 = null;
                    if (list.Count > 0)
                        spanNearQuery1 = new SpanNearQuery(list.ToArray(), 0, true);
                    if (spanQuery1 == null)
                    {
                        spanQuery1 = new SpanNearQuery(new SpanQuery[2]
                        {
                          spanNearQuery1,
                          spanQuery2
                        }, slop, true);
                    }
                    else
                    {
                        SpanNearQuery spanNearQuery2;
                        if (spanNearQuery1 == null)
                            spanNearQuery2 = new SpanNearQuery(new SpanQuery[2]
                            {
                                spanQuery1,
                                spanQuery2
                            }, slop, true);
                        else
                            spanNearQuery2 = new SpanNearQuery(new SpanQuery[3]
                            {
                                spanQuery1,
                                spanNearQuery1,
                                spanQuery2
                            }, slop, true);
                        spanQuery1 = spanNearQuery2;
                    }
                    list = new List<SpanQuery>();
                }
                else
                    list.Add(spanQuery2);
            }
            if (spanQuery1 == null && list.Count > 0)
                spanQuery1 = new SpanNearQuery(list.ToArray(), 0, true);
            return spanQuery1;
        }

        private SpanQuery GetSpanQuery(string fieldName, IEnumerable<string> terms, bool isWildcard)
        {
            if (isWildcard || Enumerable.Count<string>(terms) > 1)
                return this.GetSpanWildcardQuery(fieldName, terms);
            return new SpanTermQuery(new Term(fieldName, Enumerable.First(terms)));
        }

        private SpanWildcardQuery GetSpanWildcardQuery(string fieldName, IEnumerable<string> terms)
        {
            return new SpanWildcardQuery(Enumerable.Select(terms, (z => new Term(fieldName, z))));
        }

        private SpanNearQuery GetSpanNearQuery(string fieldName, IEnumerable<string> terms, int slop = 0, bool direction = true)
        {
            return new SpanNearQuery(Enumerable.ToArray(Enumerable.Cast<SpanQuery>(Enumerable.Select(terms, (z => new SpanTermQuery(new Term(fieldName, z)))))), slop, direction);
        }

        protected virtual Query VisitEndsWith(EndsWithNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var query = new Queries.RegexQuery(fieldNode.FieldKey, queryText, Queries.RegexQuery.RegexQueryTypes.EndsWith);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitEqual(EqualNode node, AzureQueryMapperState mappingState)
        {
            if (node.LeftNode is ConstantNode && node.RightNode is ConstantNode)
                return new BooleanQuery()
                {
                  {
                    new MatchAllDocsQuery(),
                    ((ConstantNode) node.LeftNode).Value.Equals(((ConstantNode) node.RightNode).Value) ? Occur.MUST : Occur.MUST_NOT
                  }
                };
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = !(fieldNode.FieldType != typeof(string)) ? QueryHelper.GetValueNode<object>(node) : QueryHelper.GetValueNode(node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.Equal, mappingState, out query))
                return query;
            var fieldKey = fieldNode.FieldKey;
            var obj = valueNode.Value;
            var boost = node.Boost;
            var fieldConfiguration = this.Parameters.FieldMap != null ? this.Parameters.FieldMap.GetFieldConfiguration(fieldKey) : (AbstractSearchFieldConfiguration)null;
            if (fieldConfiguration != null)
                obj = fieldConfiguration.FormatForWriting(obj);
            if (obj == null)
                throw new NotSupportedException("Comparison of null values is not supported.");
            return this.VisitEqual(fieldKey, obj, boost, mappingState);
        }

        protected virtual Query VisitEqual(string fieldName, object fieldValue, float fieldBoost, AzureQueryMapperState mappingState)
        {
            var query = this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            {
                FieldName = fieldName,
                Boost = fieldBoost,
                FieldFromValue = fieldValue,
                FieldToValue = fieldValue,
                IncludeLower = true,
                IncludeUpper = true
            }, this, false);
            if (query != null)
                return query;
            var obj = this.ValueFormatter.FormatValueForIndexStorage(fieldValue, fieldName);
            var fieldConfiguration = this.Parameters.FieldMap != null ? this.Parameters.FieldMap.GetFieldConfiguration(fieldName) : null;
            if (fieldConfiguration != null)
            {
                if (obj as string == string.Empty && fieldConfiguration.Attributes.ContainsKey("emptyString"))
                    obj = (object)fieldConfiguration.Attributes["emptyString"];
                else if (obj == null && fieldConfiguration.Attributes.ContainsKey("nullValue"))
                    obj = (object)fieldConfiguration.Attributes["nullValue"];
            }
            if (obj == null)
                throw new NotSupportedException("Comparison of null values is not supported.");

            var val = LinqStringExtensions.ToStringOrEmpty(obj);
            if (val.Contains(" "))
            {
                val = "\"" + val + "\"";
            }
            var equalsQuery = this.GetEqualsQuery(fieldName, val, mappingState);
            equalsQuery.Boost = fieldBoost;
            return equalsQuery;
        }

        protected virtual Query VisitLessThanOrEqual(LessThanOrEqualNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.LessThanOrEqual, mappingState, out query))
                return query;
            return this.VisitLessThanOrEqual(fieldNode.FieldKey, valueNode.Value, node.Boost);
        }

        protected virtual Query VisitLessThanOrEqual(string fieldName, object fieldValue, float fieldBoost)
        {
            return this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            {
                FieldName = fieldName,
                Boost = fieldBoost,
                FieldFromValue = null,
                FieldToValue = fieldValue,
                IncludeLower = true,
                IncludeUpper = true
            }, this, true);
        }

        protected virtual Query VisitLessThan(LessThanNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.LessThan, mappingState, out query))
                return query;
            return this.VisitLessThan(fieldNode.FieldKey, valueNode.Value, node.Boost);
        }

        protected virtual Query VisitLessThan(string fieldName, object fieldValue, float fieldBoost)
        {
            return this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            {
                FieldName = fieldName,
                Boost = fieldBoost,
                FieldFromValue = (object)null,
                FieldToValue = fieldValue,
                IncludeLower = true,
                IncludeUpper = false
            }, this, true);
        }

        protected virtual Query VisitGreaterThanOrEqual(GreaterThanOrEqualNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.GreaterThanOrEqual, mappingState, out query))
                return query;
            return this.VisitGreaterThanOrEqual(fieldNode.FieldKey, valueNode.Value, node.Boost);
        }

        protected virtual Query VisitGreaterThanOrEqual(string fieldName, object fieldValue, float fieldBoost)
        {
            return this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            {
                FieldName = fieldName,
                Boost = fieldBoost,
                FieldFromValue = fieldValue,
                FieldToValue = (object)null,
                IncludeLower = true,
                IncludeUpper = true
            }, this, true);
        }

        protected virtual Query VisitGreaterThan(GreaterThanNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.GreaterThan, mappingState, out query))
                return query;
            return this.VisitGreaterThan(fieldNode.FieldKey, valueNode.Value, node.Boost);
        }

        protected virtual Query VisitGreaterThan(string fieldName, object fieldValue, float fieldBoost)
        {
            throw new NotSupportedException("Azure Search does not support Range Queries.");
            //return this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
            //{
            //    FieldName = fieldName,
            //    Boost = fieldBoost,
            //    FieldFromValue = fieldValue,
            //    FieldToValue = (object)null,
            //    IncludeLower = false,
            //    IncludeUpper = true
            //}, this, true);
        }

        protected virtual Query VisitMatchAll(MatchAllNode node, AzureQueryMapperState mappingState)
        {
            return new MatchAllDocsQuery();
        }

        protected virtual Query VisitMatchNone(MatchNoneNode node, AzureQueryMapperState mappingState)
        {
            return new BooleanQuery()
              {
                {
                  new MatchAllDocsQuery(),
                  Occur.MUST_NOT
                }
              };
        }

        protected virtual Query VisitNot(NotNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode((BinaryNode)node.Operand);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>((BinaryNode)node.Operand).Value, fieldNode.FieldKey).ToString();
            return new NotEqualQuery(fieldNode.FieldKey, queryText);
        }

        protected virtual Query VisitOr(OrNode node, AzureQueryMapperState mappingState)
        {
            var booleanQuery1 = new BooleanQuery();
            var query1 = this.Visit(node.LeftNode, mappingState);
            var query2 = this.Visit(node.RightNode, mappingState);
            var booleanQuery2 = query1 as BooleanQuery;
            if (booleanQuery2 != null && booleanQuery2.Clauses.TrueForAll(o => o.Occur == Occur.SHOULD))
            {
                foreach (var query3 in Enumerable.Select(booleanQuery2.Clauses, (o => o.Query)))
                    booleanQuery1.Add(query3, Occur.SHOULD);
            }
            else
                booleanQuery1.Add(query1, Occur.SHOULD);
            booleanQuery1.Add(query2, Occur.SHOULD);
            return booleanQuery1;
        }

        protected virtual Query VisitStartsWith(StartsWithNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var query = new Queries.RegexQuery(fieldNode.FieldKey, queryText, Queries.RegexQuery.RegexQueryTypes.StartsWith);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitWhere(WhereNode node, AzureQueryMapperState mappingState)
        {
            var booleanQuery = new BooleanQuery();
            var query1 = this.Visit(node.PredicateNode, mappingState);
            var query2 = this.Visit(node.SourceNode, mappingState);
            if (query1 is MatchAllDocsQuery && query2 is MatchAllDocsQuery)
            {
                booleanQuery.Add(query1, Occur.MUST);
            }
            else
            {
                if (!(query1 is MatchAllDocsQuery))
                    booleanQuery.Add(query1, Occur.MUST);
                if (!(query2 is MatchAllDocsQuery))
                    booleanQuery.Add(query2, Occur.MUST);
            }
            return booleanQuery;
        }

        protected Query VisitMatches(MatchesNode node, AzureQueryMapperState mappingState)
        {
            var regexQuery = new Contrib.Regex.RegexQuery(new Term(QueryHelper.GetFieldNode(node).FieldKey, LinqStringExtensions.ToStringOrEmpty(QueryHelper.GetValueNode<string>(node).Value)));
            if (node.RegexOptions != null)
            {
                if (!TypeExtensions.IsAssignableTo(((ConstantNode)node.RegexOptions).Type, typeof(RegexOptions)))
                    throw new NotSupportedException(string.Format("The regex options part of the '{0}' was expected to be of type '{1}', but was of type '{2}'.", node.GetType().Name, typeof(RegexOptions).FullName, ((ConstantNode)node.RegexOptions).Type.FullName));
                var regexOptions = (RegexOptions)((ConstantNode)node.RegexOptions).Value;
                regexQuery.RegexImplementation = new AzureRegexCapabilities(regexOptions);
            }
            regexQuery.Boost = node.Boost;
            return regexQuery;
        }

        protected Query VisitWildcardMatch(WildcardMatchNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var fieldConfiguration = this.Parameters.GetFieldConfiguration(fieldNode.FieldKey);
            var analyzer = new KeywordAnalyzer();
            var list1 = this.GetFieldTerms(fieldNode.FieldKey, queryText, analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.MatchWildcard, analyzer));
            Query query;
            if (list1.Count > 1)
            {
                var list2 = new List<SpanQuery>();
                for (int index = 0; index < list1.Count; ++index)
                    list2.Add(new SpanWildcardQuery(new Term(fieldNode.FieldKey, list1[index])));
                query = new SpanNearQuery(list2.ToArray(), 0, true);
            }
            else
                query = list1.Count != 1 ? new MatchNoDocsQuery() : (Query)new WildcardQuery(new Term(fieldNode.FieldKey, list1[0]));
            query.Boost = node.Boost;
            return query;
        }

        protected Query VisitLike(LikeNode node, AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var queryText = this.NormalizeValue(this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString());
            Analyzer analyzer;
            var fieldTerms = this.GetFieldTerms(fieldNode.FieldKey, queryText, out analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.Like, analyzer));
            Query query;
            if (fieldTerms.Count > 1)
            {
                var list = new List<SpanQuery>();
                for (int index = 0; index < fieldTerms.Count; ++index)
                    list.Add(new SpanFuzzyQuery(new Term(fieldNode.FieldKey, fieldTerms[index]), node.MinimumSimilarity));
                query = new SpanNearQuery(list.ToArray(), node.Slop, true);
            }
            else
                query = fieldTerms.Count != 1 ? new MatchNoDocsQuery() : (Query)new FuzzyQuery(new Term(fieldNode.FieldKey, fieldTerms[0]), node.MinimumSimilarity);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitFilter(FilterNode node, AzureQueryMapperState mappingState)
        {
            var predNode = node.PredicateNode;
            switch (predNode.NodeType)
            {
                case QueryNodeType.Between:
                    return VisitBetweenFilter((BetweenNode)predNode);
                case QueryNodeType.Equal:
                    return VisitEqualFilter((EqualNode)predNode);
                case QueryNodeType.GreaterThan:
                    return VisitGreaterThanFilter((GreaterThanNode)predNode);
                case QueryNodeType.GreaterThanOrEqual:
                    return VisitGreaterThanOrEqualFilter((GreaterThanOrEqualNode)predNode);
                case QueryNodeType.LessThan:
                    return VisitLessThanFilter((LessThanNode)predNode);
                case QueryNodeType.LessThanOrEqual:
                    return VisitLessThanOrEqualFilter((LessThanOrEqualNode)predNode);
                case QueryNodeType.Not:
                case QueryNodeType.NotEqual:
                    return VisitNotFilter((NotNode)predNode);
                default:
                    throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", (object)node.NodeType));
            }   
        }

        protected virtual Query VisitLessThanOrEqualFilter(LessThanOrEqualNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            return new FilterQuery(fieldNode.FieldKey, valueNode.Value, FilterQuery.FilterQueryTypes.LessThanEquals);
        }

        protected virtual Query VisitLessThanFilter(LessThanNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            return new FilterQuery(fieldNode.FieldKey, valueNode.Value, FilterQuery.FilterQueryTypes.LessThan);
        }

        protected virtual Query VisitGreaterThanOrEqualFilter(GreaterThanOrEqualNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            return new FilterQuery(fieldNode.FieldKey, valueNode.Value, FilterQuery.FilterQueryTypes.GreaterThanEquals);
        }

        protected virtual Query VisitGreaterThanFilter(GreaterThanNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = QueryHelper.GetValueNode(node, fieldNode.FieldType);
            return new FilterQuery(fieldNode.FieldKey, valueNode.Value, FilterQuery.FilterQueryTypes.GreaterThan);
        }

        protected virtual Query VisitNotFilter(NotNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode((BinaryNode)node.Operand);
            var queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>((BinaryNode)node.Operand).Value, fieldNode.FieldKey).ToString();
            return new FilterQuery(fieldNode.FieldKey, queryText, FilterQuery.FilterQueryTypes.NotEquals);
        }

        protected virtual Query VisitEqualFilter(EqualNode node)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            var valueNode = !(fieldNode.FieldType != typeof(string)) ? QueryHelper.GetValueNode<object>(node) : QueryHelper.GetValueNode(node, fieldNode.FieldType);
            return new FilterQuery(fieldNode.FieldKey, valueNode.Value, FilterQuery.FilterQueryTypes.Equals);
        }

        protected bool ProcessAsVirtualField(string fieldName, AzureQueryMapperState mappingState)
        {
            if (this.Parameters.FieldQueryTranslators == null)
                return false;
            var translator = this.Parameters.FieldQueryTranslators.GetTranslator(fieldName.ToLowerInvariant());
            if (translator == null)
                return false;
            mappingState.VirtualFieldProcessors.Add(translator);
            return true;
        }

        protected virtual bool ProcessAsVirtualField(FieldNode fieldNode, ConstantNode valueNode, float boost, ComparisonType comparison, AzureQueryMapperState mappingState, out Query query)
        {
            return this.ProcessAsVirtualField(fieldNode.FieldKey, valueNode.Value, boost, comparison, mappingState, out query);
        }

        protected bool ProcessAsVirtualField(string fieldName, object fieldValue, float boost, ComparisonType comparison, AzureQueryMapperState mappingState, out Query query)
        {
            query = null;
            if (this.Parameters.FieldQueryTranslators == null)
                return false;
            var translator = this.Parameters.FieldQueryTranslators.GetTranslator(fieldName.ToLowerInvariant());
            if (translator == null)
                return false;
            var translatedFieldQuery = translator.TranslateFieldQuery(fieldName, fieldValue, comparison, this.Parameters.FieldNameTranslator);
            if (translatedFieldQuery == null)
                return false;
            var booleanQuery = new BooleanQuery();
            if (translatedFieldQuery.FieldComparisons != null)
            {
                foreach (Tuple<string, object, ComparisonType> tuple in translatedFieldQuery.FieldComparisons)
                {
                    string indexFieldName = this.Parameters.FieldNameTranslator.GetIndexFieldName(tuple.Item1);
                    switch (tuple.Item3)
                    {
                        case ComparisonType.Equal:
                            booleanQuery.Add(this.VisitEqual(indexFieldName, tuple.Item2, boost, mappingState), Occur.MUST);
                            continue;
                        case ComparisonType.LessThan:
                            booleanQuery.Add(this.VisitLessThan(indexFieldName, tuple.Item2, boost), Occur.MUST);
                            continue;
                        case ComparisonType.LessThanOrEqual:
                            booleanQuery.Add(this.VisitLessThanOrEqual(indexFieldName, tuple.Item2, boost), Occur.MUST);
                            continue;
                        case ComparisonType.GreaterThan:
                            booleanQuery.Add(this.VisitGreaterThan(indexFieldName, tuple.Item2, boost), Occur.MUST);
                            continue;
                        case ComparisonType.GreaterThanOrEqual:
                            booleanQuery.Add(this.VisitGreaterThanOrEqual(indexFieldName, tuple.Item2, boost), Occur.MUST);
                            continue;
                        default:
                            throw new InvalidOperationException("Unsupported comparison type: " + (object)tuple.Item3);
                    }
                }
            }
            if (translatedFieldQuery.QueryMethods != null)
                mappingState.AdditionalQueryMethods.AddRange(translatedFieldQuery.QueryMethods);
            mappingState.VirtualFieldProcessors.Add(translator);
            query = booleanQuery;
            return true;
        }

        protected string NormalizeValue(string value)
        {
            if (this.queryParser.LowercaseExpandedTerms)
                value = value.ToLower(this.queryParser.Locale);
            return value;
        }

        protected class AzureQueryMapperState
        {
            public List<QueryMethod> AdditionalQueryMethods { get; set; }

            public FiltersListQuery FilterQueries { get; set; }

            public List<IFieldQueryTranslator> VirtualFieldProcessors { get; set; }

            public List<FacetQuery> FacetQueries { get; set; }

            public List<Tuple<string, ComparisonType, Analyzer>> UsedAnalyzers { get; set; }

            public List<IExecutionContext> ExecutionContexts { get; set; }

            public List<string> Highlights { get; set; }

            public string HighlightPreTag { get; set; }
            public string HighlightPostTag { get; set; }

            public bool MergeHighlights { get; set; }

            public AzureQueryMapperState(IEnumerable<IExecutionContext> executionContexts)
            {
                this.AdditionalQueryMethods = new List<QueryMethod>();
                this.VirtualFieldProcessors = new List<IFieldQueryTranslator>();
                this.FacetQueries = new List<FacetQuery>();
                this.UsedAnalyzers = new List<Tuple<string, ComparisonType, Analyzer>>();
                this.ExecutionContexts = executionContexts != null ? Enumerable.ToList(executionContexts) : new List<IExecutionContext>();
                this.FilterQueries = new FiltersListQuery();
                this.Highlights = new List<string>();
            }
        }
    }
}
