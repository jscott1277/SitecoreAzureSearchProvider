using Sitecore;
using Sitecore.Abstractions;
using Sitecore.Collections;
using Sitecore.Common;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Pipelines.GetContextIndex;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.LanguageFallback;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Jarstan.ContentSearch
{
    public class AzureItemCrawler : HierarchicalDataCrawler<SitecoreIndexableItem>, IContextIndexRankable
    {
        private string database;
        private string root;
        private Item rootItem;
        private volatile int rootItemErrorLogged;

        public string Database
        {
            get
            {
                if (!string.IsNullOrEmpty(database))
                    return database;
                return null;
            }
            set
            {
                database = value;
            }
        }

        public string Root
        {
            get
            {
                if (string.IsNullOrEmpty(root))
                {
                    var database = ContentSearchManager.Locator.GetInstance<IFactory>().GetDatabase(this.database);
                    Assert.IsNotNull(database, "Database " + database + " does not exist");
                    using (new SecurityDisabler())
                        root = database.GetRootItem().ID.ToString();
                }
                return root;
            }
            set
            {
                root = value;
                rootItem = (Item)null;
            }
        }

        public Item RootItem
        {
            get
            {
                rootItem = GetRootItem();
                if (rootItem == null)
                    throw new InvalidOperationException(string.Format("[Index={0}, Crawler={1}, Database={2}] Root item could not be found: {3}.", index != null ? index.Name : "NULL", typeof(SitecoreItemCrawler).Name, database, root));
                return rootItem;
            }
        }

        public AzureItemCrawler()
        {
            
        }

        public AzureItemCrawler(IIndexOperations indexOperations)
          : base(indexOperations)
        {
            base.Operations = indexOperations;
        }

        private Item GetRootItem()
        {
            if (rootItem == null)
            {
                var database = ContentSearchManager.Locator.GetInstance<IFactory>().GetDatabase(this.database);
                Assert.IsNotNull(database, "Database " + database + " does not exist");
                using (new SecurityDisabler())
                {
                    rootItem = database.GetItem(Root);
                    if (rootItem == null)
                    {
                        if (rootItemErrorLogged == 0)
                        {
                            Interlocked.Increment(ref rootItemErrorLogged);
                            var message = string.Format("[Index={0}, Crawler={1}, Database={2}] Root item could not be found: {3}.", index != null ? index.Name : "NULL", typeof(SitecoreItemCrawler).Name, database, root);
                            CrawlingLog.Log.Error(message);
                            Log.Error(message, this);
                        }
                    }
                }
            }
            return rootItem;
        }

        public override void Initialize(ISearchIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            Assert.IsNotNull(Database, "Database element not set.");
            Assert.IsNotNull(Root, "Root element not set.");
            if (Operations == null)
            {
                Operations = index.Operations;
                CrawlingLog.Log.Info(string.Format("[Index={0}] Initializing {3}. DB:{1} / Root:{2}", index.Name, Database, Root, typeof(SitecoreItemCrawler).Name));
            }

           this.index = index;
        }

        public virtual int GetContextIndexRanking(IIndexable indexable)
        {
            var sitecoreIndexableItem = indexable as SitecoreIndexableItem;
            if (sitecoreIndexableItem == null || GetRootItem() == null)
                return int.MaxValue;
            var obj = (Item)sitecoreIndexableItem;
            using (new SecurityDisabler())
            {
                using (new SitecoreCachesDisabler())
                    return obj.Axes.Level - RootItem.Axes.Level;
            }
        }

        public override bool IsExcludedFromIndex(IIndexable indexable)
        {
            return IsExcludedFromIndex((SitecoreIndexableItem)indexable, true);
        }

        public override void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            Update(context, indexableUniqueId, null, indexingOptions);
        }

        public override void Update(IProviderUpdateContext context, IIndexableUniqueId indexableUniqueId, IndexEntryOperationContext operationContext, IndexingOptions indexingOptions = IndexingOptions.Default)
        {
            Assert.ArgumentNotNull(indexableUniqueId, "indexableUniqueId");

            if (!ShouldStartIndexing(indexingOptions))
            {
                return;
            }

            if (IsExcludedFromIndex(indexableUniqueId, true))
            {
                return;
            }

            if (operationContext != null)
            {
                if (operationContext.NeedUpdateChildren)
                {
                    var obj = Sitecore.Data.Database.GetItem((indexableUniqueId as SitecoreItemUniqueId));
                    if (obj != null)
                    {
                        UpdateHierarchicalRecursive(context, obj, CancellationToken.None);
                        return;
                    }
                }

                if (operationContext.NeedUpdatePreviousVersion)
                {
                    var obj = Sitecore.Data.Database.GetItem((indexableUniqueId as SitecoreItemUniqueId));
                    if (obj != null)
                        UpdatePreviousVersion(obj, context);
                }
            }
            var indexableAndCheckDeletes = GetIndexableAndCheckDeletes(indexableUniqueId);
            if (indexableAndCheckDeletes == null)
            {
                if (GroupShouldBeDeleted(indexableUniqueId.GroupId))
                {
                    Delete(context, indexableUniqueId.GroupId, IndexingOptions.Default);
                }
                else
                {
                    Delete(context, indexableUniqueId, IndexingOptions.Default);
                }
            }
            else
            {
                DoUpdate(context, indexableAndCheckDeletes, operationContext);
            }
        }

        protected override bool IsExcludedFromIndex(SitecoreIndexableItem indexable, bool checkLocation = false)
        {
            var item = (Item)indexable;
            Assert.ArgumentNotNull(item, "item");
            var documentOptions = DocumentOptions;
            Assert.IsNotNull(documentOptions, "DocumentOptions");
            if (!item.Database.Name.Equals(Database, StringComparison.InvariantCultureIgnoreCase))
            {
                Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", index.Name, item.Uri);
                return true;
            }
            if (checkLocation)
            {
                if (GetRootItem() == null)
                    return true;
                if (!IsAncestorOf(item))
                {
                    Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", index.Name, item.Uri);
                    return true;
                }
            }
            if (documentOptions.HasIncludedTemplates)
            {
                if (documentOptions.HasExcludedTemplates)
                    CrawlingLog.Log.Warn("You have specified both IncludeTemplates and ExcludeTemplates. This logic is not supported. Exclude templates will be ignored.");
                if (documentOptions.IncludedTemplates.Contains(item.TemplateID.ToString()))
                    return false;
                Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", index.Name, item.Uri);
                return true;
            }
            if (documentOptions.HasIncludedTemplates)
            {
                if (documentOptions.HasExcludedTemplates)
                    CrawlingLog.Log.Warn("You have specified both IncludeTemplates and ExcludeTemplates. This logic is not supported. Exclude templates will be ignored.");
                if (documentOptions.IncludedTemplates.Contains(item.TemplateID.ToString()))
                    return false;
                Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", index.Name, item.Uri);
                return true;
            }
            if (!documentOptions.ExcludedTemplates.Contains(item.TemplateID.ToString()))
                return false;
            Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", index.Name, item.Uri);
            return true;
        }

        protected virtual bool IsAncestorOf(Item item)
        {
            using (new SecurityDisabler())
            {
                using (new WriteCachesDisabler())
                {
                    if (RootItem != null)
                        return item.Paths.LongID.StartsWith(RootItem.Paths.LongID, StringComparison.InvariantCulture);
                }
            }
            return false;
        }

        protected override bool IsExcludedFromIndex(IIndexableUniqueId indexableUniqueId)
        {
            return IsExcludedFromIndex(indexableUniqueId, false);
        }

        protected override bool IsExcludedFromIndex(IIndexableUniqueId indexableUniqueId, bool checkLocation)
        {
            var itemUri = (ItemUri)(indexableUniqueId as SitecoreItemUniqueId);
            if (itemUri != null && !itemUri.DatabaseName.Equals(Database, StringComparison.InvariantCultureIgnoreCase))
                return true;
            if (checkLocation)
            {
                var item = Sitecore.Data.Database.GetItem((indexableUniqueId as SitecoreItemUniqueId));
                if (item != null && !IsAncestorOf(item))
                    return true;
            }
            return false;
        }

        protected override void DoAdd(IProviderUpdateContext context, SitecoreIndexableItem indexable)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(indexable, "indexable");
            using (new LanguageFallbackItemSwitcher(new bool?(context.Index.EnableItemLanguageFallback)))
            {
                Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:adding", context.Index.Name, indexable.UniqueId, indexable.AbsolutePath);
                if (!IsExcludedFromIndex(indexable, false))
                {
                    foreach (var language in indexable.Item.Languages)
                    {
                        Item item;
                        using (new WriteCachesDisabler())
                            item = indexable.Item.Database.GetItem(indexable.Item.ID, language, Sitecore.Data.Version.Latest);
                        if (item == null)
                        {
                            CrawlingLog.Log.Warn(string.Format("SitecoreItemCrawler : AddItem : Could not build document data {0} - Latest version could not be found. Skipping.", indexable.Item.Uri));
                        }
                        else
                        {
                            Item[] versions;
                            using (new WriteCachesDisabler())
                                versions = item.Versions.GetVersions(false);
                            foreach (var version in versions)
                            {
                                var sitecoreIndexableItem = (SitecoreIndexableItem)version;
                                var indexableBuiltinFields = (IIndexableBuiltinFields)sitecoreIndexableItem;
                                indexableBuiltinFields.IsLatestVersion = indexableBuiltinFields.Version == item.Version.Number;
                                sitecoreIndexableItem.IndexFieldStorageValueFormatter = context.Index.Configuration.IndexFieldStorageValueFormatter;
                                Operations.Add(sitecoreIndexableItem, context, index.Configuration);
                            }
                        }
                    }
                }
                Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:added", context.Index.Name, indexable.UniqueId, indexable.AbsolutePath);
            }
        }

        protected override void DoUpdate(IProviderUpdateContext context, SitecoreIndexableItem indexable)
        {
            DoUpdate(context, indexable, null);
        }

        protected override void DoUpdate(IProviderUpdateContext context, SitecoreIndexableItem indexable, IndexEntryOperationContext operationContext)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(indexable, "indexable");
            using (new LanguageFallbackItemSwitcher(new bool?(Index.EnableItemLanguageFallback)))
            {
                if (IndexUpdateNeedDelete(indexable))
                {
                    Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:deleteitem", index.Name, indexable.UniqueId, indexable.AbsolutePath);
                    Operations.Delete(indexable, context);
                }
                else
                {
                    Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updatingitem", index.Name, indexable.UniqueId, indexable.AbsolutePath);
                    if (!IsExcludedFromIndex(indexable, true))
                    {
                        if (operationContext != null && !operationContext.NeedUpdateAllVersions)
                        {
                            UpdateItemVersion(context, indexable, operationContext);
                        }
                        else
                        {
                            Language[] languageArray;
                            if (operationContext == null || operationContext.NeedUpdateAllLanguages)
                                languageArray = indexable.Item.Languages;
                            else
                                languageArray = new Language[1]
                                {
                                    indexable.Item.Language
                                };
                            foreach (var language in languageArray)
                            {
                                Item item;
                                using (new WriteCachesDisabler())
                                    item = indexable.Item.Database.GetItem(indexable.Item.ID, language, Sitecore.Data.Version.Latest);
                                if (item == null)
                                {
                                    CrawlingLog.Log.Warn(string.Format("SitecoreItemCrawler : Update : Latest version not found for item {0}. Skipping.", indexable.Item.Uri));
                                }
                                else
                                {
                                    Item[] versions;
                                    using (new SitecoreCachesDisabler())
                                        versions = item.Versions.GetVersions(false);
                                    foreach (var version in versions)
                                        UpdateItemVersion(context, version, operationContext);
                                }
                            }
                        }
                        Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updateditem", index.Name, indexable.UniqueId, indexable.AbsolutePath);
                    }
                    if (!DocumentOptions.ProcessDependencies)
                        return;
                    Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:updatedependents", index.Name, indexable.UniqueId, indexable.AbsolutePath);
                    UpdateDependents(context, indexable);
                }
            }
        }

        [Obsolete("Use UpdateItemVersion(IProviderUpdateContext context, Item version, IndexEntryOperationContext operationContext) instead")]
        protected virtual void UpdateItemVersion(IProviderUpdateContext context, Item version)
        {
            UpdateItemVersion(context, version, new IndexEntryOperationContext());
        }

        protected virtual void UpdateItemVersion(IProviderUpdateContext context, Item version, IndexEntryOperationContext operationContext)
        {
            var versionIndexable = PrepareIndexableVersion(version, context);
            Operations.Update(versionIndexable, context, context.Index.Configuration);
            UpdateClones(context, versionIndexable);
            UpdateLanguageFallbackDependentItems(context, versionIndexable, operationContext);
        }

        private void UpdateClones(IProviderUpdateContext context, SitecoreIndexableItem versionIndexable)
        {
            IEnumerable<Item> clones;
            using (new WriteCachesDisabler())
                clones = versionIndexable.Item.GetClones(false);
            foreach (Item clone in clones)
            {
                SitecoreIndexableItem sitecoreIndexableItem = PrepareIndexableVersion(clone, context);
                if (!IsExcludedFromIndex(clone, false))
                    Operations.Update(sitecoreIndexableItem, context, context.Index.Configuration);
            }
        }

        private void UpdateLanguageFallbackDependentItems(IProviderUpdateContext context, SitecoreIndexableItem versionIndexable, IndexEntryOperationContext operationContext)
        {
            if (operationContext == null || operationContext.NeedUpdateAllLanguages)
                return;
            var item = versionIndexable.Item;
            bool? currentValue1 = Switcher<bool?, LanguageFallbackFieldSwitcher>.CurrentValue;
            if ((!currentValue1.GetValueOrDefault() ? 1 : (!currentValue1.HasValue ? 1 : 0)) != 0)
            {
                bool? currentValue2 = Switcher<bool?, LanguageFallbackItemSwitcher>.CurrentValue;
                if ((!currentValue2.GetValueOrDefault() ? 1 : (!currentValue2.HasValue ? 1 : 0)) != 0 || StandardValuesManager.IsStandardValuesHolder(item) && item.Fields[FieldIDs.EnableItemFallback].GetValue(false) != "1")
                    return;
                using (new LanguageFallbackItemSwitcher(new bool?(false)))
                {
                    if (item.Fields[FieldIDs.EnableItemFallback].GetValue(true, true, false) != "1")
                        return;
                }
            }
            if (!item.Versions.IsLatestVersion())
                return;
            foreach (var indexable in Enumerable.Select(Enumerable.Where<Item>(Enumerable.SelectMany(LanguageFallbackManager.GetDependentLanguages(item.Language, item.Database, item.ID), language =>
            {
                var item2 = item.Database.GetItem(item.ID, language);
                if (item2 == null)
                    return new Item[0];
                return item2.Versions.GetVersions();
            }), item1 => !IsExcludedFromIndex(item1, false)), item1 => PrepareIndexableVersion(item1, context)))
                Operations.Update(indexable, context, context.Index.Configuration);
        }

        internal SitecoreIndexableItem PrepareIndexableVersion(Item item, IProviderUpdateContext context)
        {
            var sitecoreIndexableItem = (SitecoreIndexableItem)item;
            ((IIndexableBuiltinFields)sitecoreIndexableItem).IsLatestVersion = item.Versions.IsLatestVersion();
            sitecoreIndexableItem.IndexFieldStorageValueFormatter = context.Index.Configuration.IndexFieldStorageValueFormatter;
            return sitecoreIndexableItem;
        }

        public override void RebuildFromRoot(IProviderUpdateContext context, IndexingOptions indexingOptions)
        {
            RebuildFromRoot(context, indexingOptions, CancellationToken.None);
        }

        //TODO: Could be removed, hasn't changed from what is being overridden, just wanted to see 
        public override void RebuildFromRoot(IProviderUpdateContext context, IndexingOptions indexingOptions, CancellationToken cancellationToken)
        {
            Assert.ArgumentNotNull(context, "context");
            if (!ShouldStartIndexing(indexingOptions))
                return;
            var indexableRoot = GetIndexableRoot();
            Assert.IsNotNull(indexableRoot, "RebuildFromRoot: Unable to retrieve root item");
            Assert.IsNotNull(DocumentOptions, "DocumentOptions");
            context.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:addingrecursive", context.Index.Name, indexableRoot.UniqueId, indexableRoot.AbsolutePath);
            AddHierarchicalRecursive(indexableRoot, context, index.Configuration, cancellationToken);
            context.Index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:addedrecursive", context.Index.Name, indexableRoot.UniqueId, indexableRoot.AbsolutePath);
        }

        protected override SitecoreIndexableItem GetIndexable(IIndexableUniqueId indexableUniqueId)
        {
            using (new SecurityDisabler())
            {
                using (new WriteCachesDisabler())
                    return Sitecore.Data.Database.GetItem((indexableUniqueId as SitecoreItemUniqueId));
            }
        }

        protected override bool GroupShouldBeDeleted(IIndexableId indexableId)
        {
            Assert.ArgumentNotNull(indexableId, "indexableId");
            var sitecoreItemId = indexableId as SitecoreItemId;
            if (sitecoreItemId == null)
                return false;
            var database = Factory.GetDatabase(Database);
            Item item;
            using (new WriteCachesDisabler())
                item = database.GetItem(sitecoreItemId);
            return item == null;
        }

        protected override SitecoreIndexableItem GetIndexableAndCheckDeletes(IIndexableUniqueId indexableUniqueId)
        {
            var itemUri = (ItemUri)(indexableUniqueId as SitecoreItemUniqueId);
            using (new SecurityDisabler())
            {
                Item item;
                using (new WriteCachesDisabler())
                    item = Sitecore.Data.Database.GetItem(itemUri);
                if (item != null)
                {
                    var item2 = Sitecore.Data.Database.GetItem(new ItemUri(itemUri.ItemID, itemUri.Language, Sitecore.Data.Version.Latest, itemUri.DatabaseName));
                    Sitecore.Data.Version[] versionArray;
                    using (new WriteCachesDisabler())
                        versionArray = item2.Versions.GetVersionNumbers() ?? new Sitecore.Data.Version[0];
                    if (Enumerable.All(versionArray, v => v.Number != itemUri.Version.Number))
                        item2 = null;
                }
                return item;
            }
        }

        protected override bool IndexUpdateNeedDelete(SitecoreIndexableItem indexable)
        {
            return false;
        }

        protected override IEnumerable<IIndexableUniqueId> GetIndexablesToUpdateOnDelete(IIndexableUniqueId indexableUniqueId)
        {
            var itemUri = indexableUniqueId.Value as ItemUri;
            using (new SecurityDisabler())
            {
                var latestItemUri = new ItemUri(itemUri.ItemID, itemUri.Language, Sitecore.Data.Version.Latest, itemUri.DatabaseName);
                Item latestItem;
                using (new WriteCachesDisabler())
                    latestItem = Sitecore.Data.Database.GetItem(latestItemUri);
                if (latestItem != null && latestItem.Version.Number < itemUri.Version.Number)
                    yield return new SitecoreItemUniqueId(latestItem.Uri);
            }
        }

        public override SitecoreIndexableItem GetIndexableRoot()
        {
            using (new SecurityDisabler())
                return RootItem;
        }

        protected override IEnumerable<IIndexableId> GetIndexableChildrenIds(SitecoreIndexableItem parent)
        {
            var childList = GetChildList(parent.Item);
            if (childList.Count == 0)
                return null;
            return Enumerable.Select(childList, (Func<Item, SitecoreItemId>)(i => i.ID));
        }

        protected override IEnumerable<SitecoreIndexableItem> GetIndexableChildren(SitecoreIndexableItem parent)
        {
            var childList = GetChildList(parent.Item);
            if (childList.Count == 0)
                return null;
            return Enumerable.Select(childList, (Func<Item, SitecoreIndexableItem>)(i => i));
        }

        protected virtual ChildList GetChildList(Item parent)
        {
            using (new WriteCachesDisabler())
                return parent.GetChildren(ChildListOptions.IgnoreSecurity | ChildListOptions.SkipSorting);
        }

        protected override SitecoreIndexableItem GetIndexable(IIndexableId indexableId, CultureInfo culture)
        {
            using (new SecurityDisabler())
            {
                using (new WriteCachesDisabler())
                {
                    var language = LanguageManager.GetLanguage(culture.Name, RootItem.Database);
                    return ItemManager.GetItem((indexableId as SitecoreItemId), language, Sitecore.Data.Version.Latest, RootItem.Database, SecurityCheck.Disable);
                }
            }
        }

        private void UpdatePreviousVersion(Item item, IProviderUpdateContext context)
        {
            Sitecore.Data.Version[] versionArray;
            using (new WriteCachesDisabler())
                versionArray = item.Versions.GetVersionNumbers() ?? new Sitecore.Data.Version[0];
            int index = Enumerable.ToList(versionArray).FindIndex((Predicate<Sitecore.Data.Version>)(version => version.Number == item.Version.Number));
            if (index < 1)
                return;
            var previousVersion = versionArray[index - 1];
            var version1 = Enumerable.FirstOrDefault(versionArray, (Func<Sitecore.Data.Version, bool>)(version => version == previousVersion));
            var sitecoreIndexableItem = (SitecoreIndexableItem)Sitecore.Data.Database.GetItem(new ItemUri(item.ID, item.Language, version1, item.Database.Name));
            if (sitecoreIndexableItem == null)
                return;
            ((IIndexableBuiltinFields)sitecoreIndexableItem).IsLatestVersion = false;
            sitecoreIndexableItem.IndexFieldStorageValueFormatter = context.Index.Configuration.IndexFieldStorageValueFormatter;
            Operations.Update(sitecoreIndexableItem, context, this.index.Configuration);
        }
    }
}
