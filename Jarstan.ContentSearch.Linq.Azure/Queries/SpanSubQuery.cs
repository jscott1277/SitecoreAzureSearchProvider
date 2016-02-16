using Lucene.Net.Search.Spans;
using System;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    public class SpanSubQuery
    {
        public Func<SpanQuery> CreatorMethod { get; set; }

        public int Position { get; set; }

        public bool IsWildcard { get; set; }
    }
}
