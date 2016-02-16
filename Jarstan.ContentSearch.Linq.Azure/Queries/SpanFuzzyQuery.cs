using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    [Serializable]
    public class SpanFuzzyQuery : SpanQuery, IEquatable<SpanFuzzyQuery>
    {
        private readonly Term term;
        private float minimumSimilarity;

        public Term Term
        {
            get
            {
                return this.term;
            }
        }

        public override string Field
        {
            get
            {
                return this.term.Field;
            }
        }

        public float MinimumSimilarity
        {
            get
            {
                return this.minimumSimilarity;
            }
            set
            {
                this.minimumSimilarity = value;
            }
        }

        public int PrefixLength { get; set; }

        public SpanFuzzyQuery(Term term, float minimumSimilarity)
        {
            this.term = term;
            this.MinimumSimilarity = minimumSimilarity;
            this.PrefixLength = 0;
        }

        public override string ToString(string field)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("SpanFuzzyQuery(");
            stringBuilder.Append((object)this.term);
            stringBuilder.Append(')');
            stringBuilder.Append(ToStringUtils.Boost(this.Boost));
            return stringBuilder.ToString();
        }

        public override Query Rewrite(IndexReader reader)
        {
            FuzzyQuery fuzzyQuery = new FuzzyQuery(this.term, this.minimumSimilarity, this.PrefixLength);
            Query query1 = fuzzyQuery.Rewrite(reader);
            BooleanQuery booleanQuery = query1 as BooleanQuery;
            if (booleanQuery == null)
            {
                query1 = query1.Rewrite(reader);
                booleanQuery = query1 as BooleanQuery;
            }
            Func<TermQuery, SpanTermQuery> func = (Func<TermQuery, SpanTermQuery>)(query =>
            {
                return new SpanTermQuery(query.Term)
                {
                    Boost = query.Boost
                };
            });
            if (booleanQuery != null)
            {
                BooleanClause[] clauses = booleanQuery.GetClauses();
                if (clauses.Length == 1)
                    return (Query)func((TermQuery)clauses[0].Query);
                SpanQuery[] spanQueryArray = new SpanQuery[clauses.Length];
                for (int index = 0; index < clauses.Length; ++index)
                    spanQueryArray[index] = (SpanQuery)func((TermQuery)clauses[index].Query);
                SpanOrQuery spanOrQuery = new SpanOrQuery(spanQueryArray);
                spanOrQuery.Boost = fuzzyQuery.Boost;
                return (Query)spanOrQuery;
            }
            if (query1 is TermQuery)
                return (Query)func((TermQuery)query1);
            throw new InvalidOperationException("Unexpected rewritten query type:" + (object)query1.GetType());
        }

        public override Spans GetSpans(IndexReader reader)
        {
            throw new InvalidOperationException("Query should have been rewritten");
        }

        public ICollection<Term> GetTerms()
        {
            return (ICollection<Term>)new List<Term>()
      {
        this.term
      };
        }

        public bool Equals(SpanFuzzyQuery other)
        {
            if (other == null)
                return false;
            if (!object.ReferenceEquals((object)this, (object)other))
                return this.term.Equals((object)other.Term);
            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FuzzyQuery))
                return false;
            return this.Equals((object)(FuzzyQuery)obj);
        }

        public override int GetHashCode()
        {
            return 29 * this.term.GetHashCode();
        }
    }
}
