using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider.DelegatingHandlers
{
    [Obsolete("Was used for Lucene Searches, but don't need unless reverting to a version of AzureSearch prior to 1.1")]
    public class BaseDelegatingHandler : DelegatingHandler
    {
        internal string QueryAppend(string query, string value)
        {
            if (query.StartsWith("?"))
            {
                return query = query.Substring(1) + value;
            }

            return query += value;
        }
    }
}
