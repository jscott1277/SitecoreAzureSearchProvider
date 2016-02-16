using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Lucene.Net.Support;

namespace Slalom.ContentSearch.Linq.Azure.Queries
{
    public class FiltersListQuery : Query, IEnumerable<FilterQuery>, IEnumerable, ICloneable
    {
        private EquatableList<FilterQuery> clauses = new EquatableList<FilterQuery>();

        public void Add(FilterQuery query)
        {
            clauses.Add(query);
        }

        public override string ToString(string field)
        {
            return String.Join(" and ", this.clauses);
        }

        public virtual List<FilterQuery> Clauses
        {
            get
            {
                return this.clauses;
            }
        }

        public IEnumerator<FilterQuery> GetEnumerator()
        {
            return clauses.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override object Clone()
        {
            var filtersListQuery = (FiltersListQuery)base.Clone();
            filtersListQuery.clauses = (EquatableList<FilterQuery>)this.clauses.Clone();
            return filtersListQuery;
        }
    }
}
