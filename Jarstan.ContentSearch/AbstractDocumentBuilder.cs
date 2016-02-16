using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Data.LanguageFallback;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Linq;

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
            this.Options = context.Index.Configuration.DocumentOptions;
            this.Indexable = indexable;
            this.Index = context.Index;
            this.Document = new T();
            this.IsParallel = context.IsParallel;
            this.ParallelOptions = context.ParallelOptions;
            Item obj = (Item)(indexable as SitecoreIndexableItem);
            if (obj != null)
            {
                this.IsTemplate = TemplateManager.IsTemplate(obj);
                this.IsMedia = obj.Paths.IsMediaItem;
            }
            this.Settings = this.Index.Locator.GetInstance<IContentSearchConfigurationSettings>();
        }

        public virtual void AddItemFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddItemFields start"));
                if (this.Options.IndexAllFields)
                    this.Indexable.LoadAllFields();
                if (this.IsParallel)
                {
                    ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
                    Parallel.ForEach<IIndexableDataField>(this.Indexable.Fields, this.ParallelOptions, (Action<IIndexableDataField>)(f =>
                    {
                        try
                        {
                            this.CheckAndAddField(this.Indexable, f);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Enqueue(ex);
                        }
                    }));
                    if (exceptions.Count > 0)
                        throw new AggregateException((IEnumerable<Exception>)exceptions);
                }
                else
                {
                    foreach (IIndexableDataField field in this.Indexable.Fields)
                        this.CheckAndAddField(this.Indexable, field);
                }
            }
            finally
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddItemFields End"));
            }
        }

        private void CheckAndAddField(IIndexable indexable, IIndexableDataField field)
        {
            string name = field.Name;
            if (this.IsTemplate && this.Options.HasExcludedTemplateFields && (this.Options.ExcludedTemplateFields.Contains(name) || this.Options.ExcludedTemplateFields.Contains(field.Id.ToString())))
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was excluded.", field.Id, (object)field.Name, (object)field.TypeKey)));
            else if (this.IsMedia && this.Options.HasExcludedMediaFields && this.Options.ExcludedMediaFields.Contains(field.Name))
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Media field was excluded.", field.Id, (object)field.Name, (object)field.TypeKey)));
            }
            else
            {
                if (!this.Options.ExcludedFields.Contains(field.Id.ToString()))
                {
                    if (!this.Options.ExcludedFields.Contains(name))
                    {
                        try
                        {
                            if (this.Options.IndexAllFields)
                            {
                                using (new LanguageFallbackFieldSwitcher(new bool?(this.Index.EnableFieldLanguageFallback)))
                                {
                                    this.AddField(field);
                                    return;
                                }
                            }
                            else if (this.Options.IncludedFields.Contains(name) || this.Options.IncludedFields.Contains(field.Id.ToString()))
                            {
                                using (new LanguageFallbackFieldSwitcher(new bool?(this.Index.EnableFieldLanguageFallback)))
                                {
                                    this.AddField(field);
                                    return;
                                }
                            }
                            else
                            {
                                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was not included.", field.Id, (object)field.Name, (object)field.TypeKey)));
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!this.Settings.StopOnCrawlFieldError())
                            {
                                CrawlingLog.Log.Fatal(string.Format("Could not add field {1} : {2} for indexable {0}", (object)indexable.UniqueId, field.Id, (object)field.Name), ex);
                                return;
                            }
                            throw;
                        }
                    }
                }
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Skipping field id:{0}, name:{1}, typeKey:{2} - Field was excluded.", field.Id, (object)field.Name, (object)field.TypeKey)));
            }
        }

        public virtual void AddSpecialField(string fieldName, object fieldValue, bool append = false)
        {
            if (this.Options.HasExcludedFields && (this.IsTemplate && this.Options.HasExcludedTemplateFields && this.Options.ExcludedTemplateFields.Contains(fieldName) || this.IsMedia && this.Options.HasExcludedMediaFields && this.Options.ExcludedMediaFields.Contains(fieldName) || this.Options.ExcludedFields.Contains(fieldName)))
                return;
            this.AddField(fieldName, fieldValue, append);
        }

        public virtual void AddSpecialFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddSpecialFields Start"));
                this.AddSpecialField("s_key", this.Indexable.UniqueId.GetHashCode(), false);
                this.AddSpecialField("s_uniqueid", (object)this.Indexable.UniqueId.ToString(), false);
                this.AddSpecialField("s_datasource", (object)this.Indexable.DataSource.ToLowerInvariant(), false);
                this.AddSpecialField("s_indexname", (object)this.Index.Name.ToLowerInvariant(), false);
                IHashedIndexable hashedIndexable = this.Indexable as IHashedIndexable;
                if (hashedIndexable != null)
                    this.AddSpecialField("s_hash", (object)hashedIndexable.GetIndexableHashCode(), false);
                IDocumentTypedIndexable documentTypedIndexable = this.Indexable as IDocumentTypedIndexable;
                if (documentTypedIndexable != null)
                    this.AddSpecialField("s_documenttype", documentTypedIndexable.DocumentType, false);
                IIndexableBuiltinFields indexableBuiltinFields = this.Indexable as IIndexableBuiltinFields;
                if (indexableBuiltinFields == null)
                    return;
                this.AddSpecialField("s_database", (object)indexableBuiltinFields.Database, false);
                this.AddSpecialField("s_language", (object)indexableBuiltinFields.Language, false);
                this.AddSpecialField("s_template", indexableBuiltinFields.TemplateId, false);
                this.AddSpecialField("s_parent", indexableBuiltinFields.Parent, false);
                if (indexableBuiltinFields.IsLatestVersion)
                    this.AddSpecialField("s_latestversion", (object)true, false);
                this.AddSpecialField("s_version", (object)indexableBuiltinFields.Version, false);
                this.AddSpecialField("s_group", indexableBuiltinFields.Group, false);
                if (indexableBuiltinFields.IsClone)
                    this.AddSpecialField("s_isclone", (object)true, false);
                this.AddSpecialField("s_fullpath", (object)indexableBuiltinFields.FullPath, false);
                if (this.Options.ExcludeAllSpecialFields)
                    return;
                this.AddSpecialField("s_name", (object)indexableBuiltinFields.Name, false);
                this.AddSpecialField("s_displayname", (object)indexableBuiltinFields.DisplayName, false);
                this.AddSpecialField("s_creator", (object)indexableBuiltinFields.CreatedBy, false);
                this.AddSpecialField("s_editor", (object)indexableBuiltinFields.UpdatedBy, false);
                this.AddSpecialField("s_templatename", (object)indexableBuiltinFields.TemplateName, false);
                this.AddSpecialField("s_created", (object)indexableBuiltinFields.CreatedDate, false);
                this.AddSpecialField("s_updated", (object)indexableBuiltinFields.UpdatedDate, false);
                this.AddSpecialField("s_path", indexableBuiltinFields.Paths, false);
                this.AddSpecialField("s_content", indexableBuiltinFields.Name + " " + indexableBuiltinFields.DisplayName, false);
                if (this.Options.Tags == null || this.Options.Tags.Length <= 0)
                    return;
                this.AddSpecialField("s_tags", this.Options.Tags, false);
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
            Assert.ArgumentNotNull((object)item, "item");
            return IdHelper.ProcessGUIDs(item.Paths.LongID.Replace('/', ' '), true);
        }

        public abstract void AddBoost();

        public abstract void AddComputedIndexFields();
    }
}
