using Sitecore.ContentSearch;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Jarstan.ContentSearch.AzureProvider")]

namespace Jarstan.ContentSearch
{
    internal static class SearchIndexInitializableUtility
    {
        public static void Initialize(ISearchIndex index, params object[] instances)
        {
            TypeActionHelper.Call<ISearchIndexInitializable>(i => i.Initialize(index), instances);
        }
    }
}
