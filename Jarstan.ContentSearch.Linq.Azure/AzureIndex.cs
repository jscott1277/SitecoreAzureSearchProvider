using Sitecore.ContentSearch.Linq.Indexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Parsing;
using Jarstan.ContentSearch.Linq.Azure;
using Sitecore.ContentSearch;

namespace Jarstan.ContentSearch.Linq.Azure
{
    public class AzureIndex<TItem> : Index<TItem, AzureQuery>
    {
        public AzureIndex(AzureIndexParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            QueryMapper = new AzureQueryMapper(parameters);
            QueryOptimizer = new AzureQueryOptimizer();
            FieldNameTranslator = parameters.FieldNameTranslator;
            Parameters = parameters;
        }

        public AzureIndexParameters Parameters { get; }

        protected override FieldNameTranslator FieldNameTranslator { get; }

        protected override QueryMapper<AzureQuery> QueryMapper { get; }

        protected override IQueryOptimizer QueryOptimizer { get; }

        protected override IIndexValueFormatter ValueFormatter { get; }

        //public override IQueryable<TItem> GetQueryable()
        //{
        //    IQueryable<TItem> queryable = new GenericQueryable<TItem, AzureQuery>(this, this.QueryMapper, this.QueryOptimizer, this.FieldNameTranslator);
        //    //foreach (IPredefinedQueryAttribute predefinedQueryAttribute in Enumerable.ToList(Enumerable.SelectMany<Type, IPredefinedQueryAttribute>(this.GetTypeInheritance(typeof(TItem)), (Func<Type, IEnumerable<IPredefinedQueryAttribute>>)(t => Enumerable.Cast<IPredefinedQueryAttribute>((IEnumerable)t.GetCustomAttributes(typeof(IPredefinedQueryAttribute), true))))))
        //    //    queryable = predefinedQueryAttribute.ApplyFilter<TItem>(queryable, this.ValueFormatter);
        //    return queryable;
        //}

        public override TResult Execute<TResult>(AzureQuery query)
        {
            return default(TResult);
        }

        public override IEnumerable<TElement> FindElements<TElement>(AzureQuery query)
        {
            return new List<TElement>();
        }
    }
}
