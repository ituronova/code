using Benchmark.Engines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{

    class Program
    {
        static IEnumerable<Stream> StreamsFromDirectoryTree(string dirPath) => Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories).Select(path => new FileStream(path, FileMode.Open));

        static IEnumerable<string> PatternsFromFile(string path) => File.ReadAllLines(path).Distinct().ToList();

        static Tuple<string, Func<ICompiler>>[] Compilers = new Tuple<string, Func<ICompiler>>[]
            {
                Tuple.Create<string, Func<ICompiler>>(".NET", () => new DotNetCompiler(System.Text.RegularExpressions.RegexOptions.Compiled)),
                Tuple.Create<string, Func<ICompiler>>("Re2", () => new Re2ManagedCompiler()),
                Tuple.Create<string, Func<ICompiler>>("Automata", () => new AutomataCompiler()),
            };

        static List<Benchmark> GetBenchmarks() => new List<Benchmark>() {
            new Benchmark("Twain",
                new Stream[] { new FileStream("../../../Datasets/pg3200.txt", FileMode.Open) },
                PatternsFromFile("../../../Patterns/Twain.txt"),
                Compilers),
            new Benchmark("Automata",
                new Stream[] { new FileStream("../../../Datasets/synthetic.txt", FileMode.Open) },
                PatternsFromFile("../../../Patterns/Assorted.txt"),
                Compilers)
        };

        static void Main(string[] args)
        {
            var benchmarks = GetBenchmarks();
            foreach (var benchmark in benchmarks) {
                benchmark.Measure(measureUtf8: true, measureUtf16: true);
                benchmark.ToCsv(Console.Out);
            }
        }
    }
}
