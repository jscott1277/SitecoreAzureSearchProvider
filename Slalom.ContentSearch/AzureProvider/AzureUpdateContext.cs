using Microsoft.Azure.Search.Models;
using Sitecore;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Sharding;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Diagnostics;
using Sitecore.Search;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureUpdateContext : IProviderUpdateContext, IProviderOperationContext, IDisposable, IProviderUpdateContextEx
    {
        private volatile bool isDisposed;
        private volatile bool isDisposing;

        public AzureUpdateContext(ISearchIndex index)
        {
            Assert.ArgumentNotNull((object)index, "index");
           
            Index = index;
            IsParallel = false;
            
            //this.ParallelOptions = new ParallelOptions();
            //int num = this.contentSearchSettings.ParallelIndexingMaxThreadLimit();
            //if (num > 0)
            //    this.ParallelOptions.MaxDegreeOfParallelism = num;
            this.CommitPolicyExecutor = (ICommitPolicyExecutor)new NullCommitPolicyExecutor();
        }

        [Obsolete("The constructor is no longer in use and will be removed in later release.")]
        public AzureUpdateContext(ISearchIndex index, ICommitPolicy commitPolicy, ICommitPolicyExecutor commitPolicyExecutor)
      : this(index, commitPolicyExecutor)
        {
        }

        public AzureUpdateContext(ISearchIndex index, ICommitPolicyExecutor commitPolicyExecutor)
      : this(index)
        {
            if (commitPolicyExecutor == null)
                throw new ArgumentNullException("commitPolicyExecutor");
            this.CommitPolicyExecutor = commitPolicyExecutor;
        }

        public ParallelOptions ParallelOptions { get; set; }

        public ISearchIndex Index { get; set; }
        public ICommitPolicyExecutor CommitPolicyExecutor { get; set; }

        public IEnumerable<Shard> ShardsWithPendingChanges { get; set; }

        public bool IsParallel { get; set; }

        public void AddDocument(object itemToAdd, IExecutionContext executionContext)
        {
            throw new NotImplementedException();
        }

        public void AddDocument(object itemToAdd, params IExecutionContext[] executionContexts)
        {

        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, IExecutionContext executionContext)
        {

        }

        public void UpdateDocument(object itemToUpdate, object criteriaForUpdate, params IExecutionContext[] executionContexts)
        {

        }

        public void Delete(IIndexableUniqueId id)
        {

        }

        public void Delete(IIndexableId id)
        {

        }

        public void Commit()
        {

        }

        public void Optimize()
        {

        }

        public void Dispose()
        {

        }

        public void Delete(IIndexableUniqueId id, params IExecutionContext[] executionContexts)
        {

        }

        public void Delete(IIndexableId id, params IExecutionContext[] executionContexts)
        {

        }
    }
}