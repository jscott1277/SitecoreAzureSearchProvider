using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Converters;
using System;
using System.ComponentModel;
using System.Globalization;

namespace Slalom.ContentSearch.AzureProvider.Converters
{
    public class IndexableUniqueIdConverter : AbstractTypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (!(sourceType == typeof(IIndexableUniqueId)))
                return base.CanConvertFrom(context, sourceType);
            return true;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (!(destinationType == typeof(object)))
                return base.CanConvertTo(context, destinationType);
            return true;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var encodeUniqueId = EncodeUniqueId(value.ToString());

            return (Activator.CreateInstance(typeof(IndexableUniqueId<>).MakeGenericType(encodeUniqueId.GetType()), new object[1]
            {
                value
            }) as IIndexableUniqueId);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var deccodeUniqueId = DecodeUniqueId(value.ToString());

            return (Activator.CreateInstance(typeof(IndexableUniqueId<>).MakeGenericType(deccodeUniqueId.GetType()), new object[1]
            {
                value
            }) as IIndexableUniqueId).Value;
        }

        private string EncodeUniqueId(string uniqueId)
        {
            //sitecore://master/{3D6658D8-A0BF-4E75-B3E2-D050FABCF4E1}?lang=en&ver=1
            return uniqueId.Replace(":", "[c]").Replace("/", "[fs]").Replace("{", "[lc]").Replace("-", "[h]").Replace("}", "[rc]").Replace("?", "[qm]").Replace("&", "[amp]");
        }

        private string DecodeUniqueId(string uniqueId)
        {
            //sitecore://master/{3D6658D8-A0BF-4E75-B3E2-D050FABCF4E1}?lang=en&ver=1
            return uniqueId.Replace("[c]", ";").Replace("[fs]", "/").Replace("[lc]", "{").Replace("[h]", "-").Replace("[rc]", "}").Replace("[qm]", "?").Replace("[amp]", "&");
        }
    }
}
