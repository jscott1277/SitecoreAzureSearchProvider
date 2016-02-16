using Lucene.Net.Search;
using Jarstan.ContentSearch.Linq.Azure;

namespace Jarstan.ContentSearch.Linq.Azure.Queries.Range
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
