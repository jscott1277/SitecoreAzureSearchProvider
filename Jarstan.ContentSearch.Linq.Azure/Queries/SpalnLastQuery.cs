using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
//using Lucene.Net.Search.Highlight;
using Lucene.Net.Search.Spans;
using Lucene.Net.Support;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    [Serializable]
    public class SpanLastQuery : SpanQuery, ICloneable
    {
        private SpanQuery match;
        private int end;
        private Analyzer analyzer;

        public virtual SpanQuery Match
        {
            get
            {
                return this.match;
            }
        }

        [Obsolete("Sitecore.ContentSearch.Linq.Lucene.Queries.SpanLastQuery.End property is no longer in use and will be removed in later release.")]
        public virtual int End
        {
            get
            {
                return this.end;
            }
        }

        public override string Field
        {
            get
            {
                return this.match.Field;
            }
        }

        [Obsolete("Sitecore.ContentSearch.Linq.Lucene.Queries.SpanLastQuery.SpanLastQuery(SpanQuery match, int end) constructor is no longer in use and will be removed in later release.")]
        public SpanLastQuery(SpanQuery match, int end)
          : this(match, (Analyzer)new StandardAnalyzer(global::Lucene.Net.Util.Version.LUCENE_30))
        {
        }

        public SpanLastQuery(SpanQuery match, Analyzer analyzer)
        {
            this.match = match;
            this.analyzer = analyzer;
        }

        public override string ToString(string field)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("spanLast(");
            stringBuilder.Append(this.match.ToString(field));
            stringBuilder.Append(")");
            stringBuilder.Append(ToStringUtils.Boost(this.Boost));
            return stringBuilder.ToString();
        }

        public override object Clone()
        {
            SpanLastQuery spanLastQuery = new SpanLastQuery((SpanQuery)this.match.Clone(), this.analyzer);
            spanLastQuery.Boost = this.Boost;
            return (object)spanLastQuery;
        }

        public override void ExtractTerms(ISet<Term> terms)
        {
            this.match.ExtractTerms(terms);
        }

        public override Spans GetSpans(IndexReader reader)
        {
            return (Spans)new SpanLastQuery.AnonymousClassSpans(reader, this);
        }

        public override Query Rewrite(IndexReader reader)
        {
            SpanLastQuery spanLastQuery = (SpanLastQuery)null;
            SpanQuery spanQuery = (SpanQuery)this.match.Rewrite(reader);
            if (spanQuery != this.match)
            {
                spanLastQuery = (SpanLastQuery)this.Clone();
                spanLastQuery.match = spanQuery;
            }
            return (Query)spanLastQuery ?? (Query)this;
        }

        public override bool Equals(object o)
        {
            if (this == o)
                return true;
            if (!(o is SpanLastQuery))
                return false;
            SpanLastQuery spanLastQuery = (SpanLastQuery)o;
            if (this.match.Equals((object)spanLastQuery.match))
                return (double)this.Boost == (double)spanLastQuery.Boost;
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = this.match.GetHashCode();
            return hashCode ^ (hashCode << 8 | Number.URShift(hashCode, 25)) ^ Convert.ToInt32(this.Boost);
        }

        private class AnonymousClassSpans : Spans
        {
            private IndexReader reader;
            private SpanLastQuery enclosingInstance;
            private Spans spans;

            public SpanLastQuery Enclosing_Instance
            {
                get
                {
                    return this.enclosingInstance;
                }
            }

            public AnonymousClassSpans(IndexReader reader, SpanLastQuery enclosingInstance)
            {
                this.InitBlock(reader, enclosingInstance);
            }

            private void InitBlock(IndexReader reader, SpanLastQuery enclosingInstance)
            {
                this.reader = reader;
                this.enclosingInstance = enclosingInstance;
                this.spans = this.Enclosing_Instance.match.GetSpans(reader);
            }

            public override bool Next()
            {
                int num1 = -1;
                int num2 = 0;
                while (this.spans.Next())
                {
                    int num3 = this.spans.Doc();
                    if (num1 != num3)
                    {
                        num1 = num3;
                        num2 = 0;
                        //using (TokenStream tokenStream = TokenSources.GetTokenStream(this.reader, num3, this.Enclosing_Instance.Field, this.Enclosing_Instance.analyzer))
                        //{
                        //    if (tokenStream == null)
                        //        throw new ArgumentException(string.Format("Field {0} does not have term vector offsets", (object)this.Enclosing_Instance.Field));
                        //    IPositionIncrementAttribute incrementAttribute = tokenStream.AddAttribute<IPositionIncrementAttribute>();
                        //    while (tokenStream.IncrementToken())
                        //        num2 += incrementAttribute.PositionIncrement;
                        //    tokenStream.End();
                        //}
                    }
                    if (this.End() == num2)
                        return true;
                }
                return false;
            }

            public override bool SkipTo(int target)
            {
                while (this.Next())
                {
                    if (this.Doc() >= target)
                        return this.Doc() == target;
                }
                return false;
            }

            public override int Doc()
            {
                return this.spans.Doc();
            }

            public override int Start()
            {
                return this.spans.Start();
            }

            public override int End()
            {
                return this.spans.End();
            }

            public override ICollection<byte[]> GetPayload()
            {
                ICollection<byte[]> collection = (ICollection<byte[]>)null;
                if (this.spans.IsPayloadAvailable())
                    collection = this.spans.GetPayload();
                return collection;
            }

            public override bool IsPayloadAvailable()
            {
                return this.spans.IsPayloadAvailable();
            }

            public override string ToString()
            {
                return "spans(" + this.Enclosing_Instance.ToString() + ")";
            }
        }
    }
}
