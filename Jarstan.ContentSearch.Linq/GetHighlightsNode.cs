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

        public string PreTag { get; set; }
        public string PostTag { get; set; }

        public bool MergeHighlights { get; set; }

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

        public GetHighlightResultsNode(QueryNode sourceNode, string preTag, string postTag, bool mergeHighlights)
        {
            this.SourceNode = sourceNode;
            this.PreTag = preTag;
            this.PostTag = postTag;
            this.MergeHighlights = mergeHighlights;
        }
    }
}
