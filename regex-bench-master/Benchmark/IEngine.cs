using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    interface IEngine
    {
        void FindAllMatches(string input);
        void FindAllMatches(byte[] input);
    }

    interface ICompiler
    {
        IEngine Compile(string pattern);
    }
}
