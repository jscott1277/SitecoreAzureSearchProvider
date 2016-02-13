// Decompiled with JetBrains decompiler
// Type: Sitecore.ContentSearch.SearchIndexInitializableUtility
// Assembly: Sitecore.ContentSearch, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 51523957-5FFB-4C54-9E7F-96D75D25D5DF
// Assembly location: C:\inetpub\wwwroot\sc81rev151207\Website\bin\Sitecore.ContentSearch.dll

using Sitecore.ContentSearch;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Slalom.ContentSearch.AzureProvider")]

namespace Slalom.ContentSearch
{
    internal static class SearchIndexInitializableUtility
    {
        public static void Initialize(ISearchIndex index, params object[] instances)
        {
            TypeActionHelper.Call<ISearchIndexInitializable>(i => i.Initialize(index), instances);
        }
    }
}
