using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    interface IAzureProviderUpdateContext
    {
        IAzureProviderIndex AzureIndex { get; }
    }
}
