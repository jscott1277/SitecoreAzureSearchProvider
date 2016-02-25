using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Nodes;
using System;
using System.Linq.Expressions;

namespace Jarstan.ContentSearch.Linq.Parsing
{
    internal class FacetPivotExpressionParserBridge : AzureExpressionParser
    {
        internal FacetPivotExpressionParserBridge(Type elementType, Type itemType, FieldNameTranslator fieldNameTranslator) : base(elementType, itemType, fieldNameTranslator)
        {
        }

        internal new QueryNode VisitItemProperty(MemberExpression expression)
        {
            return base.VisitItemProperty(expression);
        }

        internal new QueryNode VisitItemMethod(MethodCallExpression methodCall)
        {
            return base.VisitItemMethod(methodCall);
        }
    }
}
