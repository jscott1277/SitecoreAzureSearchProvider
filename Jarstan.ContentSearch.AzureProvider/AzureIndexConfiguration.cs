using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureIndexConfiguration : ProviderIndexConfiguration
    {
        public string AzureSearchServiceName { get; set; }
        public string AzureSearchServiceApiKey { get; set; }
        public bool AzureSearchEnableBatching { get; set; }
        public int AzureSearchBatchSize { get; set; }
        public int AzureSearchRetryCount { get; set; }
        public int AzureSearchRetryInitialInterval { get; set; }
        public int AzureSearchRetryIncrement { get; set; }

        public string AzureDefaultScoringProfileName { get; set; }

        public IIndexDocumentPropertyMapper<Document> IndexDocumentPropertyMapper { get; set; }

        public AzureIndexConfiguration()
        {
            this.DocumentOptions = new AzureDocumentBuilderOptions();
        }

        public override void Initialize(ISearchIndex searchIndex)
        {
            SearchIndexInitializableUtility.Initialize(searchIndex, new object[1]
            {
                this.IndexDocumentPropertyMapper
            });
            base.Initialize(searchIndex);
        }
    }
}
