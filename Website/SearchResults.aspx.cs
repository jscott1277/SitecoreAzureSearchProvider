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
                    predicate = predicate.Or(s => s.Language == "en");
                    predicate = predicate.Or(s => s.Language == "da");
                    
                    var predicate1 = PredicateBuilder.True<AzureSearchResultItem>();
                    //predicate1 = predicate1.And(f => f.TemplateName == "Image");
                    //predicate1 = predicate1.And(f => f.Name.EndsWith("landscape"));
                    predicate1 = predicate1.And(f => f.Name != "Windows Phone Landscape");
                    predicate = predicate.And(predicate1);

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