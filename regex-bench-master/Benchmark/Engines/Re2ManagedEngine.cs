using RE2.Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Engines
{
    class Re2ManagedEngine : IEngine
    {
        public string Regex { get; }

        public Re2ManagedEngine(string regex)
        {
            Regex = regex;
        }

        public void FindAllMatches(string input)
        {
            byte[] buffer = null;
            String8 input8 = String8.Convert(input, ref buffer);
            var e = Regex2.Matches(input8, Regex).GetEnumerator();
            while (e.MoveNext()) { }
        }

        public void FindAllMatches(byte[] input)
        {
            var e = Regex2.Matches(new String8(input, 0, input.Length), Regex).GetEnumerator();
            while (e.MoveNext()) { }
        }
    }

    class Re2ManagedCompiler : ICompiler
    {
        public IEngine Compile(string pattern)
        {
            Regex2.IsMatch(String8.Empty, pattern);
            return new Re2ManagedEngine(pattern);
        }
    }
}
