using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider.DelegatingHandlers
{
    [Obsolete("Was used for Lucene Searches, but don't need unless reverting to a version of AzureSearch prior to 1.1")]
    public class SearchDelegatingHandler : BaseDelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(request.RequestUri);
            if (uriBuilder.Query.Contains("api-version=2015-02-28"))
            {
                uriBuilder.Query = uriBuilder.Query.Replace("api-version=2015-02-28", "api-version=2015-02-28-Preview");
            }
            
            //uriBuilder.Query = base.QueryAppend(uriBuilder.Query, "&queryType=full");
            request.RequestUri = uriBuilder.Uri;

            return base.SendAsync(request, cancellationToken);
        }
    }
}
