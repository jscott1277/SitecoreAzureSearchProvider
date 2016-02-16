using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Abstractions;
using Sitecore.ContentSearch.Maintenance;
using Sitecore.ContentSearch.Maintenance.Strategies;
using Sitecore.ContentSearch.Security;
using Sitecore.ContentSearch.Sharding;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureIndex : AbstractSearchIndex, ISearchIndex, IDisposable
    {
        private SearchServiceClient _searchClient;
        private SearchIndexClient _indexClient;
        private Dictionary<string, ISearchIndex> indexes = new Dictionary<string, ISearchIndex>();
        private readonly Sitecore.ContentSearch.Abstractions.ISettings settings;
        private readonly Sitecore.Abstractions.IFactory factory;

        public AzureIndex(string name, string folder, IIndexPropertyStore propertyStore, string group)
        {
            Assert.ArgumentNotNullOrEmpty(name, "name");
            Assert.ArgumentNotNullOrEmpty(folder, "folder");
            Assert.ArgumentNotNull((object)propertyStore, "propertyStore");
            Name = name.Replace("_", "-").ToLower();
            //FolderName = folder;
            this.PropertyStore = propertyStore;
            this.settings = ContentSearchManager.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
            this.factory = ContentSearchManager.Locator.GetInstance<Sitecore.Abstractions.IFactory>();
            this.indexes = new Dictionary<string, ISearchIndex>();
            AddIndex(this);
        }

        public AzureIndex(string name, string folder, IIndexPropertyStore propertyStore)
      : this(name.Replace("_", "-").ToLower(), folder, propertyStore, (string) null)
        {
            this.settings = ContentSearchManager.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
            this.factory = ContentSearchManager.Locator.GetInstance<Sitecore.Abstractions.IFactory>();
            this.indexes = new Dictionary<string, ISearchIndex>();
            AddIndex(this);
        }

        protected AzureIndex(string name)
        {
            Assert.ArgumentNotNullOrEmpty(name, "name");
            Name = name.Replace("_", "-").ToLower();
            this.settings = ContentSearchManager.Locator.GetInstance<Sitecore.ContentSearch.Abstractions.ISettings>();
            this.factory = ContentSearchManager.Locator.GetInstance<Sitecore.Abstractions.IFactory>();
            this.indexes = new Dictionary<string, ISearchIndex>();
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

        public override string Name { get; }

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
            
        }

        public override IProviderDeleteContext CreateDeleteContext()
        {
            return null;
        }

        public override IProviderSearchContext CreateSearchContext(SearchSecurityOptions options = SearchSecurityOptions.Default)
        {
            return null;
        }

        public override IProviderUpdateContext CreateUpdateContext()
        {
            //this.EnsureInitialized();
            //ICommitPolicyExecutor commitPolicyExecutor = (ICommitPolicyExecutor)this.CommitPolicyExecutor.Clone();
            //commitPolicyExecutor.Initialize((ISearchIndex)this);
            return (IProviderUpdateContext)(new AzureUpdateContext((ISearchIndex)this));
        }

        public override void Delete(IIndexableUniqueId indexableUniqueId)
        {
            
        }

        public override void Delete(IIndexableId indexableId)
        {
            
        }

        public override void Delete(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
        {
            
        }

        public override void Delete(IIndexableId indexableId, IndexingOptions indexingOptions)
        {
            
        }

        public override void Initialize()
        {
            //this.Crawlers = new List<IProviderCrawler>();
            var indexConfiguration = this.Configuration as AzureIndexConfiguration;
            if (indexConfiguration == null)
                throw new ConfigurationErrorsException("Index has no configuration.");

            InitializeSearchIndexInitializables(this.Configuration, this.Crawlers);

            string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

            // Create an HTTP reference to the catalog index
            _searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
            _indexClient = _searchClient.Indexes.GetClient(Name);
        }

        public override ISearchIndexSummary Summary { get; }

        public override ISearchIndexSchema Schema { get; }

        public override IIndexPropertyStore PropertyStore { get; set; }

        public override AbstractFieldNameTranslator FieldNameTranslator { get; set; }

        public override ProviderIndexConfiguration Configuration { get; set; }

        public override Sitecore.ContentSearch.IIndexOperations Operations { get; }

        public override bool IsSharded { get; }

        public override bool EnableItemLanguageFallback { get; set; }

        public override bool EnableFieldLanguageFallback { get; set; }

        public override IShardingStrategy ShardingStrategy { get; set; }

        public override IShardFactory ShardFactory { get; }

        public override IEnumerable<Shard> Shards { get; }

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
                ProviderIndexConfiguration @object = this.factory.CreateObject<ProviderIndexConfiguration>(configNode);
                if (@object == null)
                    throw new ConfigurationException("Unable to create configuration object from path specified in setting 'ContentSearch.DefaultIndexConfigurationPath'. Please check your config.");
                index.Configuration = @object;
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
            //TODO:  Build the Azure Index
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            BuildAzureIndex();

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

        private void BuildAzureIndex()
        {          
            try
            {
                if (this.Configuration.DocumentOptions.IndexAllFields)
                {
                    
                }

                var definition = new Index()
                {
                    Name = this.Name,
                    Fields = new[]
                    {
                        new Field("FEATURE_ID",     DataType.String)         { IsKey = true,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("FEATURE_NAME",   DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("FEATURE_CLASS",  DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("STATE_ALPHA",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("STATE_NUMERIC",  DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("COUNTY_NAME",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("COUNTY_NUMERIC", DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("ELEV_IN_M",      DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("ELEV_IN_FT",     DataType.Int32)          { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("MAP_NAME",       DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = true,  IsSortable = true,  IsFacetable = false, IsRetrievable = true},
                        new Field("DESCRIPTION",    DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("HISTORY",        DataType.String)         { IsKey = false, IsSearchable = true,  IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                        new Field("DATE_CREATED",   DataType.DateTimeOffset) { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true},
                        new Field("DATE_EDITED",    DataType.DateTimeOffset) { IsKey = false, IsSearchable = false, IsFilterable = true,  IsSortable = true,  IsFacetable = true,  IsRetrievable = true}
                    }
                };

                _searchClient.Indexes.CreateOrUpdate(definition);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating index: {0}\r\n", ex.Message.ToString());
            }
        }

        public override Task RebuildAsync(IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            return Task.Run(() => Rebuild(indexingOptions), cancellationToken);
        }

        public override void Refresh()
        {
            //TODO:  What to do when no starting point?
        }

        public override void Refresh(IIndexable indexableStartingPoint)
        {
            Refresh(indexableStartingPoint, IndexingOptions.Default);
        }

        public override void Refresh(IIndexable indexableStartingPoint, IndexingOptions indexingOptions)
        {
            //TODO:  Refresh Azure Indexes
        }

        public override Task RefreshAsync(IIndexable indexableStartingPoint, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            return Task.Run(() => Refresh(indexableStartingPoint, indexingOptions), cancellationToken);
        }

        public override void RemoveAllCrawlers()
        {
            
        }

        public override bool RemoveCrawler(IProviderCrawler crawler)
        {
            return false;
        }

        public override void Reset()
        {
            
        }

        public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds)
        {
            
        }

        public override void Update(IEnumerable<IndexableInfo> indexableInfo)
        {
           
        }

        public override void Update(IIndexableUniqueId indexableUniqueId)
        {
            
        }

        public override void Update(IEnumerable<IIndexableUniqueId> indexableUniqueIds, IndexingOptions indexingOptions)
        {
            
        }

        public override void Update(IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions)
        {
            
        }

        protected override void PerformRebuild(IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override void PerformRefresh(IIndexable indexableStartingPoint, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
