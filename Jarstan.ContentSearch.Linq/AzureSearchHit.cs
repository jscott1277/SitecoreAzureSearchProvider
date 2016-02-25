using Sitecore.ContentSearch.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq
{
    public class AzureSearchHit<TSource> : SearchHit<TSource>
    {
        public List<HighlightResult> HighlightResults {get; set; }

        public AzureSearchHit(float score, TSource document)
            :base(score, document)
        {
            HighlightResults = new List<HighlightResult>();
        }

        public AzureSearchHit(float score, TSource document, List<HighlightResult> highlights)
            : base(score, document)
        {
            HighlightResults = highlights;
        }
    }
}
