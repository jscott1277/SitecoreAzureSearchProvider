// Decompiled with JetBrains decompiler
using Lucene.Net.Analysis;
using Sitecore.ContentSearch.Linq.Common;
using System.Collections.Generic;

namespace Slalom.ContentSearch.Linq.Azure
{
    public interface ICompositeAnalyzer
    {
        Analyzer GetAnalyzerByExecutionContext(IEnumerable<IExecutionContext> executionContexts);
    }
}
