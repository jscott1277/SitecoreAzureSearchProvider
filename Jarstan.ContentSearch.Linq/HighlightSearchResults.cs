using System;
using System.Collections;
using System.Collections.Generic;

namespace Jarstan.ContentSearch.Linq
{
    public sealed class HighlightSearchResults<TSource> : IEnumerable<AzureSearchHit<TSource>>, IEnumerable
    {
        public int TotalSearchResults { get; private set; }

        public IEnumerable<AzureSearchHit<TSource>> Hits { get; private set; }

        public HighlightSearchResults(IEnumerable<AzureSearchHit<TSource>> results, int totalSearchResults)
        {
            if (results == null)
                throw new ArgumentNullException("results");
            this.Hits = results;
            this.TotalSearchResults = totalSearchResults;
        }
        
        IEnumerator<AzureSearchHit<TSource>> IEnumerable<AzureSearchHit<TSource>>.GetEnumerator()
        {
            return this.Hits.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)this.Hits.GetEnumerator();
        }
    }
}
