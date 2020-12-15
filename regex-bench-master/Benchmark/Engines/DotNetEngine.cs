using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Benchmark.Engines
{
    class DotNetEngine : IEngine
    {
        public Regex Regex { get; }

        public DotNetEngine(Regex regex)
        {
            Regex = regex;
        }

        public void FindAllMatches(string input)
        {
            var e = Regex.Matches(input).GetEnumerator();
            while (e.MoveNext()) { }
        }

        public void FindAllMatches(byte[] input)
        {
            FindAllMatches(Encoding.UTF8.GetString(input));
        }
    }

    class DotNetCompiler : ICompiler
    {
        public RegexOptions Options { get; }

        public DotNetCompiler(RegexOptions options = RegexOptions.None)
        {
            Regex.CacheSize = 0;
            Options = options;
        }

        public IEngine Compile(string pattern)
        {
            var regex = new Regex(pattern, Options);
            regex.IsMatch("");
            return new DotNetEngine(regex);
        }
    }
}
