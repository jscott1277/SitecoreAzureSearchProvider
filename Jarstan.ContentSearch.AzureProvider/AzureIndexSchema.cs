using Sitecore.ContentSearch;
using Sitecore.Diagnostics;
using Jarstan.ContentSearch.AzureProvider;
using System.Collections.Generic;

namespace Sitecore.ContentSearch.LuceneProvider
{
    public class AzureIndexSchema : ISearchIndexSchema
    {
        private readonly IAzureProviderIndex index;

        public ICollection<string> AllFieldNames
        {
            get
            {
                return null;
            }
        }

        public AzureIndexSchema(IAzureProviderIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            this.index = index;
        }
    }
}
