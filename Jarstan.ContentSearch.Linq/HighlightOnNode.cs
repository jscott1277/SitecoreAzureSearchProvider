using Sitecore.ContentSearch.Linq.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq
{
    public class HighlightOnNode : CustomNode
    {
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
                return CustomQueryNodeTypes.HighlightOn;
            }
        }

        public override IEnumerable<QueryNode> SubNodes
        {
            get
            {
                yield return SourceNode;
                yield break;
            }
        }

        public string Field { get; protected set; }

        public QueryNode SourceNode { get; protected set; }

        public HighlightOnNode(QueryNode sourceNode, string field)
        {
            SourceNode = sourceNode;
            Field = field;
        }

        public override string ToString()
        {
            return base.ToString() + string.Format(" - Field: {0};", Field);
        }
    }
}
