using Microsoft.Azure.Search.Models;
using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Pipelines.CleanUp;
using Sitecore.ContentSearch.Pipelines.IndexingFilters;
using Sitecore.Diagnostics;
using Sitecore.Reflection;
using Jarstan.ContentSearch.AzureProvider;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureIndexOperations : IIndexOperations
    {
        private readonly IAzureProviderIndex index;

        public AzureIndexOperations(IAzureProviderIndex index)
        {
            Assert.ArgumentNotNull((object)index, "index");
            //Assert.IsNotNull((object)index.Schema, "Index schema not available.");
            this.index = index;
        }

        internal AzureIndexOperations()
        {
        }

        public void Update(IIndexable indexable, IProviderUpdateContext context, ProviderIndexConfiguration indexConfiguration)
        {
            var data = this.BuildDataToIndex(context, indexable);
            if (data == null)
                return;
            if (data.IsEmpty)
                CrawlingLog.Log.Warn(string.Format("AzureIndexOperations.Update(): IndexVersion produced a NULL doc for UniqueId {0}. Skipping.", (object)indexable.UniqueId), (Exception)null);
            Document document = data.BuildDocument();
            AzureIndexOperations.LogIndexOperation((Func<string>)(() => string.Format("Updating indexable UniqueId:{0}, Culture:{1}, DataSource:{2}, Index:{3}", (object)indexable.UniqueId, (object)indexable.Culture, (object)indexable.DataSource, (object)context.Index.Name)), data, document);
            context.UpdateDocument(document, data.UpdateTerm, data.Culture != null ? (IExecutionContext)new CultureExecutionContext(data.Culture) : (IExecutionContext)null);
        }

        public void Delete(IIndexable indexable, IProviderUpdateContext context)
        {
            Assert.ArgumentNotNull((object)indexable, "indexable");
            VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Deleting indexable UniqueId:{0}, Index:{1}", (object)indexable.UniqueId, (object)context.Index.Name)));
            IProviderUpdateContextEx providerUpdateContextEx = context as IProviderUpdateContextEx;
            if (providerUpdateContextEx != null)
                providerUpdateContextEx.Delete(indexable.UniqueId, new IExecutionContext[1]
                {
          indexable.Culture != null ? (IExecutionContext) new CultureExecutionContext(indexable.Culture) : (IExecutionContext) null
                });
            else
                context.Delete(indexable.UniqueId);
        }

        public void Delete(IIndexableId id, IProviderUpdateContext context)
        {
            Assert.ArgumentNotNull((object)id, "id");
            VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Deleting indexable id:{0}, Index:{1}", (object)id, (object)context.Index.Name)));
            context.Delete(id);
        }

        public void Delete(IIndexableUniqueId indexableUniqueId, IProviderUpdateContext context)
        {
            Assert.ArgumentNotNull((object)indexableUniqueId, "indexableUniqueId");
            VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Deleting indexable UniqueId:{0}, Index:{1}", (object)indexableUniqueId, (object)context.Index.Name)));
            context.Delete(indexableUniqueId);
        }

        public void Add(IIndexable indexable, IProviderUpdateContext context, ProviderIndexConfiguration indexConfiguration)
        {
            Assert.ArgumentNotNull((object)indexable, "indexable");
            Assert.ArgumentNotNull((object)context, "context");
            var data = this.BuildDataToIndex(context, indexable);
            if (data == null)
                return;
            if (data.IsEmpty)
                CrawlingLog.Log.Warn(string.Format("AzureIndexOperations.Add(): IndexVersion produced a NULL doc for version {0}. Skipping.", indexable.UniqueId));
            var document = data.BuildDocument();

            ((IAzureProviderIndex)context.Index).AzureSchema.ReconcileAzureIndexSchema(document);

            AzureIndexOperations.LogIndexOperation((Func<string>)(() => string.Format("Adding indexable UniqueId:{0}, Culture:{1}, DataSource:{2}, Index:{3}", indexable.UniqueId, indexable.Culture, indexable.DataSource, context.Index.Name)), data, document);
            context.AddDocument(document, data.Culture != null ? new CultureExecutionContext(data.Culture) : null);
        }

        private IndexData BuildDataToIndex(IProviderUpdateContext context, IIndexable version)
        {
            ICorePipeline instance = context.Index.Locator.GetInstance<ICorePipeline>();
            version = CleanUpPipeline.Run(instance, new CleanUpArgs(version, context));
            if (InboundIndexFilterPipeline.Run(instance, new InboundIndexFilterArgs(version)))
            {
                this.index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", new object[2]
                {
                  this.index.Name,
                  version.UniqueId
                });
                return null;
            }
            var indexData = this.GetIndexData(version, context);

            if (!indexData.IsEmpty)
                return indexData;
            CrawlingLog.Log.Warn(string.Format("AzureIndexOperations : IndexVersion produced a NULL doc for version {0}. Skipping.", (object)version.UniqueId), (Exception)null);
            return null;
        }

        internal IndexData GetIndexData(IIndexable indexable, IProviderUpdateContext context)
        {
            Assert.ArgumentNotNull((object)indexable, "indexable");
            Assert.ArgumentNotNull((object)context, "context");
            Assert.Required((object)(this.index.Configuration.DocumentOptions as AzureDocumentBuilderOptions), "IDocumentBuilderOptions of wrong type for this crawler");
            AzureDocumentBuilder documentBuilder = (AzureDocumentBuilder)ReflectionUtil.CreateObject(context.Index.Configuration.DocumentBuilderType, new object[2]
            {
                indexable,
                context
            });
            if (documentBuilder == null)
            {
                CrawlingLog.Log.Error("Unable to create document builder (" + context.Index.Configuration.DocumentBuilderType + "). Please check your configuration. We will fallback to the default for now.", (Exception)null);
                documentBuilder = new AzureDocumentBuilder(indexable, context);
            }
            documentBuilder.AddSpecialFields();
            documentBuilder.AddItemFields();
            documentBuilder.AddComputedIndexFields();
            documentBuilder.AddBoost();
            var indexData = new IndexData(this.index, indexable, documentBuilder);
            index.AzureSchema.AddAzureIndexFields(indexData.Fields.Where(f => f.Name != indexData.UpdateTerm.Name).Select(f => f.Field).ToList());
            index.AzureSchema.BuildAzureIndexSchema(indexData.UpdateTerm, indexData.FullUpdateTerm);
            return indexData;
        }

        private static void LogIndexOperation(Func<string> logOperation, IndexData data, Document document)
        {
            VerboseLogging.CrawlingLogDebug((Func<string>)(() =>
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(logOperation());
                stringBuilder.AppendLine(string.Format(" - UpdateTerm Field:{0}, Text:{1}", data.UpdateTerm.Name, data.UpdateTerm.Value));
                if (VerboseLogging.Enabled)
                {
                    foreach (var key in document.Keys)
                    {
                        stringBuilder.AppendLine(string.Format(" - {0}: {1}", key, document[key]));
                    }
                }
                return stringBuilder.ToString();
            }));
        }
    }
}
