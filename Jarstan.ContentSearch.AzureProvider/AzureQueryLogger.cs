
using Contrib.Regex;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using Lucene.Net.Search.Payloads;
using Lucene.Net.Search.Spans;
using Sitecore.ContentSearch.Linq.Lucene.Queries;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Jarstan.ContentSearch.AzureProvider
{
    public static class AzureQueryLogger
    {
        public static string Trace(this Query query)
        {
            AzureQueryLogger.IndentedTextWriter writer = new AzureQueryLogger.IndentedTextWriter((TextWriter)new StringWriter());
            writer.WriteLine("-----------------------");
            AzureQueryLogger.Visit(query, writer);
            writer.WriteLine("-----------------------");
            return writer.ToString();
        }

        private static void Visit(Query query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Query Type: {0}", (object)query.GetType().FullName);
            ++writer.Indent;
            writer.WriteLine("Boost: {0}", (object)query.Boost);
            if (query is BooleanQuery)
                AzureQueryLogger.VisitQuery((BooleanQuery)query, writer);
            if (query is TermQuery)
                AzureQueryLogger.VisitQuery((TermQuery)query, writer);
            if (query is PhraseQuery)
                AzureQueryLogger.VisitQuery((PhraseQuery)query, writer);
            if (query is MultiTermQuery)
                AzureQueryLogger.VisitQuery((MultiTermQuery)query, writer);
            if (query is MultiPhraseQuery)
                AzureQueryLogger.VisitQuery((MultiPhraseQuery)query, writer);
            if (query is MatchAllDocsQuery)
                AzureQueryLogger.VisitQuery((MatchAllDocsQuery)query, writer);
            if (query is FieldScoreQuery)
                AzureQueryLogger.VisitQuery((FieldScoreQuery)query, writer);
            if (query is ValueSourceQuery)
                AzureQueryLogger.VisitQuery((ValueSourceQuery)query, writer);
            if (query is CustomScoreQuery)
                AzureQueryLogger.VisitQuery((CustomScoreQuery)query, writer);
            if (query is FilteredQuery)
                AzureQueryLogger.VisitQuery((FilteredQuery)query, writer);
            if (query is DisjunctionMaxQuery)
                AzureQueryLogger.VisitQuery((DisjunctionMaxQuery)query, writer);
            if (query is ConstantScoreQuery)
                AzureQueryLogger.VisitQuery((ConstantScoreQuery)query, writer);
            if (query is SpanQuery)
                AzureQueryLogger.VisitQuery((SpanQuery)query, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Field: {0}", (object)query.Field);
            if (query is FieldMaskingSpanQuery)
                AzureQueryLogger.VisitQuery((FieldMaskingSpanQuery)query, writer);
            if (query is SpanFirstQuery)
                AzureQueryLogger.VisitQuery((SpanFirstQuery)query, writer);
            if (query is SpanNearQuery)
                AzureQueryLogger.VisitQuery((SpanNearQuery)query, writer);
            if (query is SpanNotQuery)
                AzureQueryLogger.VisitQuery((SpanNotQuery)query, writer);
            if (query is SpanOrQuery)
                AzureQueryLogger.VisitQuery((SpanOrQuery)query, writer);
            if (query is SpanRegexQuery)
                AzureQueryLogger.VisitQuery((SpanRegexQuery)query, writer);
            if (query is SpanTermQuery)
                AzureQueryLogger.VisitQuery((SpanTermQuery)query, writer);
            if (query is PayloadNearQuery)
                AzureQueryLogger.VisitQuery((PayloadNearQuery)query, writer);
            if (query is PayloadTermQuery)
                AzureQueryLogger.VisitQuery((PayloadTermQuery)query, writer);
            if (query is SpanWildcardQuery)
                AzureQueryLogger.VisitQuery((SpanWildcardQuery)query, writer);
            if (query is SpanLastQuery)
                AzureQueryLogger.VisitQuery((SpanLastQuery)query, writer);
            if (!(query is SpanFuzzyQuery))
                return;
            AzureQueryLogger.VisitQuery((SpanFuzzyQuery)query, writer);
        }

        private static void VisitQuery(SpanFuzzyQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("MinSimilarity: {0}", (object)query.MinimumSimilarity);
            writer.WriteLine("PrefixLength: {0}", (object)query.PrefixLength);
            AzureQueryLogger.VisitTerm(query.Term, "Fuzzy Term", writer);
        }

        private static void VisitQuery(SpanLastQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Match:");
            ++writer.Indent;
            AzureQueryLogger.VisitQuery(query.Match, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanWildcardQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(FieldMaskingSpanQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("MaskedQuery:");
            ++writer.Indent;
            AzureQueryLogger.VisitQuery(query.MaskedQuery, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanFirstQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("End: {0}", (object)query.End);
            writer.WriteLine("Match:");
            ++writer.Indent;
            AzureQueryLogger.VisitQuery(query.Match, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanNearQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("IsInOrder: {0}", (query.IsInOrder ? 1 : 0));
            writer.WriteLine("Slop: {0}", (object)query.Slop);
            AzureQueryLogger.VisitClauses(writer, query.GetClauses());
        }

        private static void VisitQuery(SpanNotQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Include:");
            ++writer.Indent;
            AzureQueryLogger.VisitQuery(query.Include, writer);
            --writer.Indent;
            writer.WriteLine("Exclude:");
            ++writer.Indent;
            AzureQueryLogger.VisitQuery(query.Exclude, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanOrQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            SpanQuery[] clauses = query.GetClauses();
            AzureQueryLogger.VisitClauses(writer, clauses);
        }

        private static void VisitClauses(AzureQueryLogger.IndentedTextWriter writer, SpanQuery[] clauses)
        {
            writer.WriteLine("Clauses:");
            ++writer.Indent;
            foreach (Query query in clauses)
                AzureQueryLogger.Visit(query, writer);
            --writer.Indent;
        }

        private static void VisitQuery(SpanRegexQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(SpanTermQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(PayloadNearQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("IsInOrder: {0}", (query.IsInOrder ? 1 : 0));
            writer.WriteLine("Slop: {0}", (object)query.Slop);
        }

        private static void VisitQuery(PayloadTermQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(ConstantScoreQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Filter: {0}", (object)query.Filter);
        }

        private static void VisitQuery(DisjunctionMaxQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            foreach (Query query1 in query)
            {
                writer.WriteLine("Sub query:");
                ++writer.Indent;
                AzureQueryLogger.Visit(query1, writer);
                --writer.Indent;
            }
        }

        private static void VisitQuery(FilteredQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Filter: {0}", (object)query.Filter);
            writer.WriteLine("Filtered query:");
            ++writer.Indent;
            AzureQueryLogger.Visit(query.Query, writer);
            --writer.Indent;
        }

        private static void VisitQuery(CustomScoreQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("IsStrict: {0}", (query.IsStrict() ? 1 : 0));
            writer.WriteLine("Name: {0}", (object)query.Name());
        }

        private static void VisitQuery(ValueSourceQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
        }

        private static void VisitQuery(FieldScoreQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
        }

        private static void VisitQuery(MatchAllDocsQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
        }

        private static void VisitQuery(MultiPhraseQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Slop: {0}", (object)query.Slop);
            foreach (Term[] termArray in (IEnumerable<Term[]>)query.GetTermArrays())
            {
                writer.WriteLine("array");
                ++writer.Indent;
                foreach (Term term in termArray)
                    AzureQueryLogger.VisitTerm(term, writer);
                --writer.Indent;
            }
        }

        private static void VisitQuery(MultiTermQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("TotalNumberOfTerms: {0}", (object)query.TotalNumberOfTerms);
            if (query is FuzzyQuery)
                AzureQueryLogger.MultiTermQuery((FuzzyQuery)query, writer);
            else if (query is PrefixQuery)
                AzureQueryLogger.MultiTermQuery((PrefixQuery)query, writer);
            else if (query is TermRangeQuery)
                AzureQueryLogger.MultiTermQuery((TermRangeQuery)query, writer);
            else if (query is WildcardQuery)
            {
                AzureQueryLogger.MultiTermQuery((WildcardQuery)query, writer);
            }
            else
            {
                if (!(query is RegexQuery))
                    return;
                AzureQueryLogger.MultiTermQuery((RegexQuery)query, writer);
            }
        }

        private static void MultiTermQuery(RegexQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("RegexImplementation: {0}", query.RegexImplementation);
            AzureQueryLogger.VisitTerm(query.Term, "Regex Term", writer);
        }

        private static void MultiTermQuery(FuzzyQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("MinSimilarity: {0}", (object)query.MinSimilarity);
            writer.WriteLine("PrefixLength: {0}", (object)query.PrefixLength);
            AzureQueryLogger.VisitTerm(query.Term, "Fuzzy Term", writer);
        }

        private static void MultiTermQuery(PrefixQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Prefix, "Prefix Term", writer);
        }

        private static void MultiTermQuery(TermRangeQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Field: {0}", (object)query.Field);
            writer.WriteLine("Collator: {0}", (object)query.Collator);
            writer.WriteLine("IncludesLower: {0}", (query.IncludesLower ? 1 : 0));
            writer.WriteLine("IncludesUpper: {0}", (query.IncludesUpper ? 1 : 0));
            writer.WriteLine("LowerTerm: {0}", (object)query.LowerTerm);
            writer.WriteLine("UpperTerm: {0}", (object)query.UpperTerm);
        }

        private static void MultiTermQuery(WildcardQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(PhraseQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("Slop: {0}", (object)query.Slop);
            foreach (Term term in query.GetTerms())
                AzureQueryLogger.VisitTerm(term, writer);
        }

        private static void VisitQuery(TermQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(query.Term, writer);
        }

        private static void VisitQuery(BooleanQuery query, AzureQueryLogger.IndentedTextWriter writer)
        {
            foreach (BooleanClause booleanClause in query.GetClauses())
            {
                writer.WriteLine("Clause:");
                ++writer.Indent;
                writer.WriteLine("IsProhibited: {0}", (booleanClause.IsProhibited ? 1 : 0));
                writer.WriteLine("IsRequired: {0}", (booleanClause.IsRequired ? 1 : 0));
                writer.WriteLine("Occur: {0}", (object)booleanClause.Occur);
                AzureQueryLogger.Visit(booleanClause.Query, writer);
                --writer.Indent;
            }
        }

        private static void VisitTerm(Term term, AzureQueryLogger.IndentedTextWriter writer)
        {
            AzureQueryLogger.VisitTerm(term, "Term", writer);
        }

        private static void VisitTerm(Term term, string termName, AzureQueryLogger.IndentedTextWriter writer)
        {
            writer.WriteLine("{0}: Field: {1}; Text: {2}", (object)termName, (object)term.Field, (object)term.Text);
        }

        private class IndentedTextWriter : TextWriter
        {
            private readonly TextWriter innerWriter;
            private bool doIndent;

            public int Indent { get; set; }

            public override Encoding Encoding
            {
                get
                {
                    return this.innerWriter.Encoding;
                }
            }

            public IndentedTextWriter(TextWriter innerWriter)
            {
                this.innerWriter = innerWriter;
            }

            public override void Write(char ch)
            {
                if (this.doIndent)
                {
                    this.doIndent = false;
                    for (int index = 0; index < this.Indent; ++index)
                        this.innerWriter.Write("  ");
                }
                this.innerWriter.Write(ch);
                if ((int)ch != 10)
                    return;
                this.doIndent = true;
            }

            public override string ToString()
            {
                return this.innerWriter.ToString();
            }
        }
    }
}
