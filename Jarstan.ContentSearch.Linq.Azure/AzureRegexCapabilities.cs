using Contrib.Regex;
using System;
using System.Text.RegularExpressions;

namespace Azure.ContentSearch.Linq.Lucene
{
    public class AzureRegexCapabilities : IRegexCapabilities, IEquatable<AzureRegexCapabilities>
    {
        private readonly RegexOptions regexOptions;
        private Regex regex;

        public AzureRegexCapabilities(RegexOptions regexOptions)
        {
            this.regexOptions = regexOptions | RegexOptions.Compiled;
        }

        public void Compile(string pattern)
        {
            this.regex = new Regex(pattern, this.regexOptions);
        }

        public bool Match(string s)
        {
            return this.regex.IsMatch(s);
        }

        public string Prefix()
        {
            return (string)null;
        }

        public bool Equals(AzureRegexCapabilities other)
        {
            return other != null && (object.ReferenceEquals((object)this, (object)other) || (this.regex != null ? (!this.regex.Equals((object)other.regex) ? 1 : 0) : (other.regex != null ? 1 : 0)) == 0);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is AzureRegexCapabilities))
                return false;
            return this.Equals((AzureRegexCapabilities)obj);
        }

        public override int GetHashCode()
        {
            if (this.regex == null)
                return 0;
            return this.regex.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0} ( RegexOptions: {1} )", (object)base.ToString(), (object)this.regexOptions);
        }
    }
}
