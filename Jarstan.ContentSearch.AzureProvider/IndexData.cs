// Decompiled with JetBrains decompiler
// Type: Sitecore.ContentSearch.LuceneProvider.IndexData
// Assembly: Sitecore.ContentSearch.LuceneProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B719F69-8579-4D19-90A9-D118C717CF98
// Assembly location: C:\Users\jscott\Downloads\Sitecore 8.1 rev. 151207\Sitecore 8.1 rev. 151207\SearchDlls\Sitecore.ContentSearch.LuceneProvider.dll

using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class IndexData
    {
        public static readonly Regex SpecialCharactersRx = new Regex("(\\+|\\-|\\&\\&|\\|\\||\\!|\\{|\\}|\\[|\\]|\\^|\\(|\\)|\\\"|\\~|\\:|\\;|\\\\|\\?|\\*|\\/)", RegexOptions.Compiled);

        public CultureInfo Culture { get; protected set; }

        protected Document Document { get; set; }

        public ConcurrentQueue<AzureField> Fields { get; set; }

        public AzureField UpdateTerm {get; set; }

        public AzureField FullUpdateTerm { get; set; }

        public IAzureProviderIndex AzureIndex { get; set; }

        public bool IsEmpty
        {
            get
            {
                if (this.Document != null)
                    return this.Fields.Count == 0;
                return true;
            }
        }

        public IndexData(ISearchIndex index, IIndexable indexable, Document document, ConcurrentQueue<AzureField> fields)
        {
            this.Document = document;
            this.Fields = fields;
            this.AzureIndex = (IAzureProviderIndex)index;
            
            this.UpdateTerm = new AzureField("s_key", index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(indexable.UniqueId.Value, "s_key"), BuildKeyField("s_key"));
            this.FullUpdateTerm = new AzureField("s_uniqueid", index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(indexable.UniqueId.Value, "s_uniqueid"), AzureFieldBuilder.BuildField("s_uniqueid", index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(indexable.UniqueId.Value, "s_uniqueid"), this.AzureIndex));
            this.Culture = indexable.Culture;
        }

        public IndexData(ISearchIndex index, IIndexable indexable, AzureDocumentBuilder documentBuilder)
          : this(index, indexable, documentBuilder.Document, documentBuilder.CollectedFields)
        {
        }

        private Field BuildKeyField(string name)
        {
            var field = AzureFieldBuilder.BuildField(name, "", this.AzureIndex);
            field.IsKey = true;
            return field;
        }

        public static string Quote(string value)
        {
            string str = IndexData.SpecialCharactersRx.Replace(value, "\\$1");
            if (str.IndexOf(' ') != -1 || str == "")
                str = string.Format("\"{0}\"", str);
            return str;
        }

        private static byte[] GetBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public Document BuildDocument()
        {
            Assert.IsNotNull(this.Document, "Document");
            AzureField result;
            if (!this.Fields.TryDequeue(out result))
                return this.Document;
            
            this.Document.Add(result.Name, result.Value.ToString());
            while (this.Fields.TryDequeue(out result))
            {
                if (!Document.ContainsKey(result.Name))
                {
                    //Reconcile value based on Document DataType
                    var val = result.Value;
                    if (result.DataType == DataType.Boolean)
                    {
                        val = val.ToString() == "1" ? true : false;
                    }

                    if (result.DataType == DataType.DateTimeOffset)
                    {
                        DateTime d;
                        DateTime.TryParse(val.ToString(), out d);
                        val = d;
                    }

                    if (result.DataType == DataType.Double)
                    {
                        Double d;
                        Double.TryParse(val.ToString(), out d);
                        val = d;
                    }

                    if (result.DataType == DataType.Int32)
                    {
                        Int32 d;
                        Int32.TryParse(val.ToString(), out d);
                        val = d;
                    }

                    if (result.DataType == DataType.Int64)
                    {
                        Int64 d;
                        Int64.TryParse(val.ToString(), out d);
                        val = d;
                    }

                    if (result.DataType.ToString() == DataType.Collection(DataType.String).ToString())
                    {
                        var list = new List<string>();
                        list.Add(val.ToString());
                        val = list;
                    }

                    this.Document.Add(result.Name, val);
                }
                else
                {
                    if (result.Value != null)
                    {
                        object objVal;
                        var val = result.Value;
                        var currentDocValue = this.Document.TryGetValue(result.Name, out objVal);
                        if (objVal != null)
                        {
                            if (result.DataType.ToString() == DataType.Collection(DataType.String).ToString())
                            {
                                var list = (List<string>)objVal;
                                list.Add(result.Value.ToString());
                                val = list;
                            }
                            else
                            {
                                val = objVal + " " + result.Value;
                            }
                        }
                        this.Document.Remove(result.Name);
                        this.Document.Add(result.Name, val);
                    }
                }
            }

            return this.Document;
        }
    }
}
