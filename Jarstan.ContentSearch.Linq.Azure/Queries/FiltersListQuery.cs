using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Lucene.Net.Support;

namespace Jarstan.ContentSearch.Linq.Azure.Queries
{
    public class FiltersListQuery : Query, IEnumerable<BaseFilterQuery>, IEnumerable, ICloneable
    {
        private EquatableList<BaseFilterQuery> clauses = new EquatableList<BaseFilterQuery>();

        public void Add(BaseFilterQuery query)
        {
            clauses.Add(query);
        }

        public override string ToString(string field)
        {
            return String.Join(" and ", this.clauses);
        }

        public virtual List<BaseFilterQuery> Clauses
        {
            get
            {
                return this.clauses;
            }
        }

        public IEnumerator<BaseFilterQuery> GetEnumerator()
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
            filtersListQuery.clauses = (EquatableList<BaseFilterQuery>)this.clauses.Clone();
            return filtersListQuery;
        }
    }
}
