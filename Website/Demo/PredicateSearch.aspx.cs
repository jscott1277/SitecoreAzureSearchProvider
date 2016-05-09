using Jarstan.ContentSearch.SearchTypes;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Website.Demo
{
    public partial class PredicateSearch : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            using (var context = ContentSearchManager.GetIndex("azure-sitecore-web-index").CreateSearchContext())
            {
                var predicate = PredicateBuilder.True<AzureSearchResultItem>();
                predicate = predicate.And(s => s.Content.Contains("Home"));
                predicate = predicate.And(s => s.Language == "en");
                var queryable = context.GetQueryable<AzureSearchResultItem>();
                queryable = queryable.Where(predicate);

                var results = queryable.GetResults();

                gvResults.DataSource = results.Hits.Select(d => d.Document);
                gvResults.DataBind();
            }
        }
    }
}