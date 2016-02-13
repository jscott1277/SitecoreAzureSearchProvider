using Sitecore.ContentSearch.Linq.Parsing;

namespace Slalom.ContentSearch.Linq.Azure
{
    public class AzureQueryOptimizerState : QueryOptimizerState
    {
        public float Boost { get; set; }

        public AzureQueryOptimizerState()
        {
            this.Boost = 1f;
        }
    }
}
