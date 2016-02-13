using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slalom.ContentSearch.AzureProvider
{
    interface IAzureProviderUpdateContext
    {
        List<IndexAction> IndexActions { get; set; }
        IAzureProviderIndex AzureIndex { get; }
    }
}
