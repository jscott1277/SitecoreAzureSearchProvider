using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Util;
using Jarstan.ContentSearch.Linq.Azure.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jarstan.ContentSearch.Linq.Lucene.Queries
{
    [Serializable]
    public class SpanWildcardQuery : SpanQuery, IEquatable<WildcardQuery>
    {
        private readonly List<Term> terms;

        [Obsolete("Property is no longer in use. Please use Terms property instead.")]
        public Term Term
        {
            get
            {
                return Enumerable.FirstOrDefault<Term>((IEnumerable<Term>)this.terms);
            }
        }

        public override string Field
        {
            get
            {
                return Enumerable.First<Term>((IEnumerable<Term>)this.terms).Field;
            }
        }

        public SpanWildcardQuery(Term term)
        {
            if (term == null)
                throw new ArgumentNullException("term");
            this.terms = new List<Term>()
      {
        term
      };
        }

        public SpanWildcardQuery(IEnumerable<Term> terms)
        {
            if (terms == null)
                throw new ArgumentNullException("terms");
            List<Term> list = new List<Term>(terms);
            if (Enumerable.Count<IGrouping<string, Term>>(Enumerable.GroupBy<Term, string>((IEnumerable<Term>)list, (Func<Term, string>)(t => t.Field))) > 1)
                throw new ArgumentNullException("terms", "Only terms of single field are allowed.");
            this.terms = new List<Term>((IEnumerable<Term>)list);
        }

        public override string ToString(string field)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("SpanWildcardQuery(");
            string str = string.Empty;
            foreach (Term term in (IEnumerable<Term>)this.GetTerms())
                str = str + (object)term + " ";
            stringBuilder.Append(str.TrimEnd());
            stringBuilder.Append(')');
            stringBuilder.Append(ToStringUtils.Boost(this.Boost));
            return stringBuilder.ToString();
        }

        public override Query Rewrite(IndexReader reader)
        {
            List<SpanQuery> list = new List<SpanQuery>();
            foreach (Term term in (IEnumerable<Term>)this.GetTerms())
            {
                WildcardQuery wildcardQuery1 = new WildcardQuery(term);
                wildcardQuery1.RewriteMethod = MultiTermQuery.SCORING_BOOLEAN_QUERY_REWRITE;
                WildcardQuery wildcardQuery2 = wildcardQuery1;
                Query query1;
                try
                {
                    query1 = wildcardQuery2.Rewrite(reader);
                }
                catch (BooleanQuery.TooManyClauses ex)
                {
                    throw new TooManyClausesException();
                }
                BooleanQuery booleanQuery = query1 as BooleanQuery;
                if (booleanQuery == null)
                {
                    try
                    {
                        query1 = query1.Rewrite(reader);
                    }
                    catch (BooleanQuery.TooManyClauses ex)
                    {
                        throw new TooManyClausesException();
                    }
                    booleanQuery = query1 as BooleanQuery;
                }
                if (booleanQuery == null)
                    throw new InvalidOperationException("Unexpected rewritten query type:" + (object)query1.GetType());
                BooleanClause[] clauses = booleanQuery.GetClauses();
                Func<TermQuery, SpanTermQuery> createSpanTermQuery = (Func<TermQuery, SpanTermQuery>)(query =>
                {
                    return new SpanTermQuery(query.Term)
                    {
                        Boost = query.Boost
                    };
                });
                list.AddRange((IEnumerable<SpanQuery>)Enumerable.Select<BooleanClause, SpanTermQuery>((IEnumerable<BooleanClause>)clauses, (Func<BooleanClause, SpanTermQuery>)(t => createSpanTermQuery((TermQuery)t.Query))));
            }
            SpanOrQuery spanOrQuery = new SpanOrQuery(list.ToArray());
            spanOrQuery.Boost = this.Boost;
            return (Query)spanOrQuery;
        }

        public override Spans GetSpans(IndexReader reader)
        {
            throw new InvalidOperationException("Query should have been rewritten");
        }

        public ICollection<Term> GetTerms()
        {
            return (ICollection<Term>)this.terms;
        }

        public bool Equals(WildcardQuery other)
        {
            if (other == null)
                return false;
            if (!object.ReferenceEquals((object)this, (object)other))
                return Enumerable.First<Term>((IEnumerable<Term>)this.terms).Equals((object)other.Term);
            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is WildcardQuery))
                return false;
            return this.Equals((WildcardQuery)obj);
        }

        public override int GetHashCode()
        {
            return 29 * this.terms.GetHashCode();
        }
    }
}
