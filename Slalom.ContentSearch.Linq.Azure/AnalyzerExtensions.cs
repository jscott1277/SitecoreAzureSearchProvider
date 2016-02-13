using Lucene.Net.Analysis;
using Sitecore.ContentSearch.Linq.Common;
using System.Collections.Generic;

namespace Slalom.ContentSearch.Linq.Azure
{
    public static class AnalyzerExtensions
    {
        public static Analyzer GetAnalyzerByExecutionContext(this Analyzer analyzer, params IExecutionContext[] executionContexts)
        {
            return AnalyzerExtensions.GetAnalyzerByExecutionContext(analyzer, (IEnumerable<IExecutionContext>)executionContexts);
        }

        public static Analyzer GetAnalyzerByExecutionContext(this Analyzer analyzer, IExecutionContext executionContext)
        {
            return AnalyzerExtensions.GetAnalyzerByExecutionContext(analyzer, (IEnumerable<IExecutionContext>)new IExecutionContext[1]
            {
        executionContext
            });
        }

        public static Analyzer GetAnalyzerByExecutionContext(this Analyzer analyzer, IEnumerable<IExecutionContext> executionContexts)
        {
            ICompositeAnalyzer compositeAnalyzer = analyzer as ICompositeAnalyzer;
            if (compositeAnalyzer == null)
                return analyzer;
            return compositeAnalyzer.GetAnalyzerByExecutionContext(executionContexts);
        }
    }
}
