using Sitecore.ContentSearch.Linq.Nodes;
using System.Collections.Generic;

namespace Jarstan.ContentSearch.Linq
{
    public class CustomNode : QueryNode
    {
        public override QueryNodeType NodeType
        {
            get
            {
                return QueryNodeType.Custom;
            }
        }

        public virtual CustomQueryNodeTypes CustomNodeType { get; }

        public override IEnumerable<QueryNode> SubNodes { get; }
    }
}
