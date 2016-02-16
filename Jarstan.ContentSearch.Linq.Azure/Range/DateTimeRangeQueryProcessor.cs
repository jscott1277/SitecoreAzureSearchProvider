using Lucene.Net.Search;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using System;

namespace Jarstan.ContentSearch.Linq.Azure.Queries.Range
{
    public class DateTimeRangeQueryProcessor : RangeQueryProcessor
    {
        public override Query Process(RangeQueryOptions options, AzureQueryMapper mapper)
        {
            if (!(options.FieldFromValue is DateTime) && !(options.FieldToValue is DateTime))
                return (Query)null;
            options.FieldFromValue = this.ConvertToUtc(options.FieldFromValue, options.FieldName, mapper);
            options.FieldToValue = this.ConvertToUtc(options.FieldToValue, options.FieldName, mapper);
            return new DefaultRangeQueryProcessor().Process(options, mapper);
        }

        private object ConvertToUtc(object objectToConvert, string fieldName, AzureQueryMapper mapper)
        {
            if (objectToConvert == null)
                return (object)null;
            DateTime dateTime = (DateTime)objectToConvert;
            if (dateTime.Kind != System.DateTimeKind.Utc)
            {
                SearchLog.Log.Warn(string.Format("Your query is using non UTC dates. field:{0} value: {1}. You will probably have incorrect search result.", (object)fieldName, (object)dateTime), (Exception)null);
                if (mapper.Parameters.ConvertQueryDatesToUtc)
                    objectToConvert = (object)ContentSearchManager.Locator.GetInstance<IDateTimeConverter>().ToUniversalTime(dateTime);
            }
            return objectToConvert;
        }
    }
}
