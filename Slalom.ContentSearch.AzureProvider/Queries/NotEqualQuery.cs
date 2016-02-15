using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.AzureProvider.Queries
{
    public class NotEqualQuery : Query
    {
        public string FieldName { get; private set; }
        public string FieldValue { get; private set; }

        public NotEqualQuery(string fieldName, string fieldValue)
        {
            FieldName = fieldName;
            FieldValue = fieldValue;
        }
        public override string ToString(string field)
        {
            return string.Format("+(-{0}:\"\"{1})", FieldName, FieldValue);
        }
    }
}
