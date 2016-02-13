using Lucene.Net.Search;
using Slalom.ContentSearch.Linq.Azure;

namespace Slalom.ContentSearch.Linq.Azure.Queries.Range
{
    public class DefaultRangeQueryProcessor : RangeQueryProcessor
    {
        public override Query Process(RangeQueryOptions options, AzureQueryMapper mapper)
        {
            string lowerTerm = (string)null;
            string upperTerm = (string)null;
            if (options.FieldFromValue != null)
                lowerTerm = this.NormalizeValue(mapper.ValueFormatter.FormatValueForIndexStorage(options.FieldFromValue, options.FieldName).ToString(), mapper);
            if (options.FieldToValue != null)
                upperTerm = this.NormalizeValue(mapper.ValueFormatter.FormatValueForIndexStorage(options.FieldToValue, options.FieldName).ToString(), mapper);
            TermRangeQuery termRangeQuery = new TermRangeQuery(options.FieldName, lowerTerm, upperTerm, options.IncludeLower, options.IncludeUpper);
            termRangeQuery.Boost = options.Boost;
            return (Query)termRangeQuery;
        }
    }
}
