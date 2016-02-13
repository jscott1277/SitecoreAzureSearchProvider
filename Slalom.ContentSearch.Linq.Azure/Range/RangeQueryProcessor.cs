using Lucene.Net.Search;
using Slalom.ContentSearch.Linq.Azure;

namespace Slalom.ContentSearch.Linq.Azure.Queries.Range
{
    public abstract class RangeQueryProcessor
    {
        public abstract Query Process(RangeQueryOptions options, AzureQueryMapper mapper);

        protected string NormalizeValue(string valueToNormalize, AzureQueryMapper mapper)
        {
            if (mapper.QueryParser != null && mapper.QueryParser.LowercaseExpandedTerms)
                valueToNormalize = valueToNormalize.ToLower(mapper.QueryParser.Locale);
            return valueToNormalize;
        }
    }
}
