using Azure.ContentSearch.AzureProvider;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Rest.TransientFaultHandling;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Events;
using Sitecore.ContentSearch.Maintenance;
using Sitecore.ContentSearch.Maintenance.Strategies;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Sharding;
using Sitecore.Diagnostics;
using Slalom.ContentSearch.AzureProvider.DelegatingHandlers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Slalom.ContentSearch.AzureProvider
{
    public class AzureIndex : AbstractSearchIndex, ISearchIndex, IAzureProviderIndex, IDisposable
    {
        private Dictionary<string, ISearchIndex> indexes = new Dictionary<string, ISearchIndex>();
        private readonly List<IIndexUpdateStrategy> strategies = new List<IIndexUpdateStrategy>();
        private readonly Sitecore.ContentSearch.Abstractions.ISettings settings;
        //private AbstractFieldNameTranslator fieldNameTranslator;
        private readonly Sitecore.Abstractions.IFactory factory;

        public AzureIndex(string name, IIndexPropertyStore propertyStore)
        {
            Assert.ArgumentNotNullOrEmpty(name, "name");
            Assert.ArgumentNotNull((object)propertyStore, "propertyStore");
            Name = name.Replace("_", "-").ToLower();
            this.PropertyStore = propertyStore;
            this.settings = ContentSearchManager.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
            this.factory = ContentSearchManager.Locator.GetInstance<Sitecore.Abstractions.IFactory>();
            this.indexes = new Dictionary<string, ISearchIndex>();
            this.AzureIndexFields = new ConcurrentQueue<Field>();
            this.Summary = new AzureIndexSummary(this);
            AddIndex(this);
        }

        public virtual Dictionary<string, ISearchIndex> Indexes
        {
            get
            {
                return this.indexes;
            }
            set
            {
                this.indexes = value;
            }
        }


        public string DefaultIndexConfigurationPath
        {
            get
            {
                return this.settings.GetSetting("ContentSearch.AzureDefaultIndexConfigurationPath", "contentSearch/indexConfigurations/defaultAzureIndexConfiguration");
            }
        }

        public ICommitPolicyExecutor CommitPolicyExecutor { get; set; }

        public override string Name { get; }

        public AzureIndexConfiguration AzureConfiguration
        {
            get
            {
                return this.Configuration as AzureIndexConfiguration;
            }
        }

        public SearchServiceClient AzureServiceClient { get; set; }
        public SearchIndexClient AzureSearchClient { get; set; }
        public SearchIndexClient AzureIndexClient { get; set; }
        public ConcurrentQueue<Field> AzureIndexFields { get; set; }

        public override void AddCrawler(IProviderCrawler crawler)
        {
            crawler.Initialize(this);
            AddCrawler(crawler, false);
        }

        public override void AddCrawler(IProviderCrawler crawler, bool initializeCrawler)
        {
            if (initializeCrawler)
            {
                crawler.Initialize(this);
            }

            this.Crawlers.Add(crawler);
        }

        public override void AddStrategy(IIndexUpdateStrategy strategy)
        {
            strategy.Initialize(this);
            strategies.Add(strategy);
        }

        public override IProviderDeleteContext CreateDeleteContext()
        {
            return null;
        }

        public override IProviderSearchContext CreateSearchContext(SearchSecurityOptions options = SearchSecurityOptions.Default)
        {
            this.EnsureInitialized();
            return new AzureSearchContext(this, options);
        }

        public override IProviderUpdateContext CreateUpdateContext()
        {
            this.EnsureInitialized();
            var commitPolicyExecutor = (ICommitPolicyExecutor)this.CommitPolicyExecutor.Clone();
            commitPolicyExecutor.Initialize(this);
            return new AzureUpdateContext(this);
        }

        public override void Delete(IIndexableUniqueId indexableUniqueId)
        {
            this.Delete(indexableUniqueId, IndexingOptions.Default);
        }

        public override void Delete(IIndexableId indexableId)
        {
            this.Delete(indexableId, IndexingOptions.Default);
        }

        public override void Delete(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
        {
            EnsureInitialized();
            this.VerifyNotDisposed();
            if (!this.ShouldStartIndexing(indexingOptions))
                return;

            using (IProviderUpdateContext updateContext = this.CreateUpdateContext())
            {
                foreach (IProviderCrawler providerCrawler in this.Crawlers)
                    providerCrawler.Delete(updateContext, indexableUniqueId, indexingOptions);

                updateContext.Commit();
            }
        }
    
        public override void Delete(IIndexableId indexableId, IndexingOptions indexingOptions)
        {
            this.VerifyNotDisposed();
            if (!this.ShouldStartIndexing(indexingOptions))
                return;

            using (IProviderUpdateContext updateContext = this.CreateUpdateContext())
            {
                foreach (IProviderCrawler providerCrawler in this.Crawlers)
                    providerCrawler.Delete(updateContext, indexableId, indexingOptions);

                updateContext.Commit();
            }
        }

        public override void Initialize()
        {
            //this.Crawlers = new List<IProviderCrawler>();
            var indexConfiguration = this.Configuration as AzureIndexConfiguration;
            if (indexConfiguration == null)
                throw new ConfigurationErrorsException("Index has no configuration.");
            if (indexConfiguration.IndexDocumentPropertyMapper == null)
                throw new ConfigurationErrorsException("IndexDocumentPropertyMapper have not been configured.");
            if (indexConfiguration.IndexFieldStorageValueFormatter == null)
                throw new ConfigurationErrorsException("IndexFieldStorageValueFormatter have not been configured.");
            if (indexConfiguration.FieldReaders == null)
                throw new ConfigurationErrorsException("FieldReaders have not been configured.");
            if (this.PropertyStore == null)
                throw new ConfigurationErrorsException("Index PropertyStore have not been configured.");

            InitializeSearchIndexInitializables(this.Configuration, this.Crawlers, this.strategies);
            FieldNameTranslator = (AbstractFieldNameTranslator)new AzureFieldNameTranslator(this);

            if (this.CommitPolicyExecutor == null)
                this.CommitPolicyExecutor = new NullCommitPolicyExecutor();

            SetupAzureSearchClient();

            this.initialized = true;
        }

        private void SetupAzureSearchClient()
        { 
            //create client/index for indexing
            AzureServiceClient = new SearchServiceClient(AzureConfiguration.AzureSearchServiceName, new SearchCredentials(AzureConfiguration.AzureSearchServiceApiKey));
            AzureIndexClient = AzureServiceClient.Indexes.GetClient(Name);
            var retryStrategy = new IncrementalRetryStrategy(AzureConfiguration.AzureSearchRetryCount, TimeSpan.FromSeconds(AzureConfiguration.AzureSearchRetryInitialInterval), TimeSpan.FromSeconds(AzureConfiguration.AzureSearchRetryIncrement));
            var retryPolicy = new RetryPolicy<AzureErrorIndexDetectionStrategy>(retryStrategy);
            AzureIndexClient.SetRetryPolicy(retryPolicy);

            //create client/index for searching
            AzureSearchClient = AzureServiceClient.Indexes.GetClient(Name);
            //AzureSearchClient.UseHttpGetForQueries = true;
        }

        public override ISearchIndexSummary Summary { get; }

        public override ISearchIndexSchema Schema { get; }

        public override IIndexPropertyStore PropertyStore { get; set; }

        public override AbstractFieldNameTranslator FieldNameTranslator { get; set; }

        public override ProviderIndexConfiguration Configuration { get; set; }

        public override Sitecore.ContentSearch.IIndexOperations Operations
        {
            get
            {
                return new AzureIndexOperations(this);
            }
        }

        public override bool IsSharded { get; }

        public override bool EnableItemLanguageFallback { get; set; }

        public override bool EnableFieldLanguageFallback { get; set; }

        public override IShardingStrategy ShardingStrategy { get; set; }

        public override IShardFactory ShardFactory { get; }

        public override IEnumerable<Shard> Shards { get; }

        public bool AzureSchemaBuilt { get; set; }

        public void AddIndex(ISearchIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            Assert.IsFalse((this.Indexes.ContainsKey(index.Name) ? 1 : 0) != 0, "An index with the name \"{0}\" have already been added.", index.Name);
            this.Indexes[index.Name] = index;
            if (index.Configuration == null)
            {
                XmlNode configNode = this.factory.GetConfigNode(this.DefaultIndexConfigurationPath);
                if (configNode == null)
                    throw new ConfigurationException("Index must have a ProviderIndexConfiguration associated with it. Please check your config.");
                var config = this.factory.CreateObject<ProviderIndexConfiguration>(configNode);
                if (config == null)
                    throw new ConfigurationException("Unable to create configuration object from path specified in setting 'ContentSearch.AzureDefaultIndexConfigurationPath'. Please check your config.");

                index.Configuration = config;
            }
            if (!index.Configuration.InitializeOnAdd)
                return;

            index.Initialize();
        }

        public override void Rebuild()
        {
            Rebuild(IndexingOptions.Default);
        }

        public override void Rebuild(IndexingOptions indexingOptions)
        {
            PerformRebuild(indexingOptions, CancellationToken.None);
        }
        
        public override Task RebuildAsync(IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            return Task.Run(() => Rebuild(indexingOptions), cancellationToken);
        }

        public override void Refresh(IIndexable indexableStartingPoint)
        {
            Refresh(indexableStartingPoint, IndexingOptions.Default);
        }

        public override void Refresh(IIndexable indexableStartingPoint, IndexingOptions indexingOptions)
        {
            PerformRefresh(indexableStartingPoint, indexingOptions, CancellationToken.None);
        }

        public override Task RefreshAsync(IIndexable indexableStartingPoint, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            return Task.Run(() => Refresh(indexableStartingPoint, indexingOptions), cancellationToken);
        }

        public override void RemoveAllCrawlers()
        {
            this.Crawlers.Clear();
        }

        public override bool RemoveCrawler(IProviderCrawler crawler)
        {
            return Crawlers.Remove(crawler);
        }

        public override void Reset()
        {
            //Do Nothing.  Nothing to reset for Azure Index
        }

        public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds, IndexingOptions indexingOptions)
        {
            PerformUpdate(indexableUniqueIds, indexingOptions);
        }

        public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds)
        {
            Update(indexableUniqueIds, IndexingOptions.Default);
        }

        public override void Update(IIndexableUniqueId indexableUniqueId)
        {
            Update(new List<IIndexableUniqueId> { indexableUniqueId }, IndexingOptions.Default);
        }

        public override void Update(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
        {
            Update(new List<IIndexableUniqueId> { indexableUniqueId }, IndexingOptions.Default);
        }

        public void BuildAzureIndexSchema(AzureField keyField, AzureField idField)
        {
            if (!this.AzureSchemaBuilt)
            {
                try
                {
                    //this.AzureIndexFields = this.AzureIndexFields.Where(f => f.Name != keyField.Name).ToList();
                    AddAzureIndexField(keyField.Field);
                    AddAzureIndexField(idField.Field);

                    var indexName = Name;
                    var fields = this.AzureIndexFields
                        .GroupBy(f => f.Name)
                        .Select(f => f.First()).ToList();

                    var definition = new Index()
                    {
                        Name = indexName,
                        Fields = fields
                    };

                    AzureServiceClient.Indexes.CreateOrUpdateAsync(definition);
                    this.AzureSchemaBuilt = true;
                }
                catch (Exception ex)
                {
                    CrawlingLog.Log.Fatal("Error creating index" + Name, ex);
                }
            }
            else
            {
                ReconcileAzureIndexSchema(null);
            }
        }

        public void ReconcileAzureIndexSchema(Document document)
        {
            try
            {
                if (document != null)
                {
                    //Look for fields that are different from the standards:
                    foreach (var key in document.Keys)
                    {
                        if (!this.AzureIndexFields.Any(f => f.Name == key))
                        {
                            object objVal;
                            document.TryGetValue(key, out objVal);
                            var field = AzureFieldBuilder.BuildField(key, objVal, this);
                            AddAzureIndexField(field);
                        }
                    }
                }

                var indexName = Name;
                var fields = this.AzureIndexFields
                    .GroupBy(f => f.Name)
                    .Select(f => f.First()).ToList();

                var definition = new Index()
                {
                    Name = indexName,
                    Fields = fields
                };

                AzureServiceClient.Indexes.CreateOrUpdateAsync(definition);
            }
            catch (Exception ex)
            {
                CrawlingLog.Log.Fatal("Error creating index" + Name, ex);
            }
        }

        public void AddAzureIndexFields(List<Field> indexFields)
        {
            foreach (var field in indexFields)
            {
                AddAzureIndexField(field);
            }
        }

        public void AddAzureIndexField(Field indexField)
        {
            if (!string.IsNullOrEmpty(indexField.Name) && !this.AzureIndexFields.Any(f => f.Name == indexField.Name))
            {
                this.AzureIndexFields.Enqueue(indexField);
            }
        }

        protected override void PerformRebuild(IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            AzureServiceClient.Indexes.Delete(this.Name);

            using (IProviderUpdateContext updateContext = CreateUpdateContext())
            {
                foreach (var crawler in this.Crawlers)
                {
                    crawler.RebuildFromRoot(updateContext, indexingOptions, CancellationToken.None);
                }

                if ((this.IndexingState & IndexingState.Stopped) != IndexingState.Stopped)
                {
                    updateContext.Optimize();
                }

                updateContext.Commit();
                updateContext.Optimize();
                stopwatch.Stop();

                if ((this.IndexingState & IndexingState.Stopped) == IndexingState.Stopped)
                {
                    return;
                }

                this.PropertyStore.Set(IndexProperties.RebuildTime, stopwatch.ElapsedMilliseconds.ToString((IFormatProvider)CultureInfo.InvariantCulture));
            }
        }

        protected override void PerformRefresh(IIndexable indexableStartingPoint, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            this.VerifyNotDisposed();
            if (!this.ShouldStartIndexing(indexingOptions))
                return;

            if (!Enumerable.Any<IProviderCrawler>(this.Crawlers, c => c.HasItemsToIndex()))
                return;

            using (var context = this.CreateUpdateContext())
            {
                foreach (var crawler in this.Crawlers)
                {
                    crawler.RefreshFromRoot(context, indexableStartingPoint, indexingOptions, CancellationToken.None);
                }

                context.Commit();

                if ((this.IndexingState & IndexingState.Stopped) == IndexingState.Stopped)
                    return;

                context.Optimize();
            }
        }

        private void PerformUpdate(IEnumerable<IIndexableUniqueId> indexableUniqueIds, IndexingOptions indexingOptions)
        {
            if (!this.ShouldStartIndexing(indexingOptions))
                return;

            var instance1 = this.Locator.GetInstance<IEvent>();
            instance1.RaiseEvent("indexing:start", new object[2]
            {
                this.Name,
                false
            });
            var instance2 = this.Locator.GetInstance<IEventManager>();
            var indexingStartedEvent1 = new IndexingStartedEvent();
            indexingStartedEvent1.IndexName = this.Name;
            indexingStartedEvent1.FullRebuild = false;
            var indexingStartedEvent2 = indexingStartedEvent1;
            instance2.QueueEvent<IndexingStartedEvent>(indexingStartedEvent2);
            var context = this.CreateUpdateContext();
            try
            {
                if (context.IsParallel)
                {
                    Parallel.ForEach<IIndexableUniqueId>(indexableUniqueIds, context.ParallelOptions, (Action<IIndexableUniqueId>)(uniqueId =>
                    {
                        if (!this.ShouldStartIndexing(indexingOptions))
                            return;
                        foreach (var providerCrawler in (IEnumerable<IProviderCrawler>)this.Crawlers)
                            providerCrawler.Update(context, uniqueId, indexingOptions);
                    }));
                    if (!this.ShouldStartIndexing(indexingOptions))
                    {
                        context.Commit();
                        return;
                    }
                }
                else
                {
                    foreach (var indexableUniqueId in indexableUniqueIds)
                    {
                        if (!this.ShouldStartIndexing(indexingOptions))
                        {
                            context.Commit();
                            return;
                        }
                        foreach (IProviderCrawler providerCrawler in (IEnumerable<IProviderCrawler>)this.Crawlers)
                            providerCrawler.Update(context, indexableUniqueId, indexingOptions);
                    }
                }
                context.Commit();
            }
            finally
            {
                if (context != null)
                    context.Dispose();
            }
            instance1.RaiseEvent("indexing:end", new object[2]
            {
                this.Name,
                false
            });
            var instance3 = this.Locator.GetInstance<IEventManager>();
            var indexingFinishedEvent1 = new IndexingFinishedEvent();
            indexingFinishedEvent1.IndexName = this.Name;
            indexingFinishedEvent1.FullRebuild = false;
            var indexingFinishedEvent2 = indexingFinishedEvent1;
            instance3.QueueEvent<IndexingFinishedEvent>(indexingFinishedEvent2);
        }

        protected void EnsureInitialized()
        {
            this.VerifyNotDisposed();
            Assert.IsNotNull(this.Configuration, "Configuration");
            Assert.IsTrue(this.Configuration is AzureIndexConfiguration, "Configuration type is not expected.");
            if (!this.initialized)
                throw new InvalidOperationException("Index has not been initialized.");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (this.isDisposed)
                return;
            base.Dispose(isDisposing);

            if (AzureIndexClient != null)
                AzureIndexClient.Dispose();

            if (AzureSearchClient != null)
                AzureSearchClient.Dispose();

            TypeActionHelper.Call<IDisposable>((Action<IDisposable>)(d => d.Dispose()), this.strategies);
            TypeActionHelper.Call<IDisposable>((Action<IDisposable>)(d => d.Dispose()), this.PropertyStore);
            TypeActionHelper.Call<IDisposable>((Action<IDisposable>)(d => d.Dispose()), this.ShardFactory);
            TypeActionHelper.Call<IDisposable>((Action<IDisposable>)(d => d.Dispose()), this.CommitPolicyExecutor);
        }
    }
}
