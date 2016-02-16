using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    public class BaseFilterQuery : Query
    {
        public override string ToString(string field)
        {
            return base.ToString();
        }
    }
}
