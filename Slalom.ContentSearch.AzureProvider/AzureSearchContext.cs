
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Parsing;
using Sitecore.ContentSearch.Pipelines.GetFacets;
using Sitecore.ContentSearch.Pipelines.ProcessFacets;
using Sitecore.ContentSearch.Pipelines.QueryGlobalFilters;
using Slalom.ContentSearch.SearchTypes;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.AzureProvider
{
    public class AzureSearchContext : IProviderSearchContext, IDisposable
    {
        public AzureSearchContext(IAzureProviderIndex index, SearchSecurityOptions securityOptions = SearchSecurityOptions.Default)
        {
            Index = index;
            if (securityOptions == SearchSecurityOptions.Default)
            {
                SecurityOptions = index.Configuration.DefaultSearchSecurityOption;
            }
            else
            {
                SecurityOptions = securityOptions;
            }
            Settings = Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
        }

        public bool ConvertQueryDatesToUtc { get; set; }

        public ISearchIndex Index { get; }

        public SearchSecurityOptions SecurityOptions { get; }

        public IContentSearchConfigurationSettings Settings { get; }

        public IQueryable<TItem> GetQueryable<TItem>()
        {
            return GetQueryable<TItem>(new IExecutionContext[] { });
        }

        public IQueryable<TItem> GetQueryable<TItem>(params IExecutionContext[] executionContexts)
        {
            var linqToAzureIndex = new LinqToAzureIndex<TItem>(this, executionContexts);
            //if (Settings.EnableSearchDebug())
                //linqToAzureIndex.TraceWriter = new LoggingTraceWriter(SearchLog.Log);
            var queryable = linqToAzureIndex.GetQueryable();
            if (typeof(TItem).IsAssignableFrom(typeof(AzureSearchResultItem)))
            {
                var globalFiltersArgs = new QueryGlobalFiltersArgs(linqToAzureIndex.GetQueryable(), typeof(TItem), Enumerable.ToList(executionContexts));
                this.Index.Locator.GetInstance<ICorePipeline>().Run("contentSearch.getGlobalLinqFilters", globalFiltersArgs);
                queryable = (IQueryable<TItem>)globalFiltersArgs.Query;
            }
            return queryable;
        }

        public IQueryable<TItem> GetQueryable<TItem>(IExecutionContext executionContext)
        {
            return GetQueryable<TItem>(new IExecutionContext[] { executionContext });
        }

        public IEnumerable<SearchIndexTerm> GetTermsByFieldName(string fieldName, string prefix)
        {
            return null;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~AzureSearchContext() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
