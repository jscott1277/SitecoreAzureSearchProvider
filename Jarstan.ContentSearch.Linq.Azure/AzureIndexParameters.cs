using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Parsing;
using System;

namespace Jarstan.ContentSearch.Linq.Azure
{
    public class AzureIndexParameters : IIndexParameters
    {
        private readonly Func<string, IAzureSearchFieldConfiguration> getFieldConfiguration;

        public IIndexValueFormatter ValueFormatter { get; protected set; }

        public IFieldQueryTranslatorMap<IFieldQueryTranslator> FieldQueryTranslators { get; protected set; }

        public FieldNameTranslator FieldNameTranslator { get; protected set; }

        public IExecutionContext[] ExecutionContexts { get; protected set; }

        public bool ConvertQueryDatesToUtc { get; protected set; }

        public IFieldMapReaders FieldMap { get; protected set; }

        public IExecutionContext ExecutionContext
        {
            get
            {
                return this.ExecutionContexts[0];
            }
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, IExecutionContext executionContext)
          : this(valueFormatter, fieldQueryTranslators, fieldNameTranslator, new IExecutionContext[1]
          {
        executionContext
          }, (IFieldMapReaders)null)
        {
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, IExecutionContext[] executionContexts)
          : this(valueFormatter, fieldQueryTranslators, fieldNameTranslator, executionContexts, (IFieldMapReaders)null)
        {
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, IExecutionContext[] executionContexts, IFieldMapReaders fieldMap)
          : this(valueFormatter, fieldQueryTranslators, fieldNameTranslator, null, executionContexts, fieldMap)
        {
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, Func<string, IAzureSearchFieldConfiguration> getFieldConfiguration, IExecutionContext[] executionContexts)
          : this(valueFormatter, fieldQueryTranslators, fieldNameTranslator, getFieldConfiguration, executionContexts, null)
        {
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, Func<string, IAzureSearchFieldConfiguration> getFieldConfiguration, IExecutionContext[] executionContexts, IFieldMapReaders fieldMap)
          : this(valueFormatter, fieldQueryTranslators, fieldNameTranslator, getFieldConfiguration, executionContexts, null, false)
        {
        }

        public AzureIndexParameters(IIndexValueFormatter valueFormatter, IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators, FieldNameTranslator fieldNameTranslator, Func<string, IAzureSearchFieldConfiguration> getFieldConfiguration, IExecutionContext[] executionContexts, IFieldMapReaders fieldMap, bool convertQueryDatesToUtc)
        {
            if (valueFormatter == null)
                throw new ArgumentNullException("valueFormatter");
            if (fieldQueryTranslators == null)
                throw new ArgumentNullException("fieldQueryTranslators");
            if (fieldNameTranslator == null)
                throw new ArgumentNullException("fieldNameTranslator");
            this.ValueFormatter = valueFormatter;
            this.FieldQueryTranslators = fieldQueryTranslators;
            this.FieldNameTranslator = fieldNameTranslator;
            this.ExecutionContexts = executionContexts ?? new IExecutionContext[0];
            this.FieldMap = fieldMap;
            this.getFieldConfiguration = getFieldConfiguration;
            this.getFieldConfiguration = getFieldConfiguration;
            this.ConvertQueryDatesToUtc = convertQueryDatesToUtc;
        }

        public IAzureSearchFieldConfiguration GetFieldConfiguration(string fieldName)
        {
            if (this.getFieldConfiguration == null)
                return (IAzureSearchFieldConfiguration)null;
            return this.getFieldConfiguration(fieldName);
        }
    }
}
