using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using System;

namespace Jarstan.ContentSearch.AzureProvider
{
    public interface IAzureProviderIndex : ISearchIndex, IDisposable
    {
        AzureIndexConfiguration AzureConfiguration { get; }
        SearchServiceClient AzureServiceClient { get; set; }
        SearchIndexClient AzureSearchClient { get; set; }
        SearchIndexClient AzureIndexClient { get; set; }
        IAzureSearchIndexSchema AzureSchema { get; }
    }
}
