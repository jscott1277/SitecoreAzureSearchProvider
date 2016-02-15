using Azure.ContentSearch.Linq.Lucene;
using Contrib.Regex;
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
using Slalom.ContentSearch.Linq.Azure.Queries.Range;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Slalom.ContentSearch.Linq.Azure.Queries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Slalom.ContentSearch.Linq.Lucene.Queries;

namespace Slalom.ContentSearch.Linq.Azure
{
    public class AzureQueryMapper : QueryMapper<AzureQuery>
    {
        private readonly QueryParser queryParser;
        private readonly RangeQueryBuilder rangeQueryBuilder;

        public AzureQueryMapper(AzureIndexParameters parameters)
        {
            Parameters = parameters;
            this.ValueFormatter = parameters.ValueFormatter;

            this.queryParser = new QueryParser(global::Lucene.Net.Util.Version.LUCENE_30, (string)null, null)
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
            var mappingState = new AzureQueryMapper.AzureQueryMapperState((IEnumerable<IExecutionContext>)this.Parameters.ExecutionContexts);
            return new AzureQuery(this.Visit(query.RootNode, mappingState), mappingState.FilterQuery, mappingState.AdditionalQueryMethods, mappingState.VirtualFieldProcessors, mappingState.FacetQueries, mappingState.UsedAnalyzers, mappingState.ExecutionContexts);
        }
        protected virtual Query GetFieldQuery(string field, string queryText, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            Analyzer analyzer;
            List<string> fieldTerms = this.GetFieldTerms(field, queryText, out analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(field, ComparisonType.Equal, analyzer));
            if (fieldTerms.Count == 0)
                return (Query)new MatchNoDocsQuery();
            if (fieldTerms.Count == 1)
                return (Query)new TermQuery(new Term(field, fieldTerms[0]));
            return (Query)this.GetPhraseQuery(field, fieldTerms);
        }

        protected virtual List<string> GetFieldTerms(string field, string queryText, out Analyzer analyzer)
        {
            IAzureSearchFieldConfiguration fieldConfiguration = this.Parameters.GetFieldConfiguration(field);
            analyzer = new KeywordAnalyzer();
            return this.GetFieldTerms(field, queryText, analyzer);
        }

        protected virtual List<string> GetFieldTerms(string field, string queryText, Analyzer analyzer)
        {
            using (TokenStream tokenStream = analyzer.TokenStream(field, (TextReader)new StringReader(queryText)))
            {
                ITermAttribute termAttribute = tokenStream.AddAttribute<ITermAttribute>();
                List<string> list = new List<string>();
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
            PhraseQuery phraseQuery = new PhraseQuery();
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
                ITermAttribute termAttribute = tokenStream.AddAttribute<ITermAttribute>();
                IPositionIncrementAttribute incrementAttribute = tokenStream.AddAttribute<IPositionIncrementAttribute>();
                List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
                int num = 0;
                bool flag = true;
                while (tokenStream.IncrementToken())
                {
                    string term = termAttribute.Term;
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
            IAzureSearchFieldConfiguration fieldConfiguration = this.Parameters.GetFieldConfiguration(fieldName);
            return new KeywordAnalyzer();
        }

        protected virtual Query GetEqualsQuery(string field, string queryText, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            IAzureSearchFieldConfiguration fieldConfiguration = this.Parameters.GetFieldConfiguration(field);
            Analyzer analyzer = this.GetAnalyzer(field);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(field, ComparisonType.Equal, analyzer));
            //if (fieldConfiguration != null && (fieldConfiguration.IndexType == Field.Index.NOT_ANALYZED || fieldConfiguration.IndexType == Field.Index.NOT_ANALYZED_NO_NORMS))
            //    return (Query)new TermQuery(new Term(field, queryText));

            var termsWithPositions = this.GetTermsWithPositions(field, queryText);
            if (termsWithPositions.Count == 0)
                return (Query)new MatchNoDocsQuery();
            if (termsWithPositions.Count == 1)
                return (Query)new TermQuery(new Term(field, termsWithPositions[0].Key));
            MultiPhraseQuery multiPhraseQuery = new MultiPhraseQuery();
            foreach (var fAnonymousType0 in Enumerable.Select(Enumerable.GroupBy<KeyValuePair<string, int>, int>((IEnumerable<KeyValuePair<string, int>>)termsWithPositions, (Func<KeyValuePair<string, int>, int>)(term => term.Value)), g => new
            {
                Position = g.Key,
                Terms = Enumerable.ToArray<Term>(Enumerable.Select<KeyValuePair<string, int>, Term>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, Term>)(pair => new Term(field, pair.Key))))
            }))
                multiPhraseQuery.Add(fAnonymousType0.Terms, fAnonymousType0.Position);
            return multiPhraseQuery;
        }

        protected virtual void StripAll(AllNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new AllMethod());
        }

        protected virtual void StripAny(AnyNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new AnyMethod());
        }

        protected virtual void StripCast(CastNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new CastMethod(node.TargetType));
        }

        protected virtual void StripCount(CountNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new CountMethod(node.IsLongCount));
        }

        protected virtual void StripElementAt(ElementAtNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new ElementAtMethod(node.Index, node.AllowDefaultValue));
        }

        protected virtual void StripFirst(FirstNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new FirstMethod(node.AllowDefaultValue));
        }

        protected virtual void StripMax(MaxNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new MaxMethod(node.AllowDefaultValue));
        }

        protected virtual void StripMin(MinNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new MinMethod(node.AllowDefaultValue));
        }

        protected virtual void StripLast(LastNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new LastMethod(node.AllowDefaultValue));
        }

        protected virtual void StripOrderBy(OrderByNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            Query query;
            if (this.ProcessAsVirtualField(node.Field, (object)node.SortDirection, 1f, ComparisonType.OrderBy, mappingState, out query))
                return;
            mappingState.AdditionalQueryMethods.Add((QueryMethod)new OrderByMethod(node.Field, node.FieldType, node.SortDirection));
        }

        protected virtual void StripSingle(SingleNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new SingleMethod(node.AllowDefaultValue));
        }

        protected virtual void StripSkip(SkipNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new SkipMethod(node.Count));
        }

        protected virtual void StripTake(TakeNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new TakeMethod(node.Count));
        }

        protected virtual void StripSelect(SelectNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new SelectMethod(node.Lambda, node.FieldNames));
        }

        protected virtual void StripGetResults(GetResultsNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new GetResultsMethod(node.Options));
        }

        protected virtual void StripGetFacets(GetFacetsNode node, List<QueryMethod> methods)
        {
            methods.Add((QueryMethod)new GetFacetsMethod());
        }

        protected virtual void StripFacetOn(FacetOnNode node, AzureQueryMapper.AzureQueryMapperState state)
        {
            this.ProcessAsVirtualField(node.Field, state);
            state.FacetQueries.Add(new FacetQuery(node.Field, (IEnumerable<string>)new string[1]
            {
        node.Field
            }, new int?(node.MinimumNumberOfDocuments), node.FilterValues));
        }

        protected virtual void StripFacetPivotOn(FacetPivotOnNode node, AzureQueryMapper.AzureQueryMapperState state)
        {
            foreach (string fieldName in node.Fields)
                this.ProcessAsVirtualField(fieldName, state);
            state.FacetQueries.Add(new FacetQuery((string)null, (IEnumerable<string>)node.Fields, new int?(node.MinimumNumberOfDocuments), node.FilterValues));
        }

        protected virtual void StripJoin(JoinNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add((QueryMethod)new JoinMethod((IEnumerable)node.GetOuterQueryable(), (IEnumerable)node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
        }

        protected virtual void StripGroupJoin(GroupJoinNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add((QueryMethod)new GroupJoinMethod((IEnumerable)node.GetOuterQueryable(), (IEnumerable)node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer));
        }

        protected virtual void StripSelfJoin(SelfJoinNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add((QueryMethod)new SelfJoinMethod((IEnumerable)node.GetOuterQueryable(), (IEnumerable)node.GetInnerQueryable(), node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression));
        }

        protected virtual void StripSelectMany(SelectManyNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            mappingState.AdditionalQueryMethods.Add((QueryMethod)new SelectManyMethod((IEnumerable)node.GetSourceQueryable(), node.CollectionSelectorExpression, node.ResultSelectorExpression));
        }

        protected virtual Query Visit(QueryNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
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
                    return this.VisitBetween((BetweenNode)node, mappingState);
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
                    if (mappingState.FilterQuery == null)
                        mappingState.FilterQuery = this.VisitFilter((FilterNode)node, mappingState);
                    else
                        mappingState.FilterQuery = (Query)new BooleanQuery()
            {
              {
                mappingState.FilterQuery,
                Occur.MUST
              },
              {
                this.VisitFilter((FilterNode) node, mappingState),
                Occur.MUST
              }
            };
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
                    return (Query)null;
                case QueryNodeType.GroupJoin:
                    this.StripGroupJoin((GroupJoinNode)node, mappingState);
                    return (Query)null;
                case QueryNodeType.SelfJoin:
                    this.StripSelfJoin((SelfJoinNode)node, mappingState);
                    return (Query)null;
                case QueryNodeType.SelectMany:
                    this.StripSelectMany((SelectManyNode)node, mappingState);
                    return (Query)null;
                default:
                    throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", (object)node.NodeType));
            }
        }

        protected virtual Query VisitField(FieldNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            if (node.FieldType != typeof(bool))
                throw new NotSupportedException(string.Format("The query node type '{0}' is not supported in this context.", (object)node.NodeType));
            object obj = this.ValueFormatter.FormatValueForIndexStorage((object)true, node.FieldKey);
            return this.GetFieldQuery(node.FieldKey, LinqStringExtensions.ToStringOrEmpty(obj), mappingState);
        }

        protected virtual Query VisitAnd(AndNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            BooleanQuery booleanQuery1 = new BooleanQuery();
            Query query1 = this.Visit(node.LeftNode, mappingState);
            Query query2 = this.Visit(node.RightNode, mappingState);
            BooleanQuery booleanQuery2 = query1 as BooleanQuery;
            if (booleanQuery2 != null && node.LeftNode.NodeType != QueryNodeType.Boost && booleanQuery2.Clauses.TrueForAll((Predicate<BooleanClause>)(clause => clause.Occur == Occur.MUST)))
            {
                foreach (Query query3 in Enumerable.Select<BooleanClause, Query>((IEnumerable<BooleanClause>)booleanQuery2.Clauses, (Func<BooleanClause, Query>)(o => o.Query)))
                    booleanQuery1.Add(query3, Occur.MUST);
            }
            else
                booleanQuery1.Add(query1, Occur.MUST);
            booleanQuery1.Add(query2, Occur.MUST);
            return (Query)booleanQuery1;
        }

        protected virtual Query VisitBetween(BetweenNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            bool includeLower = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Lower;
            bool includeUpper = node.Inclusion == Inclusion.Both || node.Inclusion == Inclusion.Upper;
            Query query = this.VisitBetween(node.Field, node.From, node.To, includeLower, includeUpper);
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

        protected virtual Query VisitContains(ContainsNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode(node);
            ConstantNode valueNode = QueryHelper.GetValueNode<string>(node);
            object obj = base.ValueFormatter.FormatValueForIndexStorage(valueNode.Value, fieldNode.FieldKey);
            string text = obj.ToString();
            Analyzer analyzer = this.GetAnalyzer(fieldNode.FieldKey);
            Query query;
            if (text.Length > 0 && text != "*")
            {
                var terms = this.GetTermsWithPositions(fieldNode.FieldKey, text);
                mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.Contains, analyzer));
                if (terms.Count > 1)
                {
                    IEnumerable<SpanSubQuery> source = from term in terms
                                                       group term by term.Value into g
                                                       select new SpanSubQuery
                                                       {
                                                           IsWildcard = g.Key == terms.Last<KeyValuePair<string, int>>().Value || g.Key == terms.First<KeyValuePair<string, int>>().Value,
                                                           Position = g.Key,
                                                           CreatorMethod = delegate
                                                           {
                                                               if (g.Key == terms.First<KeyValuePair<string, int>>().Value)
                                                               {
                                                                   return this.GetSpanQuery(fieldNode.FieldKey, from pair in g
                                                                                                                select "*" + pair.Key, true);
                                                               }
                                                               if (g.Key == terms.Last<KeyValuePair<string, int>>().Value)
                                                               {
                                                                   return this.GetSpanQuery(fieldNode.FieldKey, from pair in g
                                                                                                                select pair.Key + "*", true);
                                                               }
                                                               return this.GetSpanQuery(fieldNode.FieldKey, from pair in g
                                                                                                            select pair.Key, false);
                                                           }
                                                       };
                    query = this.BuildSpanQuery(source.ToArray<SpanSubQuery>());
                }
                else if (terms.Count == 1)
                {
                    query = new TermQuery(new Term(fieldNode.FieldKey, "/.*" + terms[0].Key + ".*/"));
                }
                else
                {
                    query = new TermQuery(new Term(fieldNode.FieldKey, "/.*" + text.ToLowerInvariant() + ".*/"));
                }
            }
            else
            {
                query = new TermQuery(new Term(fieldNode.FieldKey, "*"));
            }
            query.Boost = node.Boost;
            return query;
        }

        private SpanQuery BuildSpanQuery(SpanSubQuery[] subqueries)
        {
            SpanQuery spanQuery1 = (SpanQuery)null;
            List<SpanQuery> list = new List<SpanQuery>();
            SpanSubQuery[] spanSubQueryArray = subqueries;
            for (int index = 0; index < spanSubQueryArray.Length; ++index)
            {
                int slop = index > 0 ? spanSubQueryArray[index].Position - spanSubQueryArray[index - 1].Position - 1 : 0;
                SpanQuery spanQuery2 = spanSubQueryArray[index].CreatorMethod();
                if (slop > 0)
                {
                    SpanNearQuery spanNearQuery1 = (SpanNearQuery)null;
                    if (list.Count > 0)
                        spanNearQuery1 = new SpanNearQuery(list.ToArray(), 0, true);
                    if (spanQuery1 == null)
                    {
                        spanQuery1 = (SpanQuery)new SpanNearQuery(new SpanQuery[2]
                        {
              (SpanQuery) spanNearQuery1,
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
                (SpanQuery) spanNearQuery1,
                spanQuery2
                            }, slop, true);
                        spanQuery1 = (SpanQuery)spanNearQuery2;
                    }
                    list = new List<SpanQuery>();
                }
                else
                    list.Add(spanQuery2);
            }
            if (spanQuery1 == null && list.Count > 0)
                spanQuery1 = (SpanQuery)new SpanNearQuery(list.ToArray(), 0, true);
            return spanQuery1;
        }

        private SpanQuery GetSpanQuery(string fieldName, IEnumerable<string> terms, bool isWildcard)
        {
            if (isWildcard || Enumerable.Count<string>(terms) > 1)
                return (SpanQuery)this.GetSpanWildcardQuery(fieldName, terms);
            return (SpanQuery)new SpanTermQuery(new Term(fieldName, Enumerable.First<string>(terms)));
        }

        private SpanWildcardQuery GetSpanWildcardQuery(string fieldName, IEnumerable<string> terms)
        {
            return new SpanWildcardQuery(Enumerable.Select<string, Term>(terms, (Func<string, Term>)(z => new Term(fieldName, z))));
        }

        private SpanNearQuery GetSpanNearQuery(string fieldName, IEnumerable<string> terms, int slop = 0, bool direction = true)
        {
            return new SpanNearQuery(Enumerable.ToArray<SpanQuery>(Enumerable.Cast<SpanQuery>((IEnumerable)Enumerable.Select<string, SpanTermQuery>(terms, (Func<string, SpanTermQuery>)(z => new SpanTermQuery(new Term(fieldName, z)))))), slop, direction);
        }

        protected virtual Query VisitEndsWith(EndsWithNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode(node);
            string queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var query = new TermQuery(new Term(fieldNode.FieldKey, "/.*" + queryText + "/"));

            //mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.EndsWith, analyzer));
            //Query query = terms.Count <= 1 ? (terms.Count != 1 ? (Query)new MatchNoDocsQuery() : (Query)new SpanLastQuery(new SpanTermQuery(new Term(fieldNode.FieldKey, "/.*" + terms[0].Key + "/")), analyzer)) : (Query)this.BuildSpanQuery(Enumerable.ToArray(Enumerable.Select(Enumerable.GroupBy(terms, term => term.Value), (g => new SpanSubQuery()
            //{
            //    IsWildcard = g.Key == Enumerable.First<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value,
            //    Position = g.Key,
            //    CreatorMethod = (Func<SpanQuery>)(() =>
            //    {
            //        if (g.Key == Enumerable.First<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value)
            //            return this.GetSpanQuery(fieldNode.FieldKey, Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => "*" + pair.Key)), true);
            //        if (g.Key == Enumerable.Last<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value)
            //            return (SpanQuery)new SpanLastQuery((SpanQuery)new SpanTermQuery(new Term(fieldNode.FieldKey, Enumerable.First<string>(Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => pair.Key))))), analyzer);
            //        return this.GetSpanQuery(fieldNode.FieldKey, Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => pair.Key)), true);
            //    })
            //}))));
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitEqual(EqualNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            if (node.LeftNode is ConstantNode && node.RightNode is ConstantNode)
                return (Query)new BooleanQuery()
        {
          {
            (Query) new MatchAllDocsQuery(),
            ((ConstantNode) node.LeftNode).Value.Equals(((ConstantNode) node.RightNode).Value) ? Occur.MUST : Occur.MUST_NOT
          }
        };
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            ConstantNode valueNode = !(fieldNode.FieldType != typeof(string)) ? QueryHelper.GetValueNode<object>((BinaryNode)node) : QueryHelper.GetValueNode((BinaryNode)node, fieldNode.FieldType);
            Query query;
            if (this.ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.Equal, mappingState, out query))
                return query;
            string fieldKey = fieldNode.FieldKey;
            object obj = valueNode.Value;
            float boost = node.Boost;
            AbstractSearchFieldConfiguration fieldConfiguration = this.Parameters.FieldMap != null ? this.Parameters.FieldMap.GetFieldConfiguration(fieldKey) : (AbstractSearchFieldConfiguration)null;
            if (fieldConfiguration != null)
                obj = fieldConfiguration.FormatForWriting(obj);
            if (obj == null)
                throw new NotSupportedException("Comparison of null values is not supported.");
            return this.VisitEqual(fieldKey, obj, boost, mappingState);
        }

        protected virtual Query VisitEqual(string fieldName, object fieldValue, float fieldBoost, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            Query query = this.rangeQueryBuilder.BuildRangeQuery(new RangeQueryOptions()
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
            object obj = this.ValueFormatter.FormatValueForIndexStorage(fieldValue, fieldName);
            AbstractSearchFieldConfiguration fieldConfiguration = this.Parameters.FieldMap != null ? this.Parameters.FieldMap.GetFieldConfiguration(fieldName) : (AbstractSearchFieldConfiguration)null;
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
            Query equalsQuery = this.GetEqualsQuery(fieldName, val, mappingState);
            equalsQuery.Boost = fieldBoost;
            return equalsQuery;
        }

        protected virtual Query VisitLessThanOrEqual(LessThanOrEqualNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            ConstantNode valueNode = QueryHelper.GetValueNode((BinaryNode)node, fieldNode.FieldType);
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
                FieldFromValue = (object)null,
                FieldToValue = fieldValue,
                IncludeLower = true,
                IncludeUpper = true
            }, this, true);
        }

        protected virtual Query VisitLessThan(LessThanNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            ConstantNode valueNode = QueryHelper.GetValueNode((BinaryNode)node, fieldNode.FieldType);
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

        protected virtual Query VisitGreaterThanOrEqual(GreaterThanOrEqualNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            ConstantNode valueNode = QueryHelper.GetValueNode((BinaryNode)node, fieldNode.FieldType);
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

        protected virtual Query VisitGreaterThan(GreaterThanNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            ConstantNode valueNode = QueryHelper.GetValueNode((BinaryNode)node, fieldNode.FieldType);
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

        protected virtual Query VisitMatchAll(MatchAllNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            return (Query)new MatchAllDocsQuery();
        }

        protected virtual Query VisitMatchNone(MatchNoneNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            return (Query)new BooleanQuery()
      {
        {
          (Query) new MatchAllDocsQuery(),
          Occur.MUST_NOT
        }
      };
        }

        protected virtual Query VisitNot(NotNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            var fieldNode = QueryHelper.GetFieldNode((BinaryNode)node.Operand);
            string queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>((BinaryNode)node.Operand).Value, fieldNode.FieldKey).ToString();
            return new NotEqualQuery(fieldNode.FieldKey, queryText);
        }

        protected virtual Query VisitOr(OrNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            BooleanQuery booleanQuery1 = new BooleanQuery();
            Query query1 = this.Visit(node.LeftNode, mappingState);
            Query query2 = this.Visit(node.RightNode, mappingState);
            BooleanQuery booleanQuery2 = query1 as BooleanQuery;
            if (booleanQuery2 != null && booleanQuery2.Clauses.TrueForAll((Predicate<BooleanClause>)(o => o.Occur == Occur.SHOULD)))
            {
                foreach (Query query3 in Enumerable.Select<BooleanClause, Query>((IEnumerable<BooleanClause>)booleanQuery2.Clauses, (Func<BooleanClause, Query>)(o => o.Query)))
                    booleanQuery1.Add(query3, Occur.SHOULD);
            }
            else
                booleanQuery1.Add(query1, Occur.SHOULD);
            booleanQuery1.Add(query2, Occur.SHOULD);
            return (Query)booleanQuery1;
        }

        protected virtual Query VisitStartsWith(StartsWithNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode(node);
            string queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>(node).Value, fieldNode.FieldKey).ToString();
            var query = new TermQuery(new Term(fieldNode.FieldKey, "/" + queryText + ".*/"));

            //mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.StartsWith, analyzer));
            //Query query = terms.Count <= 1 ? (terms.Count != 1 ? (Query)new SpanFirstQuery((SpanQuery)new SpanWildcardQuery(new Term(fieldNode.FieldKey, queryText.ToLowerInvariant() + "*")), 1) : (Query)new SpanFirstQuery((SpanQuery)new SpanWildcardQuery(new Term(fieldNode.FieldKey, terms[0].Key + "*")), terms[0].Value + 1)) : (Query)this.BuildSpanQuery(Enumerable.ToArray<SpanSubQuery>(Enumerable.Select<IGrouping<int, KeyValuePair<string, int>>, SpanSubQuery>(Enumerable.GroupBy<KeyValuePair<string, int>, int>((IEnumerable<KeyValuePair<string, int>>)terms, (Func<KeyValuePair<string, int>, int>)(term => term.Value)), (Func<IGrouping<int, KeyValuePair<string, int>>, SpanSubQuery>)(g => new SpanSubQuery()
            //{
            //    IsWildcard = g.Key == Enumerable.Last<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value,
            //    Position = g.Key,
            //    CreatorMethod = (Func<SpanQuery>)(() =>
            //    {
            //        if (g.Key == Enumerable.First<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value)
            //            return (SpanQuery)new SpanFirstQuery((SpanQuery)new SpanTermQuery(new Term(fieldNode.FieldKey, Enumerable.First<string>(Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => pair.Key))))), g.Key + 1);
            //        if (g.Key == Enumerable.Last<KeyValuePair<string, int>>((IEnumerable<KeyValuePair<string, int>>)terms).Value)
            //            return this.GetSpanQuery(fieldNode.FieldKey, Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => pair.Key + "*")), true);
            //        return this.GetSpanQuery(fieldNode.FieldKey, Enumerable.Select<KeyValuePair<string, int>, string>((IEnumerable<KeyValuePair<string, int>>)g, (Func<KeyValuePair<string, int>, string>)(pair => pair.Key)), true);
            //    })
            //}))));
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitWhere(WhereNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            BooleanQuery booleanQuery = new BooleanQuery();
            Query query1 = this.Visit(node.PredicateNode, mappingState);
            Query query2 = this.Visit(node.SourceNode, mappingState);
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
            return (Query)booleanQuery;
        }

        protected Query VisitMatches(MatchesNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            RegexQuery regexQuery = new RegexQuery(new Term(QueryHelper.GetFieldNode((BinaryNode)node).FieldKey, LinqStringExtensions.ToStringOrEmpty(QueryHelper.GetValueNode<string>((BinaryNode)node).Value)));
            if (node.RegexOptions != null)
            {
                if (!TypeExtensions.IsAssignableTo(((ConstantNode)node.RegexOptions).Type, typeof(RegexOptions)))
                    throw new NotSupportedException(string.Format("The regex options part of the '{0}' was expected to be of type '{1}', but was of type '{2}'.", (object)node.GetType().Name, (object)typeof(RegexOptions).FullName, (object)((ConstantNode)node.RegexOptions).Type.FullName));
                RegexOptions regexOptions = (RegexOptions)((ConstantNode)node.RegexOptions).Value;
                regexQuery.RegexImplementation = new AzureRegexCapabilities(regexOptions);
            }
          ((Query)regexQuery).Boost = node.Boost;
            return (Query)regexQuery;
        }

        protected Query VisitWildcardMatch(WildcardMatchNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            string queryText = this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>((BinaryNode)node).Value, fieldNode.FieldKey).ToString();
            IAzureSearchFieldConfiguration fieldConfiguration = this.Parameters.GetFieldConfiguration(fieldNode.FieldKey);
            Analyzer analyzer = new KeywordAnalyzer();
            List<string> list1 = this.GetFieldTerms(fieldNode.FieldKey, queryText, analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.MatchWildcard, analyzer));
            Query query;
            if (list1.Count > 1)
            {
                List<SpanQuery> list2 = new List<SpanQuery>();
                for (int index = 0; index < list1.Count; ++index)
                    list2.Add((SpanQuery)new SpanWildcardQuery(new Term(fieldNode.FieldKey, list1[index])));
                query = (Query)new SpanNearQuery(list2.ToArray(), 0, true);
            }
            else
                query = list1.Count != 1 ? (Query)new MatchNoDocsQuery() : (Query)new WildcardQuery(new Term(fieldNode.FieldKey, list1[0]));
            query.Boost = node.Boost;
            return query;
        }

        protected Query VisitLike(LikeNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            FieldNode fieldNode = QueryHelper.GetFieldNode((BinaryNode)node);
            string queryText = this.NormalizeValue(this.ValueFormatter.FormatValueForIndexStorage(QueryHelper.GetValueNode<string>((BinaryNode)node).Value, fieldNode.FieldKey).ToString());
            Analyzer analyzer;
            List<string> fieldTerms = this.GetFieldTerms(fieldNode.FieldKey, queryText, out analyzer);
            mappingState.UsedAnalyzers.Add(new Tuple<string, ComparisonType, Analyzer>(fieldNode.FieldKey, ComparisonType.Like, analyzer));
            Query query;
            if (fieldTerms.Count > 1)
            {
                List<SpanQuery> list = new List<SpanQuery>();
                for (int index = 0; index < fieldTerms.Count; ++index)
                    list.Add((SpanQuery)new SpanFuzzyQuery(new Term(fieldNode.FieldKey, fieldTerms[index]), node.MinimumSimilarity));
                query = (Query)new SpanNearQuery(list.ToArray(), node.Slop, true);
            }
            else
                query = fieldTerms.Count != 1 ? (Query)new MatchNoDocsQuery() : (Query)new FuzzyQuery(new Term(fieldNode.FieldKey, fieldTerms[0]), node.MinimumSimilarity);
            query.Boost = node.Boost;
            return query;
        }

        protected virtual Query VisitFilter(FilterNode node, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            AzureQueryMapper.AzureQueryMapperState mappingState1 = new AzureQueryMapper.AzureQueryMapperState((IEnumerable<IExecutionContext>)mappingState.ExecutionContexts);
            return this.Visit(node.PredicateNode, mappingState1);
        }

        protected bool ProcessAsVirtualField(string fieldName, AzureQueryMapper.AzureQueryMapperState mappingState)
        {
            if (this.Parameters.FieldQueryTranslators == null)
                return false;
            IFieldQueryTranslator translator = this.Parameters.FieldQueryTranslators.GetTranslator(fieldName.ToLowerInvariant());
            if (translator == null)
                return false;
            mappingState.VirtualFieldProcessors.Add(translator);
            return true;
        }

        protected virtual bool ProcessAsVirtualField(FieldNode fieldNode, ConstantNode valueNode, float boost, ComparisonType comparison, AzureQueryMapper.AzureQueryMapperState mappingState, out Query query)
        {
            return this.ProcessAsVirtualField(fieldNode.FieldKey, valueNode.Value, boost, comparison, mappingState, out query);
        }

        protected bool ProcessAsVirtualField(string fieldName, object fieldValue, float boost, ComparisonType comparison, AzureQueryMapper.AzureQueryMapperState mappingState, out Query query)
        {
            query = (Query)null;
            if (this.Parameters.FieldQueryTranslators == null)
                return false;
            IFieldQueryTranslator translator = this.Parameters.FieldQueryTranslators.GetTranslator(fieldName.ToLowerInvariant());
            if (translator == null)
                return false;
            TranslatedFieldQuery translatedFieldQuery = translator.TranslateFieldQuery(fieldName, fieldValue, comparison, this.Parameters.FieldNameTranslator);
            if (translatedFieldQuery == null)
                return false;
            BooleanQuery booleanQuery = new BooleanQuery();
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
                mappingState.AdditionalQueryMethods.AddRange((IEnumerable<QueryMethod>)translatedFieldQuery.QueryMethods);
            mappingState.VirtualFieldProcessors.Add(translator);
            query = (Query)booleanQuery;
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

            public Query FilterQuery { get; set; }

            public List<IFieldQueryTranslator> VirtualFieldProcessors { get; set; }

            public List<FacetQuery> FacetQueries { get; set; }

            public List<Tuple<string, ComparisonType, Analyzer>> UsedAnalyzers { get; set; }

            public List<IExecutionContext> ExecutionContexts { get; set; }

            public AzureQueryMapperState(IEnumerable<IExecutionContext> executionContexts)
            {
                this.AdditionalQueryMethods = new List<QueryMethod>();
                this.VirtualFieldProcessors = new List<IFieldQueryTranslator>();
                this.FacetQueries = new List<FacetQuery>();
                this.UsedAnalyzers = new List<Tuple<string, ComparisonType, Analyzer>>();
                this.ExecutionContexts = executionContexts != null ? Enumerable.ToList<IExecutionContext>(executionContexts) : new List<IExecutionContext>();
            }
        }
    }
}
