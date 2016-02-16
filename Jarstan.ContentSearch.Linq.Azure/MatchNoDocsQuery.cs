using Lucene.Net.Index;
using Lucene.Net.Search;
using System;

namespace Jarstan.ContentSearch.Linq.Azure
{
    [Serializable]
    public class MatchNoDocsQuery : Query
    {
        private string normsField;

        public override string ToString(string field)
        {
            return string.Format("MatchNoDocsQuery[{0}]", (object)field);
        }

        public override Weight CreateWeight(Searcher searcher)
        {
            return new MatchNoDocsQuery.MatchNoDocsWeight(this, searcher);
        }

        [Serializable]
        private class MatchNoDocsWeight : Weight
        {
            private MatchNoDocsQuery enclosingInstance;
            private Similarity similarity;
            private float queryWeight;
            private float queryNorm;

            public MatchNoDocsQuery Enclosing_Instance
            {
                get
                {
                    return this.enclosingInstance;
                }
            }

            public override Query Query
            {
                get
                {
                    return (Query)this.Enclosing_Instance;
                }
            }

            public override float Value
            {
                get
                {
                    return this.queryWeight;
                }
            }

            public MatchNoDocsWeight(MatchNoDocsQuery enclosingInstance, Searcher searcher)
            {
                this.InitBlock(enclosingInstance);
                this.similarity = searcher.Similarity;
            }

            private void InitBlock(MatchNoDocsQuery enclosingInstance)
            {
                this.enclosingInstance = enclosingInstance;
            }

            public override string ToString()
            {
                return "weight(" + (object)this.Enclosing_Instance + ")";
            }

            public override float GetSumOfSquaredWeights()
            {
                this.queryWeight = this.Enclosing_Instance.Boost;
                return this.queryWeight * this.queryWeight;
            }

            public override void Normalize(float queryNorm)
            {
                this.queryNorm = queryNorm;
                this.queryWeight *= this.queryNorm;
            }

            public override Scorer Scorer(IndexReader reader, bool scoreDocsInOrder, bool topScorer)
            {
                return (Scorer)new MatchNoDocsQuery.MatchNoDocsScorer(this.enclosingInstance, reader, this.similarity, (Weight)this, this.Enclosing_Instance.normsField != null ? reader.Norms(this.Enclosing_Instance.normsField) : (byte[])null);
            }

            public override Explanation Explain(IndexReader reader, int doc)
            {
                Explanation explanation = (Explanation)new ComplexExplanation(false, this.Value, "MatchNoDocsQuery, product of:");
                if ((double)this.Enclosing_Instance.Boost != 1.0)
                    explanation.AddDetail(new Explanation(this.Enclosing_Instance.Boost, "boost"));
                explanation.AddDetail(new Explanation(this.queryNorm, "queryNorm"));
                explanation.Value = this.Value;
                return explanation;
            }
        }

        private class MatchNoDocsScorer : Scorer
        {
            private int doc = -1;
            private MatchNoDocsQuery enclosingInstance;
            internal TermDocs termDocs;
            internal float score;
            internal byte[] norms;

            public MatchNoDocsQuery Enclosing_Instance
            {
                get
                {
                    return this.enclosingInstance;
                }
            }

            internal MatchNoDocsScorer(MatchNoDocsQuery enclosingInstance, IndexReader reader, Similarity similarity, Weight w, byte[] norms)
              : base(similarity)
            {
                this.InitBlock(enclosingInstance);
                this.termDocs = reader.TermDocs((Term)null);
                this.score = w.Value;
                this.norms = norms;
            }

            private void InitBlock(MatchNoDocsQuery enclosingInstance)
            {
                this.enclosingInstance = enclosingInstance;
            }

            public override int DocID()
            {
                return this.doc;
            }

            public override int NextDoc()
            {
                return DocIdSetIterator.NO_MORE_DOCS;
            }

            public override float Score()
            {
                if (this.norms != null)
                    return this.score * Similarity.DecodeNorm(this.norms[this.DocID()]);
                return this.score;
            }

            public override int Advance(int target)
            {
                return DocIdSetIterator.NO_MORE_DOCS;
            }
        }
    }
}
