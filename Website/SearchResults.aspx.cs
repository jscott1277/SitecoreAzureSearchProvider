﻿using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Jarstan.ContentSearch.SearchTypes;
using Jarstan.ContentSearch.Linq;
using Sitecore.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Sitecore.Data.Items;
using Sitecore.ContentSearch.Linq.Utilities;

namespace Website
{
    public partial class SearchResults : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                using (var context = ContentSearchManager.GetIndex("azure-sitecore-master-media-index").CreateSearchContext())
                {
                    var predicate = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate = predicate.And(s => s.Language == "en");
                    predicate = predicate.Or(s => s.Language == "da");
                    //predicate = predicate.And(f => f.Name.EndsWith("landscape"));
                    //predicate = predicate.And(f => f.Name != "Windows Phone Landscape");

                    var predicate2 = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate2 = predicate2.And(f => f.TemplateName == "Image");
                    predicate2 = predicate2.Or(f => f.TemplateName == "Jpeg");
                    predicate = predicate.And(predicate2);

                    //Meant to enable one at a time for testing purposes
                    var predicate3 = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate3 = predicate3.And(f => f.Name != "Windows Phone Landscape");
                    //predicate3 = predicate3.And(f => f.Name.StartsWith("Page"));
                    //predicate3 = predicate3.And(f => f.Name.EndsWith("Found"));
                    //predicate3 = predicate3.And(f => f.Name.Contains("Not"));
                    predicate = predicate.And(predicate3);

                    var queryable = context.GetQueryable<AzureSearchResultItem>();
                    queryable = queryable.Where(predicate);

                    //queryable = queryable.Filter(o => o.TemplateName == "Jpeg");
                    //queryable = queryable.Filter(o => o.TemplateName != "test");
                    //queryable = queryable.Filter(o => o.iVersion.Between(1, 3, Inclusion.None));

                    //TODO:  Only first orderby is being honored, appears to be an Azure Search Bug....
                    queryable = queryable.OrderBy(o => o.Name).ThenByDescending(o => o.TemplateName).Take(10);

                    

                    //GetFacets extension method support
                    var facets0 = queryable.FacetOn(o => o.TemplateName).GetFacets();
                    //var facets1 = queryable.FacetOn(o => o.TemplateName, 4).GetFacets();
                    //var facets2 = queryable.FacetOn(o => o.TemplateName, 4, new List<string>() { "Jpeg", "Image" }).GetFacets();

                    //GetHighlightResults
                    var results = queryable.HighlightOn(h => h.TemplateName).HighlightOn(h => h.Language).GetHighlightResults("<b>", "</b>", true);

                    //Or

                    //GetResults
                    //var results = queryable.GetResults();
                    gvResults.DataSource = results.Hits.Select(r => r.Document);
                    gvResults.DataBind();
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}