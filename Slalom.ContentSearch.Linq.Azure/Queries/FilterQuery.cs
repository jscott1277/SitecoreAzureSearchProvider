using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.Linq.Azure.Queries
{
    public class FilterQuery : Query
    {
        public enum FilterQueryTypes
        {
            Equals,
            GreaterThan,
            GreaterThanEquals,
            LessThan,
            LessThanEquals,
            NotEquals
        }

        private enum ValueTypes
        {
            Bool,
            Numeric,
            DateTimeOffset,
            String
        }

        public FilterQueryTypes QueryType { get; set; }
        public string FieldName { get; private set; }
        public object FieldValue { get; private set; }
        private ValueTypes ValueType { get; set; }

        public FilterQuery(string fieldName, object fieldValue, FilterQueryTypes queryType)
        {
            FieldName = fieldName;
            FieldValue = fieldValue;
            QueryType = queryType;
            ValueType = ValueTypes.String;
            if (fieldValue.GetType() == typeof(bool))
            {
                ValueType = ValueTypes.Bool;
            }

            if (IsNumeric(fieldValue.ToString()))
            {
                ValueType = ValueTypes.Numeric;
            }

            if (fieldValue.GetType() == typeof(DateTimeOffset))
            {
                ValueType = ValueTypes.DateTimeOffset;
            }
        }

        public override string ToString(string field)
        {
            var retVal = FieldName;
            switch (QueryType)
            {
                case FilterQueryTypes.GreaterThan:
                    retVal += " gt ";
                    break;
                case FilterQueryTypes.GreaterThanEquals:
                    retVal += " ge ";
                    break;
                case FilterQueryTypes.Equals:
                    retVal += " eq ";
                    break;
                case FilterQueryTypes.NotEquals:
                    retVal += " ne ";
                    break;
                case FilterQueryTypes.LessThan:
                    retVal += " lt ";
                    break;
                case FilterQueryTypes.LessThanEquals:
                    retVal += " le ";
                    break;
            }

            switch (ValueType)
            {
                case ValueTypes.Bool:
                case ValueTypes.Numeric:
                    retVal += string.Format("{0}", FieldValue);
                    break;
                case ValueTypes.DateTimeOffset:
                    retVal += ((DateTimeOffset)FieldValue).ToString();
                    break;
                case ValueTypes.String:
                    retVal += "'" + FieldValue.ToString() + "'";
                    break;
            }
            
            return retVal;
        }

        private bool IsNumeric(string val)
        {
            return Regex.IsMatch(val, @"^\d+$");
        }
    }
}
