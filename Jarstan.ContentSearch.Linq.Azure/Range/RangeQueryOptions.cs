namespace Jarstan.ContentSearch.Linq.Azure.Queries.Range
{
    public class RangeQueryOptions
    {
        public string FieldName { get; set; }

        public object FieldFromValue { get; set; }

        public object FieldToValue { get; set; }

        public float Boost { get; set; }

        public bool IncludeUpper { get; set; }

        public bool IncludeLower { get; set; }
    }
}
