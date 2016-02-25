using Sitecore.ContentSearch.Linq.Extensions;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using System;
using System.Linq;

namespace Jarstan.ContentSearch.Linq.Azure
{
    public class AzureQueryOptimizer : QueryOptimizer<AzureQueryOptimizerState>
    {
        protected override QueryNode Visit(QueryNode node, AzureQueryOptimizerState state)
        {
            switch (node.NodeType)
            {
                case QueryNodeType.All:
                    return this.VisitAll((AllNode)node, state);
                case QueryNodeType.And:
                    return this.VisitAnd((AndNode)node, state);
                case QueryNodeType.Any:
                    return this.VisitAny((AnyNode)node, state);
                case QueryNodeType.Between:
                    return this.VisitBetween((BetweenNode)node, state);
                case QueryNodeType.Boost:
                    return this.VisitBoost((BoostNode)node, state);
                case QueryNodeType.Cast:
                    return this.VisitCast((CastNode)node, state);
                case QueryNodeType.Constant:
                    return this.VisitConstant((ConstantNode)node, state);
                case QueryNodeType.Contains:
                    return this.VisitContains((ContainsNode)node, state);
                case QueryNodeType.Count:
                    return this.VisitCount((CountNode)node, state);
                case QueryNodeType.ElementAt:
                    return this.VisitElementAt((ElementAtNode)node, state);
                case QueryNodeType.EndsWith:
                    return this.VisitEndsWith((EndsWithNode)node, state);
                case QueryNodeType.Equal:
                    return this.VisitEqual((EqualNode)node, state);
                case QueryNodeType.First:
                    return this.VisitFirst((FirstNode)node, state);
                case QueryNodeType.GreaterThan:
                    return this.VisitGreaterThan((GreaterThanNode)node, state);
                case QueryNodeType.GreaterThanOrEqual:
                    return this.VisitGreaterThanOrEqual((GreaterThanOrEqualNode)node, state);
                case QueryNodeType.Last:
                    return this.VisitLast((LastNode)node, state);
                case QueryNodeType.LessThan:
                    return this.VisitLessThan((LessThanNode)node, state);
                case QueryNodeType.LessThanOrEqual:
                    return this.VisitLessThanOrEqual((LessThanOrEqualNode)node, state);
                case QueryNodeType.Max:
                    return this.VisitMax((MaxNode)node, state);
                case QueryNodeType.Min:
                    return this.VisitMin((MinNode)node, state);
                case QueryNodeType.Not:
                    return this.VisitNot((NotNode)node, state);
                case QueryNodeType.NotEqual:
                    return this.VisitNotEqual((NotEqualNode)node, state);
                case QueryNodeType.Or:
                    return this.VisitOr((OrNode)node, state);
                case QueryNodeType.OrderBy:
                    return this.VisitOrderBy((OrderByNode)node, state);
                case QueryNodeType.Select:
                    return this.VisitSelect((SelectNode)node, state);
                case QueryNodeType.Single:
                    return this.VisitSingle((SingleNode)node, state);
                case QueryNodeType.Skip:
                    return this.VisitSkip((SkipNode)node, state);
                case QueryNodeType.StartsWith:
                    return this.VisitStartsWith((StartsWithNode)node, state);
                case QueryNodeType.Take:
                    return this.VisitTake((TakeNode)node, state);
                case QueryNodeType.Where:
                    return this.VisitWhere((WhereNode)node, state);
                case QueryNodeType.Matches:
                    return this.VisitMatches((MatchesNode)node, state);
                case QueryNodeType.Filter:
                    return this.VisitFilter((FilterNode)node, state);
                case QueryNodeType.GetResults:
                    return this.VisitGetResults((GetResultsNode)node, state);
                case QueryNodeType.Negate:
                    return this.VisitNegate((NegateNode)node, state);
                case QueryNodeType.GetFacets:
                    return this.VisitGetFacets((GetFacetsNode)node, state);
                case QueryNodeType.FacetOn:
                    return this.VisitFacetOn((FacetOnNode)node, state);
                case QueryNodeType.FacetPivotOn:
                    return this.VisitFacetPivotOn((FacetPivotOnNode)node, state);
                case QueryNodeType.WildcardMatch:
                    return this.VisitWildcardMatch((WildcardMatchNode)node, state);
                case QueryNodeType.Like:
                    return this.VisitLike((LikeNode)node, state);
                case QueryNodeType.Join:
                    return this.VisitJoin((JoinNode)node, state);
                case QueryNodeType.GroupJoin:
                    return this.VisitGroupJoin((GroupJoinNode)node, state);
                case QueryNodeType.SelfJoin:
                    return this.VisitSelfJoin((SelfJoinNode)node, state);
                case QueryNodeType.SelectMany:
                    return this.VisitSelectMany((SelectManyNode)node, state);
                case QueryNodeType.Custom:
                    var customNode = node as CustomNode;
                    if (customNode != null)
                    {
                        switch (customNode.CustomNodeType)
                        {
                            case CustomQueryNodeTypes.HighlightOn:
                                return this.VisitHighlightOn((HighlightOnNode)customNode, state);
                            case CustomQueryNodeTypes.GetHighlightResults:
                                return this.VisitGetHighlightResults((GetHighlightResultsNode)customNode, state);
                        }
                    }
                    return node;
                default:
                    return node;
            }
        }

        protected virtual QueryNode VisitAll(AllNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new AllNode(queryNode1, queryNode2);
            return (QueryNode)new AllNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode());
        }

        protected virtual QueryNode VisitAnd(AndNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.LeftNode, state);
            QueryNode queryNode2 = this.Visit(node.RightNode, state);
            bool? booleanValue1 = this.GetBooleanValue(queryNode1);
            bool? booleanValue2 = this.GetBooleanValue(queryNode2);
            if (!booleanValue1.HasValue && !booleanValue2.HasValue)
                return (QueryNode)new AndNode(queryNode1, queryNode2);
            if (booleanValue1.HasValue && booleanValue2.HasValue)
            {
                if (!booleanValue1.Value || !booleanValue2.Value)
                    return (QueryNode)new MatchNoneNode();
                return (QueryNode)new MatchAllNode();
            }
            if (booleanValue1.HasValue)
            {
                if (!booleanValue1.Value)
                    return (QueryNode)new MatchNoneNode();
                return queryNode2;
            }
            if (!booleanValue2.Value)
                return (QueryNode)new MatchNoneNode();
            return queryNode1;
        }

        protected virtual QueryNode VisitAny(AnyNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new AnyNode(queryNode1, queryNode2);
            return (QueryNode)new AnyNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode());
        }

        protected virtual QueryNode VisitBoost(BoostNode node, AzureQueryOptimizerState state)
        {
            float boost = state.Boost;
            state.Boost = node.Boost;
            QueryNode queryNode = this.Visit(node.Operand, state);
            state.Boost = boost;
            return queryNode;
        }

        protected virtual QueryNode VisitCast(CastNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new CastNode(this.Visit(node.SourceNode, state), node.TargetType);
        }

        protected virtual QueryNode VisitConstant(ConstantNode node, AzureQueryOptimizerState state)
        {
            Type other = typeof(IQueryable);
            if (TypeExtensions.IsAssignableTo(node.Type, other))
                return (QueryNode)new MatchAllNode();
            return (QueryNode)node;
        }

        protected virtual QueryNode VisitCount(CountNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new CountNode(queryNode1, queryNode2, node.IsLongCount);
            return (QueryNode)new CountNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.IsLongCount);
        }

        protected virtual QueryNode VisitElementAt(ElementAtNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new ElementAtNode(this.Visit(node.SourceNode, state), node.Index, node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitEqual(EqualNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new EqualNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), state.Boost);
        }

        protected virtual QueryNode VisitMatches(MatchesNode node, AzureQueryOptimizerState state)
        {
            QueryNode regexOptions = (QueryNode)null;
            QueryNode leftNode = this.Visit(node.LeftNode, state);
            QueryNode rightNode = this.Visit(node.RightNode, state);
            if (node.RegexOptions != null)
                regexOptions = this.Visit(node.RegexOptions, state);
            return (QueryNode)new MatchesNode(leftNode, rightNode, regexOptions, state.Boost);
        }

        protected virtual QueryNode VisitFirst(FirstNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new FirstNode(queryNode1, queryNode2, node.AllowDefaultValue);
            return (QueryNode)new FirstNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitMax(MaxNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new MaxNode(queryNode1, queryNode2, node.AllowDefaultValue);
            return (QueryNode)new MaxNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitMin(MinNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new MinNode(queryNode1, queryNode2, node.AllowDefaultValue);
            return (QueryNode)new MinNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitLast(LastNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new LastNode(queryNode1, queryNode2, node.AllowDefaultValue);
            return (QueryNode)new LastNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitNot(NotNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode = this.Visit(node.Operand, state);
            if (queryNode.NodeType == QueryNodeType.Not)
                return ((NotNode)queryNode).Operand;
            bool? booleanValue = this.GetBooleanValue(queryNode);
            if (!booleanValue.HasValue)
                return (QueryNode)new NotNode(queryNode);
            if (booleanValue.Value)
                return (QueryNode)new MatchNoneNode();
            return (QueryNode)new MatchAllNode();
        }

        protected virtual QueryNode VisitNotEqual(NotEqualNode node, AzureQueryOptimizerState state)
        {
            return this.Visit((QueryNode)new NotNode((QueryNode)new EqualNode(node.LeftNode, node.RightNode, node.Boost)), state);
        }

        protected virtual QueryNode VisitOr(OrNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.LeftNode, state);
            QueryNode queryNode2 = this.Visit(node.RightNode, state);
            bool? booleanValue1 = this.GetBooleanValue(queryNode1);
            bool? booleanValue2 = this.GetBooleanValue(queryNode2);
            if (!booleanValue1.HasValue && !booleanValue2.HasValue)
                return (QueryNode)new OrNode(queryNode1, queryNode2);
            if (booleanValue1.HasValue && booleanValue2.HasValue)
            {
                if (!booleanValue1.Value && !booleanValue2.Value)
                    return (QueryNode)new MatchNoneNode();
                return (QueryNode)new MatchAllNode();
            }
            if (booleanValue1.HasValue)
                return queryNode2;
            return queryNode1;
        }

        protected virtual QueryNode VisitOrderBy(OrderByNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new OrderByNode(this.Visit(node.SourceNode, state), node.Field, node.FieldType, node.SortDirection);
        }

        protected virtual QueryNode VisitSelect(SelectNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new SelectNode(this.Visit(node.SourceNode, state), node.Lambda, node.FieldNames);
        }

        protected virtual QueryNode VisitSingle(SingleNode node, AzureQueryOptimizerState state)
        {
            QueryNode queryNode1 = this.Visit(node.SourceNode, state);
            QueryNode queryNode2 = this.Visit(node.PredicateNode, state);
            if (queryNode2.NodeType == QueryNodeType.MatchAll)
                return (QueryNode)new SingleNode(queryNode1, queryNode2, node.AllowDefaultValue);
            return (QueryNode)new SingleNode(this.VisitAnd(new AndNode(queryNode1, queryNode2), state), (QueryNode)new MatchAllNode(), node.AllowDefaultValue);
        }

        protected virtual QueryNode VisitSkip(SkipNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new SkipNode(this.Visit(node.SourceNode, state), node.Count);
        }

        protected virtual QueryNode VisitTake(TakeNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new TakeNode(this.Visit(node.SourceNode, state), node.Count);
        }

        protected virtual QueryNode VisitWhere(WhereNode node, AzureQueryOptimizerState state)
        {
            QueryNode sourceNode = this.Visit(node.SourceNode, state);
            QueryNode queryNode = this.Visit(node.PredicateNode, state);
            bool? booleanValue = this.GetBooleanValue(queryNode);
            if (!booleanValue.HasValue)
            {
                if (sourceNode is MatchAllNode)
                    return queryNode;
                if (sourceNode is MatchNoneNode)
                    return sourceNode;
                return (QueryNode)new WhereNode(sourceNode, queryNode);
            }
            if (booleanValue.Value)
                return sourceNode;
            return (QueryNode)new MatchNoneNode();
        }

        protected virtual QueryNode VisitFilter(FilterNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new FilterNode(this.Visit(node.SourceNode, state), this.Visit(node.PredicateNode, state));
        }

        protected virtual QueryNode VisitGetResults(GetResultsNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new GetResultsNode(this.Visit(node.SourceNode, state), node.Options);
        }

        protected virtual QueryNode VisitGetFacets(GetFacetsNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new GetFacetsNode(this.Visit(node.SourceNode, state));
        }

        protected virtual QueryNode VisitFacetOn(FacetOnNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new FacetOnNode(this.Visit(node.SourceNode, state), node.Field, node.MinimumNumberOfDocuments, node.FilterValues);
        }

        protected virtual QueryNode VisitHighlightOn(HighlightOnNode node, AzureQueryOptimizerState state)
        {
            return new HighlightOnNode(this.Visit(node.SourceNode, state), node.Field);
        }

        protected virtual QueryNode VisitGetHighlightResults(GetHighlightResultsNode node, AzureQueryOptimizerState state)
        {
            return new GetHighlightResultsNode(this.Visit(node.SourceNode, state));
        }

        protected virtual QueryNode VisitFacetPivotOn(FacetPivotOnNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new FacetPivotOnNode(this.Visit(node.SourceNode, state), node.Fields, node.MinimumNumberOfDocuments, node.FilterValues);
        }

        protected virtual QueryNode VisitBetween(BetweenNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new BetweenNode(node.Field, node.From, node.To, node.Inclusion, state.Boost);
        }

        protected virtual QueryNode VisitNegate(NegateNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new NegateNode(this.Visit(node.Operand, state));
        }

        protected virtual QueryNode VisitGreaterThan(GreaterThanNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new GreaterThanNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state));
        }

        protected virtual QueryNode VisitGreaterThanOrEqual(GreaterThanOrEqualNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new GreaterThanOrEqualNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state));
        }

        protected virtual QueryNode VisitLessThan(LessThanNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new LessThanNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state));
        }

        protected virtual QueryNode VisitLessThanOrEqual(LessThanOrEqualNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new LessThanOrEqualNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state));
        }

        protected virtual QueryNode VisitContains(ContainsNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new ContainsNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), state.Boost);
        }

        protected virtual QueryNode VisitStartsWith(StartsWithNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new StartsWithNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), state.Boost);
        }

        protected virtual QueryNode VisitEndsWith(EndsWithNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new EndsWithNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), state.Boost);
        }

        protected virtual QueryNode VisitWildcardMatch(WildcardMatchNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new WildcardMatchNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), state.Boost);
        }

        protected virtual QueryNode VisitLike(LikeNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new LikeNode(this.Visit(node.LeftNode, state), this.Visit(node.RightNode, state), node.MinimumSimilarity, node.Slop, state.Boost);
        }

        protected virtual QueryNode VisitJoin(JoinNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new JoinNode(this.Visit(node.OuterQuery, new AzureQueryOptimizerState()), this.Visit(node.InnerQuery, new AzureQueryOptimizerState()), node.OuterQueryExpression, node.InnerQueryExpression, node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer, node.GetQueryableDelegate);
        }

        protected virtual QueryNode VisitGroupJoin(GroupJoinNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new GroupJoinNode(this.Visit(node.OuterQuery, new AzureQueryOptimizerState()), this.Visit(node.InnerQuery, new AzureQueryOptimizerState()), node.OuterQueryExpression, node.InnerQueryExpression, node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.SelectQuery, node.EqualityComparer, node.GetQueryableDelegate);
        }

        protected virtual QueryNode VisitSelfJoin(SelfJoinNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new SelfJoinNode(this.Visit(node.OuterQuery, new AzureQueryOptimizerState()), this.Visit(node.InnerQuery, new AzureQueryOptimizerState()), node.OuterQueryExpression, node.InnerQueryExpression, node.OuterKey, node.InnerKey, node.OuterKeyExpression, node.InnerKeyExpression, node.GetQueryableDelegate);
        }

        private QueryNode VisitSelectMany(SelectManyNode node, AzureQueryOptimizerState state)
        {
            return (QueryNode)new SelectManyNode(this.Visit(node.SourceQuery, new AzureQueryOptimizerState()), node.SourceQueryExpression, node.CollectionSelectorExpression, node.ResultSelectorExpression, node.GetQueryableDelegate);
        }

        private bool? GetBooleanValue(QueryNode node)
        {
            if (node.NodeType == QueryNodeType.MatchAll)
                return new bool?(true);
            if (node.NodeType == QueryNodeType.MatchNone)
                return new bool?(false);
            if (node.NodeType == QueryNodeType.Constant)
            {
                ConstantNode constantNode = (ConstantNode)node;
                if (constantNode.Type == typeof(bool))
                    return new bool?((bool)constantNode.Value);
            }
            return new bool?();
        }
    }
}
