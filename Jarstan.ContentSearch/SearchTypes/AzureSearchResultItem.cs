using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Converters;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Fields;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Sitecore.ContentSearch.SearchTypes;

namespace Jarstan.ContentSearch.SearchTypes
{
    [DataContract]
    [DebuggerDisplay("Name={Name}, TemplateName={TemplateName}, Version={Version}, Language={Language}")]
    public class AzureSearchResultItem : ISearchResult, IObjectIndexers
    {
        private readonly Dictionary<string, object> fields = new Dictionary<string, object>();

        [DataMember, IndexField("s__smallcreateddate")]
        public virtual DateTimeOffset CreatedDate { get; set; }

        [DataMember, IndexField("urllink")]
        public virtual string Url { get; set; }

        [DataMember, IndexField("s_datasource")]
        public virtual string Datasource { get; set; }

        [DataMember, IndexField("parsedcreatedby")]
        public virtual string CreatedBy { get; set; }

        [DataMember, IndexField("s_group"), TypeConverter(typeof(IndexFieldIDValueConverter))]
        public virtual ID ItemId { get; set; }

        [DataMember, IndexField("s_language")]
        public virtual string Language { get; set; }

        [DataMember, IndexField("s_name")]
        public virtual string Name { get; set; }

        [DataMember, IndexField("s_fullpath")]
        public virtual string Path { get; set; }

        [IndexField("s_path")]
        public virtual IEnumerable<ID> Paths { get; set; }

        [DataMember, IndexField("s_parent")]   
        public virtual ID Parent { get; set; }

        [DataMember, IndexField("s_template"), TypeConverter(typeof(IndexFieldIDValueConverter))]
        public virtual ID TemplateId { get; set; }

        [DataMember, IndexField("s_templatename")]
        public virtual string TemplateName { get; set; }

        [DataMember, IndexField("s_database")]
        public virtual string DatabaseName { get; set; }

        [DataMember, IndexField("s__smallupdateddate")]
        public virtual DateTimeOffset Updated { get; set; }

        [DataMember, IndexField("parsedupdatedby")]
        public virtual string UpdatedBy { get; set; }

        [DataMember, IndexField("s_content")]
        public virtual string Content { get; set; }

        [IndexField("s__semantics")]
        public virtual IEnumerable<ID> Semantics { get; set; }

        [IndexField("site")]
        public virtual IEnumerable<string> Sites { get; set; }

        [IndexField("s_key")]
        public virtual string Key { get; set; }

        [IndexField("s_uniqueid")]
        public virtual string UniqueId { get; set; }


        [DataMember, IndexField("_uniqueid"), TypeConverter(typeof(IndexFieldItemUriValueConverter)), XmlIgnore]
        public virtual ItemUri Uri { get; set; }

        [DataMember, IndexField("s_version")]
        public virtual string Version
        {
            get
            {
                if (Uri == null)
                    Uri = new ItemUri(this["s_uniqueId"]);
                return Uri.Version.Number.ToString(CultureInfo.InvariantCulture);
            }
        }

        public virtual Dictionary<string, object> Fields
        {
            get
            {
                return fields;
            }
        }

        public virtual string this[string key]
        {
            get
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return fields[key.ToLowerInvariant()].ToString();
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                fields[key.ToLowerInvariant()] = value;
            }
        }

        public virtual object this[ObjectIndexerKey key]
        {
            get
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return fields[key.ToString().ToLowerInvariant()];
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                fields[key.ToString().ToLowerInvariant()] = value;
            }
        }

        public virtual Sitecore.Data.Items.Item GetItem()
        {
            if (Uri == null)
                Uri = new ItemUri(this["s_uniqueId"]);
            return Factory.GetDatabase(Uri.DatabaseName).GetItem(Uri.ItemID, Uri.Language, Uri.Version);
        }

        public virtual Field GetField(ID fieldId)
        {
            if (Uri == null)
                Uri = new ItemUri(this["s_url"]);
            return Factory.GetDatabase(Uri.DatabaseName).GetItem(Uri.ItemID, Uri.Language, Uri.Version).Fields[fieldId];
        }

        public virtual FieldCollection GetFields(ID[] fieldId)
        {
            if (Uri == (ItemUri)null)
                Uri = new ItemUri(this["s_url"]);
            Sitecore.Data.Items.Item obj = Factory.GetDatabase(Uri.DatabaseName).GetItem(Uri.ItemID, Uri.Language, Uri.Version);
            foreach (ID fieldId1 in fieldId)
                obj.Fields.EnsureField(fieldId1);
            return obj.Fields;
        }

        public virtual Field GetField(string fieldName)
        {
            if (Uri == null)
                Uri = new ItemUri(this["s_url"]);
            return Factory.GetDatabase(Uri.DatabaseName).GetItem(Uri.ItemID, Uri.Language, Uri.Version).Fields[fieldName];
        }

        public override string ToString()
        {
            return Enumerable.Aggregate(Enumerable.Cast<string>(fields.Keys), string.Format("{0}, {1}, {2}", Uri.ItemID, Uri.Language, Uri.Version), ((current, key) => current + ", " + fields[key]));
        }

        //public IQueryable<TResult> GetDescendants<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Sitecore.Data.Items.Item sitecoreItem = GetItem();
        //    string s = IdHelper.NormalizeGuid(sitecoreItem.ID.ToString(), true);
        //    return Queryable.Where(context.GetQueryable<TResult>(new CultureExecutionContext(new CultureInfo(Language))), (i => (i.Parent == sitecoreItem.ID || Enumerable.Contains<ID>(i.Paths, sitecoreItem.ID)) && i["s_group"] != s));
        //}

        //public IQueryable<TResult> GetChildren<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Sitecore.Data.Items.Item sitecoreItem = GetItem();
        //    return Queryable.Where<TResult>(context.GetQueryable<TResult>((IExecutionContext)new CultureExecutionContext(new CultureInfo(Language))), (i => i.Parent == sitecoreItem.ID));
        //}

        //public IQueryable<TResult> GetAncestors<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Expression<Func<TResult, bool>> expression = PredicateBuilder.True<TResult>();
        //    ID currentItemid = GetItem().ID;

        //    foreach (ID id in EnumerableExtensions.RemoveWhere(Paths, (i => i == currentItemid)))
        //    {
        //        string normalizeGuid = IdHelper.NormalizeGuid(id.ToString(), true);
        //        expression = PredicateBuilder.Or(expression, (i => i["s_group"] == normalizeGuid));
        //    }
        //    return Queryable.Where(context.GetQueryable<TResult>(new CultureExecutionContext(new CultureInfo(Language))), expression);
        //}
    }
}
