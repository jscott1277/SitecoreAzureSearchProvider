using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Extensions;
using Sitecore.ContentSearch.Linq.Helpers;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Jarstan.ContentSearch.Linq.Parsing
{
    public class AzureExpressionParser : ExpressionParser
    {
        public AzureExpressionParser(Type elementType, Type itemType, FieldNameTranslator fieldNameTranslator)
            : base(elementType, itemType, fieldNameTranslator)
        {
           
        }

        public override IndexQuery Parse(Expression expression)
        {
            var rootNode = this.Visit(expression);
            return new IndexQuery(rootNode, this.ElementType);
        }

        protected override QueryNode VisitMethodCall(MethodCallExpression methodCall)
        {
            MethodInfo method = methodCall.Method;
            if (method.DeclaringType == typeof(Queryable))
            {
                return this.VisitQueryableMethod(methodCall);
            }
            if (method.DeclaringType == typeof(QueryableExtensions))
            {
                return this.VisitQueryableExtensionMethod(methodCall);
            }
            if (method.DeclaringType == typeof(AzureQueryableExtensions))
            {
                return this.VisitQueryableExtensionMethod(methodCall);
            }
            if (method.DeclaringType == this.ItemType || this.ItemType.IsSubclassOf(method.DeclaringType) || (method.DeclaringType.IsInterface && this.ItemType.ImplementsInterface(method.DeclaringType)))
            {
                return this.VisitItemMethod(methodCall);
            }
            if (method.DeclaringType == typeof(string))
            {
                return this.VisitStringMethod(methodCall);
            }
            if (method.DeclaringType == typeof(MethodExtensions))
            {
                return this.VisitExtensionMethod(methodCall);
            }
            if (!method.DeclaringType.IsGenericType || !(method.DeclaringType.GetGenericTypeDefinition() == typeof(ICollection<>)))
            {
                if (!method.DeclaringType.GetInterfaces().Any((Type x) => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>)))
                {
                    if (method.DeclaringType == typeof(Enumerable) && methodCall.Arguments.Count > 0 && methodCall.Arguments.First<Expression>() is MemberExpression)
                    {
                        return this.VisitLinqEnumerableExtensionMethod(methodCall);
                    }
                    return this.EvaluateMethodCall(methodCall);
                }
            }
            return this.VisitICollectionMethod(methodCall);
        }

        protected override QueryNode Visit(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Add:
                case ExpressionType.AndAlso:
                case ExpressionType.Divide:
                case ExpressionType.Equal:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.Multiply:
                case ExpressionType.NotEqual:
                case ExpressionType.OrElse:
                case ExpressionType.Subtract:
                    return this.VisitBinary((BinaryExpression)expression);
                case ExpressionType.Call:
                    return this.VisitMethodCall((MethodCallExpression)expression);
                case ExpressionType.Constant:
                    return this.VisitConstant((ConstantExpression)expression);
                case ExpressionType.Convert:
                case ExpressionType.Negate:
                case ExpressionType.Not:
                    return this.VisitUnary((UnaryExpression)expression);
                case ExpressionType.Invoke:
                    return this.VisitInvocation((InvocationExpression)expression);
                case ExpressionType.MemberAccess:
                    return this.VisitMemberAccess((MemberExpression)expression);
                case ExpressionType.New:
                    return this.VisitNew((NewExpression)expression);
                case ExpressionType.Parameter:
                    throw new NotSupportedException("Unsupported expression node type: " + expression.NodeType + ". This could be due to ordering of expression statements. For example filtering after a select expression like this : queryable.Select(i => new { i.Id, i.Title }).Where(i => d.Title > \"A\"");
                case ExpressionType.Index:
                    return VisitIndex((IndexExpression)expression);
                default:
                    throw new NotSupportedException(string.Format("Unsupported expression node type: {0}", expression.NodeType));
            }
        }


        private QueryNode VisitIndex(IndexExpression expression)
        {
            var queryNode = Visit(expression.Object);
            if (!(queryNode is FieldNode))
                throw new NotSupportedException(string.Format("Unsupported expression node type: {0}", (object)expression.NodeType));
            return queryNode;
        }

        protected override QueryNode EvaluateMethodCall(MethodCallExpression methodCall)
        {
            ValidateMethodCallArguments(methodCall.Arguments);
            try
            {
                return new ConstantNode(Expression.Lambda(methodCall).Compile().DynamicInvoke(), methodCall.Type);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(string.Format("The method '{0}' is not supported. Declaring type: {1}. Exception: {2}", methodCall.Method.Name, methodCall.Method.DeclaringType.FullName, ex));
            }
        }

        protected new void ValidateMethodCallArguments(IEnumerable<Expression> arguments)
        {
            foreach (Expression current in arguments)
            {
                QueryNode queryNode = this.Visit(current);
                if (!(queryNode is ConstantNode))
                {
                    throw new NotSupportedException(string.Format("Invalid Method Call Argument Type: {0} - {1}. Only constant arguments is supported.", queryNode.NodeType, queryNode));
                }
            }
        }

        protected override QueryNode VisitQueryableExtensionMethod(MethodCallExpression methodCall)
        {
            MethodInfo method = methodCall.Method;
            string name;
            switch (name = method.Name)
            {
                case "Filter":
                    return this.VisitFilterMethod(methodCall);
                case "GetResults":
                    return this.VisitGetResultsMethod(methodCall);
                case "GetFacets":
                    return this.VisitGetFacetsMethod(methodCall);
                case "FacetOn":
                    return this.VisitFacetOnMethod(methodCall);
                case "HighlightOn":
                    return this.VisitHighlightOnMethod(methodCall);
                case "GetHighlightResults":
                    return this.VisitGetHighlightResultsMethod(methodCall);
                case "FacetPivotOn":
                    return this.VisitFacetPivotOnMethod(methodCall);
                case "SelfJoin":
                    return this.VisitSelfJoinMethod(methodCall);
            }
            throw new NotSupportedException(string.Format("The method '{0}' is not supported. Declaring type: {1}", method.Name, (method.DeclaringType != null) ? method.DeclaringType.FullName : "[unknown]"));
        }

        protected virtual QueryNode VisitGetHighlightResultsMethod(MethodCallExpression methodCall)
        {
            string preTag = "<em>";
            string postTag = "</em>";
            bool mergeHighlights = false;
            if (methodCall.Arguments.Count == 4)
            {
                var queryNode = Visit(GetArgument(methodCall.Arguments, 1));
                preTag = (string)((ConstantNode)queryNode).Value;

                queryNode = Visit(GetArgument(methodCall.Arguments, 2));
                postTag = (string)((ConstantNode)queryNode).Value;

                queryNode = Visit(GetArgument(methodCall.Arguments, 3));
                mergeHighlights = (bool)((ConstantNode)queryNode).Value;
            }
            return new GetHighlightResultsNode(this.Visit(methodCall.Arguments[0]), preTag, postTag, mergeHighlights);
        }

        protected virtual QueryNode VisitHighlightOnMethod(MethodCallExpression methodCall)
        {
            var sourceNode = Visit(GetArgument(methodCall.Arguments, 0));
            //if (methodCall.Arguments.Count >= 3)
            //{
            //    var queryNode = Visit(GetArgument(methodCall.Arguments, 2));
            //    if (queryNode.NodeType != QueryNodeType.Constant)
            //        throw new NotSupportedException(string.Format("Invalid minimumNumberOfDocuments node type: {0} - {1}", (object)queryNode.NodeType, (object)queryNode));
            //    minimumNumberOfDocuments = (int)((ConstantNode)queryNode).Value;
            //}
            //if (methodCall.Arguments.Count >= 4)
            //{
            //    QueryNode queryNode = this.Visit(this.GetArgument(methodCall.Arguments, 3));
            //    if (queryNode.NodeType != QueryNodeType.Constant)
            //        throw new NotSupportedException(string.Format("Invalid filterValues node type: {0} - {1}", (object)queryNode.NodeType, (object)queryNode));
            //    filterValues = (IEnumerable<object>)((ConstantNode)queryNode).Value;
            //}
            var lambdaExpression = Convert<LambdaExpression>(StripQuotes(GetArgument(methodCall.Arguments, 1)));
            if (lambdaExpression.Body.NodeType == ExpressionType.MemberAccess)
            {
                var queryNode = Visit(lambdaExpression.Body);
                FieldNode fieldNode = queryNode as FieldNode;
                if (fieldNode != null)
                    return new HighlightOnNode(sourceNode, fieldNode.FieldKey);
                var constantNode = queryNode as ConstantNode;
                if (constantNode != null && constantNode.Value is string)
                    return new HighlightOnNode(sourceNode, constantNode.Value.ToString());

                throw new NotSupportedException(string.Format("Highlighting can only be done on '{0}'. Expression used '{1}'", typeof(FieldNode).FullName, methodCall.Arguments[1].Type.FullName));
            }

            var fieldNode1 = Visit(lambdaExpression.Body) as FieldNode;

            if (fieldNode1 == null)
                throw new NotSupportedException(string.Format("Ordering can only be done on '{0}'. Expression used '{1}'", typeof(FieldNode).FullName, methodCall.Arguments[1].Type.FullName));

            return new HighlightOnNode(sourceNode, fieldNode1.FieldKey);
        }

    }
}
