using Lucene.Net.Search;
using Sitecore.ContentSearch.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    public class BetweenFilterQuery : BaseFilterQuery
    {
        public FilterQuery LeftQuery { get; private set; }
        public FilterQuery RightQuery { get; private set; }

        public BetweenFilterQuery(string fieldName, object from, object to, Inclusion inclusion)
        {
            switch(inclusion)
            {
                case Inclusion.Both:
                    LeftQuery = new FilterQuery(fieldName, from, FilterQuery.FilterQueryTypes.GreaterThanEquals);
                    RightQuery = new FilterQuery(fieldName, to, FilterQuery.FilterQueryTypes.LessThanEquals);
                    break;
                case Inclusion.Lower:
                    LeftQuery = new FilterQuery(fieldName, from, FilterQuery.FilterQueryTypes.GreaterThanEquals);
                    RightQuery = new FilterQuery(fieldName, to, FilterQuery.FilterQueryTypes.LessThan);
                    break;
                case Inclusion.Upper:
                    LeftQuery = new FilterQuery(fieldName, from, FilterQuery.FilterQueryTypes.GreaterThan);
                    RightQuery = new FilterQuery(fieldName, to, FilterQuery.FilterQueryTypes.LessThanEquals);
                    break;
                case Inclusion.None:
                    LeftQuery = new FilterQuery(fieldName, from, FilterQuery.FilterQueryTypes.GreaterThan);
                    RightQuery = new FilterQuery(fieldName, to, FilterQuery.FilterQueryTypes.LessThan);
                    break;
            }
        }

        public override string ToString()
        {
            return string.Format("({0} and {1})", LeftQuery, RightQuery);
        }
    }
}
