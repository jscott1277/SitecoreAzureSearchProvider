using Sitecore.ContentSearch.Linq.Common;
using System;

namespace Slalom.ContentSearch.Linq.Azure
{
    public class FieldExecutionContext : IExecutionContext
    {
        public string FieldName { get; set; }

        public FieldExecutionContext(string fieldName)
        {
            if (fieldName == null)
                throw new ArgumentNullException("fieldName");
            this.FieldName = fieldName;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FieldExecutionContext))
                return false;
            return ((FieldExecutionContext)obj).FieldName.Equals(this.FieldName);
        }

        public override int GetHashCode()
        {
            return typeof(FieldExecutionContext).GetHashCode() ^ this.FieldName.GetHashCode();
        }
    }
}
