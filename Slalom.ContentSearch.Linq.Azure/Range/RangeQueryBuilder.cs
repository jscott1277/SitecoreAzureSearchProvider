using Lucene.Net.Search;
using System;
using System.Collections.Generic;

namespace Slalom.ContentSearch.Linq.Azure.Queries.Range
{
    public class RangeQueryBuilder
    {
        public Query BuildRangeQuery(RangeQueryOptions options, AzureQueryMapper mapper, bool useDefaultProcessor)
        {
            if (string.IsNullOrEmpty(options.FieldName))
                throw new ArgumentException("RangeQueryOptions.FieldName cannot be null or empty string.");
            foreach (RangeQueryProcessor rangeQueryProcessor in this.GetProcessors())
            {
                Query query = rangeQueryProcessor.Process(options, mapper);
                if (query != null)
                    return query;
            }
            if (useDefaultProcessor)
                return new DefaultRangeQueryProcessor().Process(options, mapper);
            return (Query)null;
        }

        protected virtual IEnumerable<RangeQueryProcessor> GetProcessors()
        {
            yield return (RangeQueryProcessor)new PrimitiveTypesProcessor();
            yield return (RangeQueryProcessor)new DateTimeRangeQueryProcessor();
        }
    }
}
