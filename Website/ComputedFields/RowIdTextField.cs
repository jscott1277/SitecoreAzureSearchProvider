using Sitecore.ContentSearch;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.Data.Items;
using System;

namespace Website.ComputedFields
{
    public class RowIdTextField : IComputedIndexField
    {
        public virtual string FieldName { get; set; }

        public virtual string ReturnType { get; set; }

        public object ComputeFieldValue(IIndexable indexable)
        {
            var item = (Item)(indexable as SitecoreIndexableItem);

            if (item == null)
                return null;

            return Guid.NewGuid();
        }
    }
}