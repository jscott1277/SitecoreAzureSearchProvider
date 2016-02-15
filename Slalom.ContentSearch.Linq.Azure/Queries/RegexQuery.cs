using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.Linq.Azure.Queries
{
    public class RegexQuery : Query
    {
        public enum RegexQueryTypes
        {
            Contains,
            EndsWith,
            StartsWith
        }

        public string FieldName { get; private set; }
        public string FieldValue { get; private set; }
        public RegexQueryTypes QueryType { get; private set; }

        public RegexQuery(string fieldName, string fieldValue, RegexQueryTypes queryType)
        {
            FieldName = fieldName;
            FieldValue = fieldValue;
            QueryType = queryType;
        }

        public override string ToString(string field)
        {
            switch(QueryType)
            {
                case RegexQueryTypes.Contains:
                    return BuildContains();
                case RegexQueryTypes.EndsWith:
                    return BuildEndsWith();
                case RegexQueryTypes.StartsWith:
                    return BuildStartsWith();
            }

            return string.Empty;
        }

        private string BuildStartsWith()
        {
            return string.Format("{0}:/{1}.*/", FieldName, FieldValue);
        }

        private string BuildEndsWith()
        {
            return string.Format("{0}:/.*{1}/", FieldName, FieldValue);
        }

        public string BuildContains()
        {
            return string.Format("{0}:/.*{1}.*/", FieldName, FieldValue);
        }
    }
}
