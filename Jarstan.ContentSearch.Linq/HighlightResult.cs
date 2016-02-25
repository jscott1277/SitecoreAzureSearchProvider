using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarstan.ContentSearch.Linq
{
    public class HighlightResult
    {
        public string Name { get; protected set; }

        public List<string> Values { get; protected set; }

        public HighlightResult(string name, IEnumerable<string> values)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (values == null)
                throw new ArgumentNullException("values");
            this.Name = name;
            this.Values = values.ToList();
        }
    }
}
