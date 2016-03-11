using Jarstan.ContentSearch.SearchTypes;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Sitecore.ContentSearch.Linq.Extensions;
using Sitecore.ContentSearch.Linq;

namespace Website
{
    public partial class Search : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            using (var context = ContentSearchManager.GetIndex("azure-sitecore-web-index").CreateSearchContext())
            {
                var queryable = context.GetQueryable<AzureSearchResultItem>();
                queryable = queryable.Where(s => s.Content.Contains(txtSearchTerm.Text));
                queryable = queryable.Where(l => l.Language == "en");
                queryable = queryable.OrderBy(o => o.Name);
                var results = queryable.GetResults();
                lblAzureCount.Text = results.TotalSearchResults.ToString();

                gvAzureResults.DataSource = results.Hits.Select(d => d.Document);
                gvAzureResults.DataBind();
            }

            using (var context = ContentSearchManager.GetIndex("sitecore_test_web_index").CreateSearchContext())
            {
                var queryable = context.GetQueryable<SearchResultItem>();
                queryable = queryable.Where(s => s.Content.Contains(txtSearchTerm.Text));
                queryable = queryable.Where(l => l.Language == "en");
                queryable = queryable.OrderBy(o => o.Name);
                var results = queryable.GetResults();
                lblLuceneCount.Text = results.TotalSearchResults.ToString();

                gvLuceneResults.DataSource = results.Hits.Select(d => d.Document);
                gvLuceneResults.DataBind();
            }
        }
    }
}