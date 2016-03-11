using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Sharding;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureUpdateContext : IProviderUpdateContext, IProviderOperationContext, IDisposable, IProviderUpdateContextEx, IAzureProviderUpdateContext
    {
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
        }

        public ParallelOptions ParallelOptions { get; set; }

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
            Commit(IndexAction.MergeOrUpload((Document)itemToAdd));
        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, IExecutionContext executionContext)
        {
            UpdateDocument(itemToUpdate, criteriaForUpdate, new IExecutionContext[] { executionContext });
        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, params IExecutionContext[] executionContexts)
        {
            UpdateDocument(itemToUpdate, executionContexts);
        }

        public void Delete(IIndexableUniqueId id)
        {
            
        }

        public void Delete(IIndexableId id)
        {
            var searchParams = new SearchParameters();
            searchParams.SearchFields = new List<string>();
            searchParams.SearchFields.Add("s_group");
            searchParams.Select = new List<string>();
            searchParams.Select.Add("s_key");
            var resultTask = AzureIndex.AzureIndexClient.Documents.SearchWithHttpMessagesAsync(id.ToString(), searchParams);
            resultTask.Wait();
            var results = resultTask.Result.Body.Results;

            foreach (var result in results)
            {
                Commit(IndexAction.Delete(result.Document));
            }
        }

        public void Commit()
        {
            //var actions = GetActions();
            //if (actions.Any())
            //{
            //    try
            //    {
            //        await AzureIndex.AzureIndexClient.Documents.IndexWithHttpMessagesAsync(IndexBatch.New(actions));
            //        //var response = AzureIndex.AzureIndexClient.Documents.IndexWithHttpMessagesAsync(IndexBatch.New(actions));
            //        //response.Wait();
            //    }
            //    catch (Exception ex)
            //    {
            //        CrawlingLog.Log.Warn("Error indexing on Item for " + Index.Name, ex);
            //    }
            //}
        }

        public async void Commit(IndexAction action, int retry = 0)
        {
            try
            {
                await AzureIndex.AzureIndexClient.Documents.IndexWithHttpMessagesAsync(IndexBatch.New(new List<IndexAction> { action }));
                //var response = AzureIndex.AzureIndexClient.Documents.IndexWithHttpMessagesAsync(IndexBatch.New(actions));
                //response.Wait();
            }
            catch (Exception ex)
            {
                if (retry < 6)
                {
                    Thread.Sleep(50);
                    Commit(action, retry++);
                }
                else
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