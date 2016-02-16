using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.AzureProvider
{
    public class AzureField
    {
        public AzureField(string name, object value, Field field, Microsoft.Azure.Search.Models.DataType dataType = null)
        {
            Name = name;
            Value = value;
            Field = field;
            DataType = dataType ?? Microsoft.Azure.Search.Models.DataType.String;
        }

        public string Name { get; set; }
        public object Value { get; set; }
        public Microsoft.Azure.Search.Models.DataType DataType { get; set; }

        public Field Field { get; set; }
        public float Boost { get; set; }
    }
}
