using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureIndexConfiguration : ProviderIndexConfiguration
    {
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
