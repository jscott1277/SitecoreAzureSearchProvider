using Microsoft.Azure.Search.Models;
using Sitecore.ContentSearch;
using System.Collections.Generic;

namespace Azure.ContentSearch.AzureProvider
{
    public class AzureDocumentBuilderOptions : DocumentBuilderOptions
    {
        internal List<Field> _customFields = new List<Field>();

        protected internal List<Field> CustomFields
        {
            get
            {
                return this._customFields;
            }
            internal set
            {
                this._customFields = value;
            }
        }
    }
}
