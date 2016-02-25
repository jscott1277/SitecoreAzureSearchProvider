using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Data.LanguageFallback;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch
{
    public abstract class AbstractDocumentBuilder<T> where T : new()
    {
        public IDocumentBuilderOptions Options { get; private set; }

        public IIndexable Indexable { get; private set; }

        public T Document { get; private set; }

        public ISearchIndex Index { get; private set; }

        public bool IsTemplate { get; private set; }

        public bool IsMedia { get; private set; }

        protected bool IsParallel { get; private set; }

        protected ParallelOptions ParallelOptions { get; private set; }

        protected IContentSearchConfigurationSettings Settings { get; set; }

        protected AbstractDocumentBuilder(IIndexable indexable, IProviderUpdateContext context)
        {
            Assert.ArgumentNotNull(indexable, "indexable");
            Assert.ArgumentNotNull(context, "context");
            Options = context.Index.Configuration.DocumentOptions;
            Indexable = indexable;
            Index = context.Index;
            Document = new T();
            IsParallel = context.IsParallel;
            ParallelOptions = context.ParallelOptions;
            var obj = (Item)(indexable as SitecoreIndexableItem);
            if (obj != null)
            {
                IsTemplate = TemplateManager.IsTemplate(obj);
                IsMedia = obj.Paths.IsMediaItem;
            }
            Settings = Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
        }

        public virtual void AddItemFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug(() => "AddItemFields start");
                if (Options.IndexAllFields)
                    Indexable.LoadAllFields();
                if (IsParallel)
                {
                    var exceptions = new ConcurrentQueue<Exception>();
                    Parallel.ForEach(Indexable.Fields, ParallelOptions, f =>
                    {
                        try
                        {
                            CheckAndAddField(Indexable, f);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Enqueue(ex);
                        }
                    });
                    if (exceptions.Count > 0)
                        throw new AggregateException(exceptions);
                }
                else
                {
                    foreach (var field in Indexable.Fields)
                        CheckAndAddField(Indexable, field);
                }
            }
            finally
            {
                VerboseLogging.CrawlingLogDebug(() => "AddItemFields End");
            }
        }

        private void CheckAndAddField(IIndexable indexable, IIndexableDataField field)
        {
            var name = field.Name;
            if (IsTemplate && Options.HasExcludedTemplateFields && (Options.ExcludedTemplateFields.Contains(name) || Options.ExcludedTemplateFields.Contains(field.Id.ToString())))
                VerboseLogging.CrawlingLogDebug(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was excluded.", field.Id, field.Name, field.TypeKey));
            else if (IsMedia && Options.HasExcludedMediaFields && Options.ExcludedMediaFields.Contains(field.Name))
            {
                VerboseLogging.CrawlingLogDebug(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Media field was excluded.", field.Id, field.Name, field.TypeKey));
            }
            else
            {
                if (!Options.ExcludedFields.Contains(field.Id.ToString()))
                {
                    if (!Options.ExcludedFields.Contains(name))
                    {
                        try
                        {
                            if (Options.IndexAllFields)
                            {
                                using (new LanguageFallbackFieldSwitcher(new bool?(Index.EnableFieldLanguageFallback)))
                                {
                                    AddField(field);
                                    return;
                                }
                            }
                            else if (Options.IncludedFields.Contains(name) || Options.IncludedFields.Contains(field.Id.ToString()))
                            {
                                using (new LanguageFallbackFieldSwitcher(new bool?(Index.EnableFieldLanguageFallback)))
                                {
                                    AddField(field);
                                    return;
                                }
                            }
                            else
                            {
                                VerboseLogging.CrawlingLogDebug(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was not included.", field.Id, field.Name, field.TypeKey));
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!Settings.StopOnCrawlFieldError())
                            {
                                CrawlingLog.Log.Fatal(string.Format("Could not add field {1} : {2} for indexable {0}", indexable.UniqueId, field.Id, field.Name), ex);
                                return;
                            }
                            throw;
                        }
                    }
                }
                VerboseLogging.CrawlingLogDebug(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was excluded.", field.Id, field.Name, field.TypeKey));
            }
        }

        public virtual void AddSpecialField(string fieldName, object fieldValue, bool append = false)
        {
            if (Options.HasExcludedFields && (IsTemplate && Options.HasExcludedTemplateFields && Options.ExcludedTemplateFields.Contains(fieldName) || IsMedia && Options.HasExcludedMediaFields && Options.ExcludedMediaFields.Contains(fieldName) || Options.ExcludedFields.Contains(fieldName)))
                return;
            AddField(fieldName, fieldValue, append);
        }

        public virtual void AddSpecialFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug(() => "AddSpecialFields Start");
                AddSpecialField("s_key", Indexable.UniqueId.GetHashCode(), false);
                AddSpecialField("s_uniqueid", Indexable.UniqueId.ToString(), false);
                AddSpecialField("s_datasource", Indexable.DataSource.ToLowerInvariant(), false);
                AddSpecialField("s_indexname", Index.Name.ToLowerInvariant(), false);
                var hashedIndexable = Indexable as IHashedIndexable;
                if (hashedIndexable != null)
                    AddSpecialField("s_hash", hashedIndexable.GetIndexableHashCode(), false);
                var documentTypedIndexable = Indexable as IDocumentTypedIndexable;
                if (documentTypedIndexable != null)
                    AddSpecialField("s_documenttype", documentTypedIndexable.DocumentType, false);
                var indexableBuiltinFields = Indexable as IIndexableBuiltinFields;
                if (indexableBuiltinFields == null)
                    return;
                AddSpecialField("s_database", indexableBuiltinFields.Database, false);
                AddSpecialField("s_language", indexableBuiltinFields.Language, false);
                AddSpecialField("s_template", indexableBuiltinFields.TemplateId, false);
                AddSpecialField("s_parent", indexableBuiltinFields.Parent, false);
                if (indexableBuiltinFields.IsLatestVersion)
                    AddSpecialField("s_latestversion", true, false);
                AddSpecialField("s_version", indexableBuiltinFields.Version, false);
                AddSpecialField("s_group", indexableBuiltinFields.Group, false);
                if (indexableBuiltinFields.IsClone)
                    AddSpecialField("s_isclone", true, false);
                AddSpecialField("s_fullpath", indexableBuiltinFields.FullPath, false);
                if (Options.ExcludeAllSpecialFields)
                    return;
                AddSpecialField("s_name", indexableBuiltinFields.Name, false);
                AddSpecialField("s_displayname", indexableBuiltinFields.DisplayName, false);
                AddSpecialField("s_creator", indexableBuiltinFields.CreatedBy, false);
                AddSpecialField("s_editor", indexableBuiltinFields.UpdatedBy, false);
                AddSpecialField("s_templatename", indexableBuiltinFields.TemplateName, false);
                AddSpecialField("s_created", indexableBuiltinFields.CreatedDate, false);
                AddSpecialField("s_updated", indexableBuiltinFields.UpdatedDate, false);
                AddSpecialField("s_path", indexableBuiltinFields.Paths, false);
                AddSpecialField("s_content", indexableBuiltinFields.Name + " " + indexableBuiltinFields.DisplayName, false);
                if (Options.Tags == null || Options.Tags.Length <= 0)
                    return;
                AddSpecialField("s_tags", Options.Tags, false);
            }
            finally
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddSpecialFields End"));
            }
        }

        public abstract void AddField(string fieldName, object fieldValue, bool append = false);

        public abstract void AddField(IIndexableDataField field);

        public virtual string GetItemPath(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            return IdHelper.ProcessGUIDs(item.Paths.LongID.Replace('/', ' '), true);
        }

        public abstract void AddBoost();

        public abstract void AddComputedIndexFields();
    }
}
