using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider.DelegatingHandlers
{
    [Obsolete("Was used for Lucene Searches, but don't need unless reverting to a version of AzureSearch prior to 1.1")]
    public class IndexDocumentDelegatingHandler : BaseDelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(request.RequestUri);
            uriBuilder.Query = base.QueryAppend(uriBuilder.Query, "&allowUnsafeKeys");
            request.RequestUri = uriBuilder.Uri;

            return base.SendAsync(request, cancellationToken);
        }
    }
}
