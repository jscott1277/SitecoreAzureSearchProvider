using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Slalom.ContentSearch.SearchTypes;
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
                using (var context = ContentSearchManager.GetIndex("azure-sitecore-master-index").CreateSearchContext())
                {
                    var predicate = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate = predicate.And(s => s.Language == "en");
                    predicate = predicate.Or(s => s.Language == "da");
                    //predicate = predicate.And(f => f.Name.EndsWith("landscape"));
                    //predicate = predicate.And(f => f.Name != "Windows Phone Landscape");

                    var predicate2 = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate2 = predicate2.And(f => f.TemplateName == "Image");
                    predicate2 = predicate2.Or(f => f.TemplateName == "Png");
                    predicate = predicate.And(predicate2);

                    var predicate3 = PredicateBuilder.True<AzureSearchResultItem>();
                    predicate3 = predicate3.And(f => f.Name != "Windows Phone Landscape");
                    predicate = predicate.And(predicate3);

                    var queryable = context.GetQueryable<AzureSearchResultItem>();
                    queryable = queryable.Where(predicate);
                    //Only first orderby is being honored, appears to be an Azure Search Bug....
                    queryable = queryable.OrderBy(o => o.Name).ThenByDescending(o => o.TemplateName).Take(10);

                    var results = queryable.GetResults();
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