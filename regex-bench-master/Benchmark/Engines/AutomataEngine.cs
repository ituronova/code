using Microsoft.Automata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Benchmark.Engines
{
    class AutomataEngine : IEngine
    {
        public IMatcher Regex { get; }

        public AutomataEngine(IMatcher regex)
        {
            Regex = regex;
        }

        public void FindAllMatches(string input)
        {
            Regex.Matches(input);
        }

        public void FindAllMatches(byte[] input)
        {
            Regex.Matches(Encoding.UTF8.GetString(input));
        }
    }

    class AutomataCompiler : ICompiler
    {
        public RegexOptions Options { get; }
        CharSetSolver css;

        public AutomataCompiler(RegexOptions options = RegexOptions.None)
        {
            Options = options;
            css = new CharSetSolver(BitWidth.BV16);
        }

        public IEngine Compile(string pattern)
        {
            var regex = new Regex(pattern, Options);
            var symbolicRegex = regex.Compile();
            return new AutomataEngine(symbolicRegex);
        }
    }
}
