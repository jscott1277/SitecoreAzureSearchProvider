using Sitecore.ContentSearch.Linq.Methods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.ContentSearch.Linq.Nodes;
using Jarstan.ContentSearch.Linq.Nodes;

namespace Jarstan.ContentSearch.Linq.Methods
{
    public class GetHighlightResultsMethod : CustomMethod
    {
        public override CustomQueryMethodTypes CustomMethodType
        {
            get
            {
                return CustomQueryMethodTypes.GetHightlights;
            }
        }
    }
}
