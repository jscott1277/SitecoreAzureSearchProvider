using Sitecore.ContentSearch.Linq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jarstan.ContentSearch.Linq
{
    public static class AzureQueryableExtensions
    {
        public static IQueryable<TSource> HighlightOn<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (keySelector == null)
            {
                throw new ArgumentNullException("keySelector");
            }
            return source.Provider.CreateQuery<TSource>(Expression.Call(null, ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(new Type[]
            {
                typeof(TSource),
                typeof(TKey)
            }), new Expression[]
            {
                source.Expression,
                Expression.Quote(keySelector)
            }));
        }

        public static HighlightSearchResults<TSource> GetHighlightResults<TSource>(this IQueryable<TSource> source, string preTag = "<em>", string postTag = "</em>", bool mergeHighlights = false)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            return source.Provider.Execute<HighlightSearchResults<TSource>>(Expression.Call(null, ((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TSource)), new Expression[4]
            {
                source.Expression,
                Expression.Constant(preTag),
                Expression.Constant(postTag),
                Expression.Constant(mergeHighlights)
            }));
        }
    }
}
