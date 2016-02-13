using Lucene.Net.Search;
using System;

namespace Slalom.ContentSearch.Linq.Azure.Queries.Range
{
    public class PrimitiveTypesProcessor : RangeQueryProcessor
    {
        public override Query Process(RangeQueryOptions opt, AzureQueryMapper mapper)
        {
            object obj = opt.FieldFromValue ?? opt.FieldToValue;
            if (obj == null || !obj.GetType().IsPrimitive)
                return (Query)null;
            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Decimal:
                    return this.BuildLongRangeQuery(opt);
                case TypeCode.Single:
                case TypeCode.Double:
                    return this.BuildDoubleRangeQuery(opt);
                default:
                    return (Query)null;
            }
        }

        protected Query BuildLongRangeQuery(RangeQueryOptions opt)
        {
            long? min = opt.FieldFromValue != null ? new long?(Convert.ToInt64(opt.FieldFromValue)) : new long?();
            long? max = opt.FieldToValue != null ? new long?(Convert.ToInt64(opt.FieldToValue)) : new long?();
            Query query = (Query)NumericRangeQuery.NewLongRange(opt.FieldName, min, max, opt.IncludeLower, opt.IncludeUpper);
            query.Boost = opt.Boost;
            return query;
        }

        protected Query BuildDoubleRangeQuery(RangeQueryOptions opt)
        {
            double? min = opt.FieldFromValue != null ? new double?(Convert.ToDouble(opt.FieldFromValue)) : new double?();
            double? max = opt.FieldToValue != null ? new double?(Convert.ToDouble(opt.FieldToValue)) : new double?();
            Query query = (Query)NumericRangeQuery.NewDoubleRange(opt.FieldName, min, max, opt.IncludeLower, opt.IncludeUpper);
            query.Boost = opt.Boost;
            return query;
        }
    }
}
