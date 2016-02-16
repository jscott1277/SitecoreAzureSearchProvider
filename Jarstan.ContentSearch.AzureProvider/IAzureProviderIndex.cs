using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    public interface IAzureProviderIndex : ISearchIndex, IDisposable
    {
        AzureIndexConfiguration AzureConfiguration { get; }
        SearchServiceClient AzureServiceClient { get; set; }
        SearchIndexClient AzureSearchClient { get; set; }
        SearchIndexClient AzureIndexClient { get; set; }
        
        ConcurrentQueue<Field> AzureIndexFields { get; set; }
        bool AzureSchemaBuilt { get; set; }
        void BuildAzureIndexSchema(AzureField keyField, AzureField idField);
        void AddAzureIndexFields(List<Field> indexFields);
        void AddAzureIndexField(Field indexField);
        void ReconcileAzureIndexSchema(Document document);
    }
}
