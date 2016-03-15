using Sitecore.ContentSearch;
using Sitecore.Diagnostics;
using Jarstan.ContentSearch.AzureProvider;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Sitecore.ContentSearch.Diagnostics;
using Microsoft.Azure.Search;
using System.Threading;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureIndexSchema : ISearchIndexSchema, IAzureSearchIndexSchema
    {
        private readonly IAzureProviderIndex index;

        public ICollection<string> AllFieldNames
        {
            get
            {
                return null;
            }
        }

        public ConcurrentQueue<AzureField> AzureIndexFields { get; set; }
        public bool AzureSchemaBuilt { get; set; }

        private Index AzureIndex { get; set; }

        public AzureIndexSchema(IAzureProviderIndex index)
        {
            Assert.ArgumentNotNull(index, "index");
            this.index = index;
            AzureIndexFields = new ConcurrentQueue<AzureField>();
        }

        public void AddAzureIndexFields(List<AzureField> indexFields)
        {
            foreach (var field in indexFields)
            {
                AddAzureIndexField(field);
            }
        }

        public void AddAzureIndexField(AzureField indexField)
        {
            if (!string.IsNullOrEmpty(indexField.Name) && !this.AzureIndexFields.Any(f => f.Name == indexField.Name))
            {
                this.AzureIndexFields.Enqueue(indexField);
            }
        }

        public void BuildAzureIndexSchema(AzureField keyField, AzureField idField)
        {
            if (!this.AzureSchemaBuilt)
            {
                try
                {
                    //this.AzureIndexFields = this.AzureIndexFields.Where(f => f.Name != keyField.Name).ToList();
                    AddAzureIndexField(keyField);
                    AddAzureIndexField(idField);

                    var indexName = index.Name;
                    var fields = AzureIndexFields
                        .GroupBy(f => f.Name)
                        .Select(f => f.First().Field).ToList();

                    var definition = new Index()
                    {
                        Name = indexName,
                        Fields = fields
                    };

                    var boostFields = AzureIndexFields.Where(f => f.Boost > 0 && f.Field.IsSearchable);
                    if (boostFields.Any())
                    {
                        var scoringProfile = new ScoringProfile();
                        scoringProfile.Name = index.AzureConfiguration.AzureDefaultScoringProfileName;
                        scoringProfile.TextWeights = new TextWeights(new Dictionary<string, double>());

                        foreach (var boostField in boostFields)
                        {
                            if (!scoringProfile.TextWeights.Weights.Any(w => w.Key == boostField.Name))
                            {
                                scoringProfile.TextWeights.Weights.Add(boostField.Name, boostField.Boost);
                            }
                        }

                        if (scoringProfile.TextWeights.Weights.Any())
                        {
                            definition.ScoringProfiles = new List<ScoringProfile>();
                            definition.ScoringProfiles.Add(scoringProfile);
                            definition.DefaultScoringProfile = index.AzureConfiguration.AzureDefaultScoringProfileName;
                        }
                    }

                    AzureIndex = index.AzureServiceClient.Indexes.Create(definition);
                    this.AzureSchemaBuilt = true;
                }
                catch (Exception ex)
                {
                    CrawlingLog.Log.Fatal("Error creating index" + index.Name, ex);
                }
            }
        }

        public bool ReconcileAzureIndexSchema(Document document, int retryCount = 0)
        {
            try
            {
                var fieldCount = AzureIndex.Fields.Count;
                if (document != null)
                {
                    //Look for fields that are different from the standards:
                    foreach (var key in document.Keys)
                    {
                        if (!AzureIndexFields.Any(f => f.Name == key))
                        {
                            object objVal;
                            document.TryGetValue(key, out objVal);
                            var field = AzureFieldBuilder.BuildField(key, objVal, index);
                            var azureField = new AzureField(key, objVal, field);
                            AddAzureIndexField(azureField);
                        }
                    }
                }

                if (AzureIndexFields.Count > fieldCount)
                {
                    var indexName = index.Name;
                    var fields = AzureIndexFields
                        .GroupBy(f => f.Name)
                        .Select(f => f.First().Field).ToList();

                    AzureIndex.Fields = fields;
                    AzureIndex = index.AzureServiceClient.Indexes.CreateOrUpdate(AzureIndex);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (!ReconcileAzureIndexSchema(null))
                {
                    Thread.Sleep(50);
                    if (retryCount < 6)
                    {
                        return ReconcileAzureIndexSchema(null, retryCount++);
                    }
                    else
                    {
                        CrawlingLog.Log.Warn("Error updating index" + index.Name);
                    }
                }
                
                return false;
            }
        }
    }
}
