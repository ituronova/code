using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    struct Axis
    {
        public string Name;
        public IEnumerable<string> Classes;

        public Axis(string name, IEnumerable<string> classes)
        {
            Name = name;
            Classes = classes;
        }
    }

    public class ArrayValueComparer<T> : IEqualityComparer<T[]>
    {
        public bool Equals(T[] a, T[] b) => a == b || (a != null && b != null && a.SequenceEqual(b));
        // As in boost::hash_combine
        public int GetHashCode(T[] a) => (int)a.Aggregate((uint)0, (hash, x) => hash ^ ((uint)x.GetHashCode() + 0x9e3779b9 + (hash << 6) + (hash >> 2)));
    }

    class Metric
    {
        public string Name { get; }
        public string[] Axes { get; }
        public List<string>[] Classes { get; }
        public Dictionary<string[], Sample> Samples { get; } = new Dictionary<string[], Sample>(new ArrayValueComparer<string>());

        public Metric(string name, params Axis[] axes)
        {
            Name = name;
            Axes = axes.Select(x => x.Name).ToArray();
            Classes = axes.Select(x => x.Classes.ToList()).ToArray();
        }

        public void Add(Sample sample, params string[] index)
        {
            if (index.Length != Axes.Length)
            {
                throw new ArgumentException($"Expected {Axes.Length} indices. Got {index.Length}");
            }

            Samples.Add(index, sample);
        }
    }
}
