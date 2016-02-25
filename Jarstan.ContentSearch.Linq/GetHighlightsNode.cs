using Sitecore.ContentSearch.Linq.Nodes;
using System.Collections.Generic;

namespace Jarstan.ContentSearch.Linq
{
    public class GetHighlightResultsNode : CustomNode
    {
        public QueryNode SourceNode
        {
            get;
            protected set;
        }

        public override QueryNodeType NodeType
        {
            get
            {
                return QueryNodeType.Custom;
            }
        }

        public override CustomQueryNodeTypes CustomNodeType
        {
            get
            {
                return CustomQueryNodeTypes.GetHighlightResults;
            }
        }

        public override IEnumerable<QueryNode> SubNodes
        {
            get
            {
                yield return this.SourceNode;
                yield break;
            }
        }

        public GetHighlightResultsNode(QueryNode sourceNode)
        {
            this.SourceNode = sourceNode;
        }
    }
}
