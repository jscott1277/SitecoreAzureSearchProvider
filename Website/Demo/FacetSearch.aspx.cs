using Jarstan.ContentSearch.SearchTypes;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website.Demo
{
    public partial class FacetSearch : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            using (var context = ContentSearchManager.GetIndex("azure-sitecore-web-index").CreateSearchContext())
            {
                var queryable = context.GetQueryable<AzureSearchResultItem>();
                queryable = queryable.Where(s => s.Content == "Home");
                queryable = queryable.Where(s => s.Language == "en");

                var results = queryable
                    .FacetOn(s => s.TemplateName)
                    .GetFacets();

                gvFacetResults.DataSource = results.Categories.FirstOrDefault().Values;
                gvFacetResults.DataBind();

                var queryable2 = context.GetQueryable<AzureSearchResultItem>();
                queryable2 = queryable2.Where(s => s.Content.Contains("Home"));
                queryable2 = queryable2.Where(s => s.Language == "en");
                var results2 = queryable.GetResults();
                gvResults.DataSource = results2.Hits.Select(d => d.Document).Select(r => new { Name = r.Name, TemplateName = r.TemplateName });
                gvResults.DataBind();
            }
        }
    }
}