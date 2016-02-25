using Sitecore.ContentSearch.Linq.Methods;
using Sitecore.ContentSearch.Linq.Nodes;
using Jarstan.ContentSearch.Linq.Nodes;

namespace Jarstan.ContentSearch.Linq.Methods
{
    public abstract class CustomMethod : QueryMethod
    {
        public override QueryMethodType MethodType
        {
            get
            {
                return QueryMethodType.All;
            }
        }

        public abstract CustomQueryMethodTypes CustomMethodType { get; }

    }
}
