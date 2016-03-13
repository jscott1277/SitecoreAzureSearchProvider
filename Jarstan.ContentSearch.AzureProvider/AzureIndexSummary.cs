using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using Sitecore;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Maintenance;
using Sitecore.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureIndexSummary : ISearchIndexSummary
    {
        private readonly IAzureProviderIndex index;
        private IIndexableInfo lastIndexedEntry;

        public long NumberOfDocuments
        {
            get
            {
                try
                {
                    return index.AzureIndexClient.Documents.Count();
                }
                catch
                {
                    //Do Nothing
                }

                return 0;
            }
        }

        public bool IsOptimized
        {
            get
            {
                return true;
            }
        }

        public bool HasDeletions
        {
            get
            {
                return false;
            }
        }

        public bool IsHealthy
        {
            get
            {
                return true;
            }
        }

        public DateTime LastUpdated
        {
            get
            {
                if (index.PropertyStore == null)
                    return DateTime.MinValue;
                string isoDate = index.PropertyStore.Get(IndexProperties.LastUpdatedKey);
                if (isoDate.Length <= 0)
                    return DateTime.MinValue;
                return DateUtil.IsoDateToDateTime(isoDate, DateTime.MinValue, true);
            }
            set
            {
                if (index.PropertyStore == null)
                    return;
                index.PropertyStore.Set(IndexProperties.LastUpdatedKey, DateUtil.ToIsoDate(value, true, true));
            }
        }

        public int NumberOfFields
        {
            get
            {
                try
                {
                    return index.AzureServiceClient.Indexes.Get(index.Name).Fields.Count;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public long NumberOfTerms
        {
            get
            {
                return -1;
            }
        }

        public bool IsClean
        {
            get
            {
                return true; 
            }
        }

        public string Directory
        {
            get
            {
                return string.Empty;
            }
        }

        public bool IsMissingSegment
        {
            get
            {
                return false;
            }
        }

        public int NumberOfBadSegments
        {
            get
            {
                return 0;
            }
        }

        public bool OutOfDateIndex
        {
            get
            {
                return false;
            }
        }

        public IDictionary<string, string> UserData
        {
            get
            {
                return null;
            }
        }

        [Obsolete("LastIndexedEntry property is no longer in use and will be removed in later release.")]
        public IIndexableInfo LastIndexedEntry
        {
            get
            {
                lastIndexedEntry = (JsonConvert.DeserializeObject<IndexableInfo>(index.PropertyStore.Get(IndexProperties.LastIndexedEntry)) ?? new IndexableInfo());
                return lastIndexedEntry;
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                lastIndexedEntry = value;
                index.PropertyStore.Set(IndexProperties.LastIndexedEntry, JsonConvert.SerializeObject(lastIndexedEntry));
            }
        }

        public long? LastUpdatedTimestamp
        {
            get
            {
                string s = index.PropertyStore.Get(IndexProperties.LastUpdatedTimestamp);
                if (string.IsNullOrEmpty(s))
                    return new long?();
                return new long?(long.Parse(s, CultureInfo.InvariantCulture));
            }
            set
            {
                string str = !value.HasValue ? string.Empty : value.Value.ToString(CultureInfo.InvariantCulture);
                index.PropertyStore.Set(IndexProperties.LastUpdatedTimestamp, str);
            }
        }

        public AzureIndexSummary(IAzureProviderIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            this.index = index;
        }
    }
}
