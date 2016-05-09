using Jarstan.ContentSearch.Linq;
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
    public partial class HighlightSearch : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            using (var context = ContentSearchManager.GetIndex("azure-sitecore-master-media-index").CreateSearchContext())
            {
                var queryable = context.GetQueryable<AzureSearchResultItem>();
                queryable = queryable.Where(s => s.Content.Contains("android"));
                queryable = queryable.Where(s => s.Language == "en");

                var results = queryable.Take(10)
                    .HighlightOn(s => s.Content)
                    .HighlightOn(s => s.Name)
                    .GetHighlightResults(preTag: "<em>", postTag: "</em>", mergeHighlights: true);

                gvResults.DataSource = results.Hits.Select(d => d.Document);
                gvResults.DataBind();
            }
        }
    }
}