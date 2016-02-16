using Lucene.Net.Search;
using System;

namespace Jarstan.ContentSearch.Linq.Azure.Exceptions
{
    public class TooManyClausesException : Exception
    {
        public TooManyClausesException()
          : base(TooManyClausesException.GetDefaultMessage())
        {
        }

        public TooManyClausesException(string message)
          : base(message)
        {
        }

        private static string GetDefaultMessage()
        {
            return string.Format("The Lucene query that was generated contains too many clauses. The maximum number of clauses that are allowed in a query is specified in the ‘Sitecore.ContentSearch.Azure.DefaultIndexConfiguration.config’ configuration file, in the ‘ContentSearch.AzureQueryClauseCount’ setting. The value is currently set to ‘{0}’. You should either make the search expression more specific or increase the value specified in the ‘ContentSearch.AzureQueryClauseCount’ setting.", BooleanQuery.MaxClauseCount);
        }
    }
}
