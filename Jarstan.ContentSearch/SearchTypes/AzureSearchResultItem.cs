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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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

        [DataMember]
        [IndexField("s__smallcreateddate")]
        public virtual DateTimeOffset CreatedDate { get; set; }

        [DataMember]
        [IndexField("urllink")]
        public virtual string Url { get; set; }

        [IndexField("s_datasource")]
        [DataMember]
        public virtual string Datasource { get; set; }

        [DataMember]
        [IndexField("parsedcreatedby")]
        public virtual string CreatedBy { get; set; }

        [DataMember]
        [IndexField("s_group")]
        [TypeConverter(typeof(IndexFieldIDValueConverter))]
        public virtual ID ItemId { get; set; }

        [DataMember]
        [IndexField("s_language")]
        public virtual string Language { get; set; }

        [DataMember]
        [IndexField("s_name")]
        public virtual string Name { get; set; }

        [DataMember]
        [IndexField("s_fullpath")]
        public virtual string Path { get; set; }

        [IndexField("s_path")]
        public virtual IEnumerable<ID> Paths { get; set; }

        [IndexField("s_parent")]
        [DataMember]
        public virtual ID Parent { get; set; }

        [TypeConverter(typeof(IndexFieldIDValueConverter))]
        [IndexField("s_template")]
        [DataMember]
        public virtual ID TemplateId { get; set; }

        [IndexField("s_templatename")]
        [DataMember]
        public virtual string TemplateName { get; set; }

        [DataMember]
        [IndexField("s_database")]
        public virtual string DatabaseName { get; set; }

        [IndexField("s__smallupdateddate")]
        [DataMember]
        public virtual DateTimeOffset Updated { get; set; }

        [DataMember]
        [IndexField("parsedupdatedby")]
        public virtual string UpdatedBy { get; set; }

        [IndexField("s_content")]
        [DataMember]
        public virtual string Content { get; set; }

        [IndexField("s__semantics")]
        public virtual IEnumerable<ID> Semantics { get; set; }

        [IndexField("site")]
        public virtual IEnumerable<string> Sites { get; set; }

        [IndexField("s_key")]
        public virtual string Key { get; set; }

        [IndexField("s_uniqueid")]
        public virtual string UniqueId { get; set; }


        [IndexField("_uniqueid"), TypeConverter(typeof(IndexFieldItemUriValueConverter)), DataMember, XmlIgnore]
        public virtual ItemUri Uri { get; set; }

        [DataMember]
        [IndexField("s_version")]
        public virtual string Version
        {
            get
            {
                if (this.Uri == null)
                    this.Uri = new ItemUri(this["s_uniqueId"]);
                return this.Uri.Version.Number.ToString(CultureInfo.InvariantCulture);
            }
            set
            {
            }
        }

        public virtual Dictionary<string, object> Fields
        {
            get
            {
                return this.fields;
            }
        }

        public virtual string this[string key]
        {
            get
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return this.fields[key.ToLowerInvariant()].ToString();
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                this.fields[key.ToLowerInvariant()] = (object)value;
            }
        }

        public virtual object this[ObjectIndexerKey key]
        {
            get
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return this.fields[key.ToString().ToLowerInvariant()];
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                this.fields[key.ToString().ToLowerInvariant()] = value;
            }
        }

        public virtual Sitecore.Data.Items.Item GetItem()
        {
            if (this.Uri == (ItemUri)null)
                this.Uri = new ItemUri(this["s_uniqueId"]);
            return Factory.GetDatabase(this.Uri.DatabaseName).GetItem(this.Uri.ItemID, this.Uri.Language, this.Uri.Version);
        }

        public virtual Field GetField(ID fieldId)
        {
            if (this.Uri == (ItemUri)null)
                this.Uri = new ItemUri(this["s_url"]);
            return Factory.GetDatabase(this.Uri.DatabaseName).GetItem(this.Uri.ItemID, this.Uri.Language, this.Uri.Version).Fields[fieldId];
        }

        public virtual FieldCollection GetFields(ID[] fieldId)
        {
            if (this.Uri == (ItemUri)null)
                this.Uri = new ItemUri(this["s_url"]);
            Sitecore.Data.Items.Item obj = Factory.GetDatabase(this.Uri.DatabaseName).GetItem(this.Uri.ItemID, this.Uri.Language, this.Uri.Version);
            foreach (ID fieldId1 in fieldId)
                obj.Fields.EnsureField(fieldId1);
            return obj.Fields;
        }

        public virtual Field GetField(string fieldName)
        {
            if (this.Uri == (ItemUri)null)
                this.Uri = new ItemUri(this["s_url"]);
            return Factory.GetDatabase(this.Uri.DatabaseName).GetItem(this.Uri.ItemID, this.Uri.Language, this.Uri.Version).Fields[fieldName];
        }

        public override string ToString()
        {
            return Enumerable.Aggregate<string, string>(Enumerable.Cast<string>(this.fields.Keys), string.Format("{0}, {1}, {2}", (object)this.Uri.ItemID, (object)this.Uri.Language, (object)this.Uri.Version), ((current, key) => current + (object)", " + (string)this.fields[key]));
        }

        //public IQueryable<TResult> GetDescendants<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Sitecore.Data.Items.Item sitecoreItem = this.GetItem();
        //    string s = IdHelper.NormalizeGuid(sitecoreItem.ID.ToString(), true);
        //    return Queryable.Where(context.GetQueryable<TResult>(new CultureExecutionContext(new CultureInfo(this.Language))), (i => (i.Parent == sitecoreItem.ID || Enumerable.Contains<ID>(i.Paths, sitecoreItem.ID)) && i["s_group"] != s));
        //}

        //public IQueryable<TResult> GetChildren<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Sitecore.Data.Items.Item sitecoreItem = this.GetItem();
        //    return Queryable.Where<TResult>(context.GetQueryable<TResult>((IExecutionContext)new CultureExecutionContext(new CultureInfo(this.Language))), (i => i.Parent == sitecoreItem.ID));
        //}

        //public IQueryable<TResult> GetAncestors<TResult>(IProviderSearchContext context) where TResult : AzureSearchResultItem, new()
        //{
        //    Expression<Func<TResult, bool>> expression = PredicateBuilder.True<TResult>();
        //    ID currentItemid = this.GetItem().ID;

        //    foreach (ID id in EnumerableExtensions.RemoveWhere(this.Paths, (i => i == currentItemid)))
        //    {
        //        string normalizeGuid = IdHelper.NormalizeGuid(id.ToString(), true);
        //        expression = PredicateBuilder.Or(expression, (i => i["s_group"] == normalizeGuid));
        //    }
        //    return Queryable.Where(context.GetQueryable<TResult>(new CultureExecutionContext(new CultureInfo(this.Language))), expression);
        //}
    }
}
