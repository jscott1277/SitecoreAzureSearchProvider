using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Boosting;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureDocumentBuilder : Jarstan.ContentSearch.AbstractDocumentBuilder<Document>
    {
        private ConcurrentQueue<AzureField> fields = new ConcurrentQueue<AzureField>();
        private readonly AzureSearchFieldConfiguration defaultTextField = new AzureSearchFieldConfiguration("NO", "TOKENIZED", "NO", 1f);
        private readonly AzureSearchFieldConfiguration defaultStoreField = new AzureSearchFieldConfiguration("NO", "TOKENIZED", "YES", 1f);
        private readonly IProviderUpdateContext Context;

        public ConcurrentQueue<AzureField> CollectedFields
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
            var fieldConfiguration = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName);
            var fieldName1 = fieldName;
            fieldName = this.Index.FieldNameTranslator.GetIndexFieldName(fieldName);
            var fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName) as AzureSearchFieldConfiguration;
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
                    var stringBuilder = new StringBuilder();
                    stringBuilder.AppendFormat("Field: {0} (Adding field with no field configuration)" + Environment.NewLine, (object)fieldName);
                    stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, fieldValue != null ? (object)fieldValue.GetType().ToString() : (object)"NULL");
                    stringBuilder.AppendFormat(" - value: {0}" + Environment.NewLine, fieldValue);
                    VerboseLogging.CrawlingLogDebug(new Func<string>(((object)stringBuilder).ToString));
                }

                var enumerable = fieldValue as IEnumerable;
                if (enumerable != null && !(fieldValue is string))
                {
                    foreach (object obj in enumerable)
                    {
                        //var valueToIndex = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(obj, fieldName1);
                        //if (fieldConfiguration != null)
                        //    fieldName1 = fieldConfiguration.FormatForWriting(fieldName1);
                        if (fieldName1 != null)
                        {
                            if (QueueContainsName(fieldName))
                            {
                                var field = this.fields.FirstOrDefault(f => f.Name == fieldName);
                                
                            }
                            else
                            {
                                this.fields.Enqueue(new AzureField(fieldName, fieldName1, AzureFieldBuilder.BuildField(fieldName, fieldName1, this.Index as IAzureProviderIndex)));
                            }
                        }
                    }
                }
                else
                {
                    //var valueToIndex = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(fieldValue, fieldName1);
                    //if (fieldConfiguration != null)
                    //    valueToIndex = fieldConfiguration.FormatForWriting(valueToIndex);
                    if (fieldName1 != null)
                        this.fields.Enqueue(new AzureField(fieldName, fieldValue, AzureFieldBuilder.BuildField(fieldName, fieldValue, this.Index as IAzureProviderIndex)));
                }
            }
        }

        private bool QueueContainsName(string fieldName)
        {
            return this.fields.Any(f => f.Name == fieldName);
        }

        public static DataType GetDataType(object val)
        {
            DataType dataType = DataType.String;
            //Chk for Bool
            try
            {
                bool boolVal;
                if (Boolean.TryParse(val.ToString(), out boolVal))
                {
                    dataType = DataType.Boolean;
                }
            }
            catch { }

            //Chk for Numbers
            try
            {
                Int32 num32;
            if (dataType == DataType.String && Int32.TryParse(val.ToString(), out num32))
            {
                dataType = DataType.Int32;
            }
            }
            catch { }

            try
            {
                Int64 num64;
                if (dataType == DataType.String && Int64.TryParse(val.ToString(), out num64))
                {
                    dataType = DataType.Int32;
                }
            }
            catch { }

            //Chk for DataTime
            try
            {
                DateTime date;
                if (dataType == DataType.String && DateTime.TryParse(val.ToString(), out date))
                {
                    dataType = DataType.DateTimeOffset;
                }
            }
            catch { }

            return dataType;
        }

        public override void AddField(IIndexableDataField field)
        {
            var fieldConfiguration1 = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(field);
            var fieldValue = this.Index.Configuration.FieldReaders.GetFieldValue(field);
            var name = field.Name;
            AzureSearchFieldConfiguration fieldSettings = this.Index.Configuration.FieldMap.GetFieldConfiguration(field) as AzureSearchFieldConfiguration;
            if (fieldSettings == null)
            {
                VerboseLogging.CrawlingLogDebug((Func<string>)(() => string.Format("Cannot resolve field settings for field id:{0}, name:{1}, typeKey:{2} - The field will not be added to the index.", field.Id, (object)field.Name, (object)field.TypeKey)));
            }
            else
            {
                var obj = fieldConfiguration1.FormatForWriting(fieldValue);
                var boost = BoostingManager.ResolveFieldBoosting(field);
                if (IndexOperationsHelper.IsTextField(field))
                {
                    var fieldConfiguration2 = this.Index.Configuration.FieldMap.GetFieldConfiguration("s_content") as AzureSearchFieldConfiguration;
                    this.AddField("s_content", obj, fieldConfiguration2 ?? this.defaultTextField, 0.0f);
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
                    var field = AzureFieldBuilder.CreateField(name, obj, fieldSettings, this.Index.Configuration.IndexFieldStorageValueFormatter);
                    if (field != null)
                    {
                        field.Boost = boost;
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

                field.Boost = boost;
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
