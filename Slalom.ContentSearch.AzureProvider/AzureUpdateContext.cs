using Microsoft.Azure.Search.Models;
using Sitecore;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Sharding;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Search;
using Slalom.ContentSearch.AzureProvider.DelegatingHandlers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.AzureProvider
{
    public class AzureUpdateContext : IProviderUpdateContext, IProviderOperationContext, IDisposable, IProviderUpdateContextEx, IAzureProviderUpdateContext
    {
        private volatile bool isDisposed;
        private volatile bool isDisposing;
        private readonly IContentSearchConfigurationSettings contentSearchSettings;

        public AzureUpdateContext(ISearchIndex index)
        {
            Assert.ArgumentNotNull(index, "index");

            Index = index;
            this.contentSearchSettings = index.Locator.GetInstance<IContentSearchConfigurationSettings>();
            IsParallel = this.contentSearchSettings.IsParallelIndexingEnabled();
            this.ParallelOptions = new ParallelOptions();
            int num = this.contentSearchSettings.ParallelIndexingMaxThreadLimit();
            if (num > 0)
                this.ParallelOptions.MaxDegreeOfParallelism = num;
            this.CommitPolicyExecutor = new NullCommitPolicyExecutor();
            IndexActions = new List<IndexAction>();
        }

        public ParallelOptions ParallelOptions { get; set; }

        public List<IndexAction> IndexActions { get; set; }

        public ISearchIndex Index { get; set; }

        public IAzureProviderIndex AzureIndex
        {
            get
            {
                return (IAzureProviderIndex)Index;
            }
        }

        public ICommitPolicyExecutor CommitPolicyExecutor { get; set; }

        public IEnumerable<Shard> ShardsWithPendingChanges { get; set; }

        public bool IsParallel { get; set; }
 
        public void AddDocument(object itemToAdd, IExecutionContext executionContext)
        {
            AddDocument(itemToAdd, new IExecutionContext[] { executionContext });
        }

        public void AddDocument(object itemToAdd, params IExecutionContext[] executionContexts)
        {
            IndexActions.Add(IndexAction.MergeOrUpload((Document)itemToAdd));

            if (!AzureIndex.AzureConfiguration.AzureSearchEnableBatching)
            {
                Commit();
            }
            else if (AzureIndex.AzureConfiguration.AzureSearchEnableBatching && IndexActions.Count >= AzureIndex.AzureConfiguration.AzureSearchBatchSize)
            {
                Commit();
            }
        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, IExecutionContext executionContext)
        {
            UpdateDocument(itemToUpdate, criteriaForUpdate, new IExecutionContext[] { executionContext });
        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, params IExecutionContext[] executionContexts)
        {
            IndexActions.Add(IndexAction.MergeOrUpload((Document)itemToUpdate));
        }

        public void Delete(IIndexableUniqueId id)
        {
            
        }

        public void Delete(IIndexableId id)
        {
            var searchParams = new SearchParameters();
            searchParams.SearchFields.Add("s_group");
            searchParams.Select.Add("s_key");
            var resultTask = AzureIndex.AzureIndexClient.Documents.SearchWithHttpMessagesAsync(id.ToString(), searchParams);
            resultTask.Wait();
            var results = resultTask.Result.Body.Results;

            foreach (var result in results)
            {
                IndexActions.Add(IndexAction.Delete(result.Document));
            }
        }

        public void Commit()
        {
            if (IndexActions.Any())
            {
                try
                {
                    var response = AzureIndex.AzureIndexClient.Documents.IndexWithHttpMessagesAsync(IndexBatch.New(IndexActions.ToArray()));
                    //response.Wait();
                    IndexActions.Clear();
                }
                catch (Exception ex)
                {
                    CrawlingLog.Log.Warn("Error indexing on Item for " + Index.Name, ex);
                }
            }
        }

        public void Optimize()
        {

        }

        public void Dispose()
        {

        }

        public void Delete(IIndexableUniqueId id, params IExecutionContext[] executionContexts)
        {
            //Get Document from results
            
        }

        public void Delete(IIndexableId id, params IExecutionContext[] executionContexts)
        {

        }
    }
}