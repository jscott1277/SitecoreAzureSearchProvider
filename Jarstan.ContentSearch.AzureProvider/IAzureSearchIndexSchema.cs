using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    public interface IAzureSearchIndexSchema
    {
        ConcurrentQueue<AzureField> AzureIndexFields { get; set; }
        void AddAzureIndexFields(List<AzureField> indexFields);
        void AddAzureIndexField(AzureField indexField);
        bool AzureSchemaBuilt { get; set; }
        void BuildAzureIndexSchema(AzureField keyField, AzureField idField);
        bool ReconcileAzureIndexSchema(Document document, int retryCount = 0);
    }
}
