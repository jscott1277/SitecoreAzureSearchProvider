using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Indexing;
using Sitecore.ContentSearch.Linq.Parsing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq.Parsing
{
    public class AzureGenericQueryable<TElement, TQuery> : GenericQueryable<TElement, TQuery>// IOrderedQueryable<TElement>, IQueryable<TElement>, IEnumerable<TElement>, IOrderedQueryable, IQueryable, IEnumerable, IQueryProvider, IHasNativeQuery<TQuery>, IHasNativeQuery, IHasTraceWriter
    {
        public AzureGenericQueryable(Index<TElement, TQuery> index, QueryMapper<TQuery> queryMapper, IQueryOptimizer queryOptimizer, FieldNameTranslator fieldNameTranslator)
            : base(index, queryMapper, queryOptimizer, fieldNameTranslator)
        {
            Index = index;   
        }

        protected AzureGenericQueryable(Index<TQuery> index, QueryMapper<TQuery> queryMapper, IQueryOptimizer queryOptimizer, Expression expression, Type itemType, FieldNameTranslator fieldNameTranslator)
            : base(index, queryMapper, queryOptimizer, expression, itemType, fieldNameTranslator)
        {
            Index = index;
        }

        public override IQueryProvider Provider
        {
            get
            {
                return this;
            }
        }

        public override TResult Execute<TResult>(Expression expression)
        {
            return Index.Execute<TResult>(GetQuery(expression));
        }

        public override IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var genericQueryable = new AzureGenericQueryable<TElement, TQuery>(Index, QueryMapper, QueryOptimizer, expression, ItemType, FieldNameTranslator);
            ((IHasTraceWriter)genericQueryable).TraceWriter = ((IHasTraceWriter)this).TraceWriter;
            return genericQueryable;

        }

        protected override TQuery GetQuery(Expression expression)
        {
            this.Trace(expression, "Expression");
            var expressionParser = new AzureExpressionParser(typeof(TElement), this.ItemType, this.FieldNameTranslator);
            IndexQuery indexQuery = expressionParser.Parse(expression);
            this.Trace(indexQuery, "Raw query:");
            IndexQuery indexQuery2 = this.QueryOptimizer.Optimize(indexQuery);
            this.Trace(indexQuery2, "Optimized query:");
            TQuery tQuery = this.QueryMapper.MapQuery(indexQuery2);
            //this.Trace(new GenericDumpable(tQuery), "Native query:");
            return tQuery;
        }
    }
}
