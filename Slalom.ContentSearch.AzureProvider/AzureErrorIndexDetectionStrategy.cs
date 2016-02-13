using Microsoft.Azure.Search;
using Microsoft.Rest.TransientFaultHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.AzureProvider
{
    public class AzureErrorIndexDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            return ex is IndexBatchException;
        }
    }
}
