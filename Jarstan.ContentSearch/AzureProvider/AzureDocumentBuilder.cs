using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Boosting;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureDocumentBuilder : AbstractDocumentBuilder<Document>
    {
        private ConcurrentQueue<Field> fields = new ConcurrentQueue<Field>();
        private readonly AzureSearchFieldConfiguration defaultTextField = new AzureSearchFieldConfiguration("NO", "TOKENIZED", "NO", 1f);
        private readonly AzureSearchFieldConfiguration defaultStoreField = new AzureSearchFieldConfiguration("NO", "TOKENIZED", "YES", 1f);
        private readonly IProviderUpdateContext Context;

        public ConcurrentQueue<Field> CollectedFields
        {
            get
            {
                return this.fields;
            }
        }

        public AzureDocumentBuilder(IIndexable indexable, IProviderUpdateContext context)
          : base(indexable, context)
        {
            this.Context = context;
        }

        public override void AddField(string fieldName, object fieldValue, bool append = false)
        {
            AbstractSearchFieldConfiguration fieldConfiguration = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName);
            string fieldName1 = fieldName;
            fieldName = this.Index.FieldNameTranslator.GetIndexFieldName(fieldName);
            AzureSearchFieldConfiguration fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName) as AzureSearchFieldConfiguration;
            if (fieldSettings != null)
            {
                if (fieldConfiguration != null)
                    fieldValue = fieldConfiguration.FormatForWriting(fieldValue);
                this.AddField(fieldName, fieldValue, fieldSettings, 0.0f);
            }
            else
            {
                if (VerboseLogging.Enabled)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendFormat("Field: {0} (Adding field with no field configuration)" + Environment.NewLine, (object)fieldName);
                    stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, fieldValue != null ? (object)fieldValue.GetType().ToString() : (object)"NULL");
                    stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, fieldValue);
                    VerboseLogging.CrawlingLogDebug(new Func<string>(((object)stringBuilder).ToString));
                }
                IEnumerable enumerable = fieldValue as IEnumerable;
                if (enumerable != null && !(fieldValue is string))
                {
                    foreach (object obj in enumerable)
                    {
                        object valueToIndex = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(obj, fieldName1);
                        if (fieldConfiguration != null)
                            valueToIndex = fieldConfiguration.FormatForWriting(valueToIndex);
                        if (valueToIndex != null)
                            this.fields.Enqueue((Field)new Field(fieldName, valueToIndex.ToString()));
                    }
                }
                else
                {
                    object valueToIndex = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(fieldValue, fieldName1);
                    if (fieldConfiguration != null)
                        valueToIndex = fieldConfiguration.FormatForWriting(valueToIndex);
                    if (valueToIndex == null)
                        return;

                    //TODO:  How to figure out setting field value
                    this.fields.Enqueue((Field)new Field(fieldName, DataType.String /*, valueToIndex.ToString()*/));
                }
            }
        }

        public override void AddField(IIndexableDataField field)
        {
            AbstractSearchFieldConfiguration fieldConfiguration1 = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(field);
            object fieldValue = this.Index.Configuration.FieldReaders.GetFieldValue(field);
            string name = field.Name;
            AzureSearchFieldConfiguration fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(field) as AzureSearchFieldConfiguration;
            if (fieldSettings == null)
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Cannot resolve field settings for field id:{0}, name:{1}, typeKey:{2} - The field will not be added to the index.", field.Id, (object)field.Name, (object)field.TypeKey)));
            }
            else
            {
                object obj = fieldConfiguration1.FormatForWriting(fieldValue);
                float boost = BoostingManager.ResolveFieldBoosting(field);
                if (IndexOperationsHelper.IsTextField(field))
                {
                    AzureSearchFieldConfiguration fieldConfiguration2 = this.Index.Configuration.FieldMap.GetFieldConfiguration("_content") as AzureSearchFieldConfiguration;
                    this.AddField("_content", obj, fieldConfiguration2 ?? this.defaultTextField, 0.0f);
                }
                this.AddField(name, obj, fieldSettings, boost);
            }
        }

        public override void AddBoost()
        {
            float num = BoostingManager.ResolveItemBoosting(this.Indexable);
            if ((double)num <= 0.0)
                return;
            
            //TODO:  Figure out boost for Azure Document
            //this.Document..Boost = num;
        }

        protected void AddField(string name, object value, AzureSearchFieldConfiguration fieldSettings, float boost = 0.0f)
        {
            Assert.IsNotNull((object)fieldSettings, "fieldSettings");
            name = this.Index.FieldNameTranslator.GetIndexFieldName(name);
            boost += fieldSettings.Boost;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object valueToIndex in enumerable)
                {
                    object obj = fieldSettings.FormatForWriting(valueToIndex);
                    Field field = AzureFieldBuilder.CreateField(name, obj, fieldSettings, this.Index.Configuration.IndexFieldStorageValueFormatter);
                    if (field != null)
                    {
                        //TODO: How to figure out boost
                        //field.Boost = boost;
                        this.fields.Enqueue(field);
                    }
                }
            }
            else
            {
                value = fieldSettings.FormatForWriting(value);
                var field = AzureFieldBuilder.CreateField(name, value, fieldSettings, this.Index.Configuration.IndexFieldStorageValueFormatter);
                if (field == null)
                    return;

                //TODO: How to figure out boost
                //field.Boost = boost;
                this.fields.Enqueue(field);
            }
        }

        public override void AddComputedIndexFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddComputedIndexFields Start"));
                if (this.IsParallel)
                {
                    ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
                    Parallel.ForEach<IComputedIndexField>((IEnumerable<IComputedIndexField>)this.Options.ComputedIndexFields, this.ParallelOptions, (Action<IComputedIndexField, ParallelLoopState>)((computedIndexField, parallelLoopState) =>
                    {
                        object fieldValue;
                        try
                        {
                            fieldValue = computedIndexField.ComputeFieldValue(this.Indexable);
                        }
                        catch (Exception ex)
                        {
                            CrawlingLog.Log.Warn(string.Format("Could not compute value for ComputedIndexField: {0} for indexable: {1}", (object)computedIndexField.FieldName, (object)this.Indexable.UniqueId), ex);
                            if (!this.Settings.StopOnCrawlFieldError())
                                return;
                            exceptions.Enqueue(ex);
                            parallelLoopState.Stop();
                            return;
                        }
                        this.AddComputedIndexField(computedIndexField, fieldValue);
                    }));
                    if (exceptions.Count > 0)
                        throw new AggregateException((IEnumerable<Exception>)exceptions);
                }
                else
                {
                    foreach (IComputedIndexField computedIndexField in this.Options.ComputedIndexFields)
                    {
                        object fieldValue;
                        try
                        {
                            fieldValue = computedIndexField.ComputeFieldValue(this.Indexable);
                        }
                        catch (Exception ex)
                        {
                            CrawlingLog.Log.Warn(string.Format("Could not compute value for ComputedIndexField: {0} for indexable: {1}", (object)computedIndexField.FieldName, (object)this.Indexable.UniqueId), ex);
                            if (this.Settings.StopOnCrawlFieldError())
                                throw;
                            else
                                continue;
                        }
                        this.AddComputedIndexField(computedIndexField, fieldValue);
                    }
                }
            }
            finally
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => "AddComputedIndexFields End"));
            }
        }

        private void AddComputedIndexField(IComputedIndexField computedIndexField, object fieldValue)
        {
            AzureSearchFieldConfiguration fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(computedIndexField.FieldName) as AzureSearchFieldConfiguration;
            if (fieldValue is IEnumerable && !(fieldValue is string))
            {
                foreach (object fieldValue1 in fieldValue as IEnumerable)
                {
                    if (fieldSettings != null)
                        this.AddField(computedIndexField.FieldName, fieldValue1, fieldSettings, 0.0f);
                    else
                        this.AddField(computedIndexField.FieldName, fieldValue1, false);
                }
            }
            else if (fieldSettings != null)
                this.AddField(computedIndexField.FieldName, fieldValue, fieldSettings, 0.0f);
            else
                this.AddField(computedIndexField.FieldName, fieldValue, false);
        }
    }
}
