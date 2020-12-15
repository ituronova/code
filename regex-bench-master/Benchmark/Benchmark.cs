using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Benchmark
{
    class Benchmark
    {
        public string Name { get; }
        public IEnumerable<Stream> Dataset { get; }
        public List<string> Patterns { get; }
        public List<Tuple<string, Func<ICompiler>>> Matchers { get; }
        public List<Metric> Metrics
        {
            get
            {
                if (null == metrics)
                {
                    throw new InvalidOperationException("Call Measure() before accessing Metrics");
                }
                return metrics;
            }
        }
        private List<Metric> metrics;

        public Benchmark(string name, IEnumerable<Stream> dataset, IEnumerable<string> patterns, IEnumerable<Tuple<string, Func<ICompiler>>> matchers)
        {
            Name = name;
            Dataset = dataset;
            Patterns = patterns.ToList();
            Matchers = matchers.ToList();
        }

        public void Measure(TextWriter status = null, bool measureCold = false, bool measureParallel = false, bool measureUtf8 = false, bool measureUtf16 = false, bool measureCompile = false, Func<Sample, bool> samplingPredicate = null)
        {
            if (status == null)
            {
                status = Console.Out;
            }
            if (samplingPredicate == null)
            {
                samplingPredicate = FixedSampleSize(1);
            }

            metrics = new List<Metric>();

            status.WriteLine($"=== Benchmark: {Name} ===");

            if (measureCompile)
            {
                if (measureCold)
                {
                    status.WriteLine("Measuring cold start compilation time...");
                    metrics.Add(MeasureCompileCold(samplingPredicate, status));
                }

                status.WriteLine("Measuring hot start compilation time...");
                metrics.Add(MeasureCompileHot(samplingPredicate, status));
            }

            if (measureUtf16)
            {
                if (measureCold)
                {
                    status.WriteLine("Measuring UTF-16 cold start matching time...");
                    metrics.Add(MeasureCold(samplingPredicate, status, "UTF-16",
                        x => new StreamReader(x).ReadToEnd(),
                        (matcher, input) => matcher.FindAllMatches(input)));
                }

                status.WriteLine("Measuring UTF-16 hot start matching time...");
                metrics.Add(MeasureHot(samplingPredicate, status, "UTF-16",
                    x => new StreamReader(x).ReadToEnd(),
                    (matcher, input) => matcher.FindAllMatches(input)));

                if (measureParallel)
                {
                    status.WriteLine("Measuring UTF-16 parallel matching time...");
                    metrics.Add(MeasureParallel(samplingPredicate, status, "UTF-16",
                        x => new StreamReader(x).ReadToEnd(),
                        (matcher, input) => matcher.FindAllMatches(input)));
                }
            }

            if (measureUtf8)
            {
                if (measureCold)
                {
                    status.WriteLine("Measuring UTF-8 cold start matching time...");
                    metrics.Add(MeasureCold(samplingPredicate, status, "UTF-8",
                        x =>
                        {
                            var memory = new MemoryStream();
                            x.CopyTo(memory);
                            return memory.ToArray();
                        },
                        (matcher, input) => matcher.FindAllMatches(input)));
                }

                status.WriteLine("Measuring UTF-8 hot start matching time...");
                metrics.Add(MeasureHot(samplingPredicate, status, "UTF-8",
                    x =>
                    {
                        var memory = new MemoryStream();
                        x.CopyTo(memory);
                        return memory.ToArray();
                    },
                    (matcher, input) => matcher.FindAllMatches(input)));

                if (measureParallel)
                {
                    status.WriteLine("Measuring UTF-8 parallel matching time...");
                    metrics.Add(MeasureParallel(samplingPredicate, status, "UTF-8",
                        x =>
                        {
                            var memory = new MemoryStream();
                            x.CopyTo(memory);
                            return memory.ToArray();
                        },
                        (matcher, input) => matcher.FindAllMatches(input)));
                }
            }
        }

        private Func<Sample, bool> FixedSampleSize(int n) => s => s.Observations.Count < n;

        private Metric MeasureCompile(TextWriter status, string namePrefix, Func<string, Func<ICompiler>, Sample> sample)
        {
            var metric = new Metric($"{namePrefix} compilation",
                new Axis("Pattern", Patterns),
                new Axis("Matcher", Matchers.Select(x => x.Item1)));
            for (int i = 0; i < Patterns.Count; ++i)
            {
                status.WriteLine($"Pattern {i+1}/{Patterns.Count}");
                var pattern = Patterns[i];
                foreach (var matcher in Matchers)
                {
                    var compileTimeSample = sample(pattern, matcher.Item2);
                    metric.Add(compileTimeSample, pattern, matcher.Item1);
                }
            }
            return metric;
        }

        private Metric MeasureCompileCold(Func<Sample, bool> samplingPredicate, TextWriter status) => MeasureCompile(status, "Cold",
            (pattern, getCompiler) => SampleWhile(samplingPredicate, () => getCompiler().Compile(pattern)));

        private Metric MeasureCompileHot(Func<Sample, bool> samplingPredicate, TextWriter status) => MeasureCompile(status, "Hot",
            (pattern, getCompiler) =>
            {
                var compiler = getCompiler();
                compiler.Compile(pattern);
                return SampleWhile(samplingPredicate, () => compiler.Compile(pattern));
            });

        private Metric MeasureCold<T>(Func<Sample, bool> samplingPredicate, TextWriter status, string namePrefix, Func<Stream, T> prepInput, Action<IEngine, T> callMatcher)
        {
            var metric = new Metric($"{namePrefix} cold",
                new Axis("Pattern", Patterns),
                new Axis("Matcher", Matchers.Select(x => x.Item1)));
            var compilers = (from matcher in Matchers
                             select new { matcher.Item1, Item2 = matcher.Item2() }).ToDictionary(x => x.Item1, x => x.Item2);
            status.Write($"Reading dataset..."); status.Flush();
            var stream = new ConcatenatedStream(Dataset);
            stream.Seek(0, SeekOrigin.Begin);
            var input = prepInput(stream);
            status.WriteLine($" done.");
            foreach (var pattern in Patterns)
            {
                foreach (var matcher in Matchers)
                {
                    var compiler = compilers[matcher.Item1];
                    var matchingTimeSample = SampleWhile(samplingPredicate, () => callMatcher(compiler.Compile(pattern), input));
                    metric.Add(matchingTimeSample, pattern, matcher.Item1);
                }
            }
            return metric;
        }

        private Metric MeasureHot<T>(Func<Sample, bool> samplingPredicate, TextWriter status, string namePrefix, Func<Stream, T> prepInput, Action<IEngine, T> callMatcher)
        {
            var metric = new Metric($"{namePrefix} hot",
                new Axis("Pattern", Patterns),
                new Axis("Matcher", Matchers.Select(x => x.Item1)));
            foreach (var p in Patterns)
            {
                foreach(var m in Matchers)
                {
                    var i1 = new { Item1 = p, Item2 = m.Item1 };
                    var i2 = m.Item2().Compile(p);
                }
            }
            var matchers = (from pattern in Patterns
                            from matcher in Matchers
                            select new { Item1 = new { Item1 = pattern, Item2 = matcher.Item1 }, Item2 = matcher.Item2().Compile(pattern) }).ToDictionary(x => x.Item1, x => x.Item2);
            status.Write($"Reading dataset..."); status.Flush();
            var stream = new ConcatenatedStream(Dataset);
            stream.Seek(0, SeekOrigin.Begin);
            var input = prepInput(stream);
            status.WriteLine($" done.");
            for (int i = 0; i < Patterns.Count; ++i)
            {
                status.WriteLine($"Pattern {i+1}/{Patterns.Count}");
                var pattern = Patterns[i];
                foreach (var entry in Matchers)
                {
                    var matcher = matchers[new { Item1 = pattern, Item2 = entry.Item1 }];
                    callMatcher(matcher, input);
                    var matchingTimeSample = SampleWhile(samplingPredicate, () => callMatcher(matcher, input));
                    metric.Add(matchingTimeSample, pattern, entry.Item1);
                }
            }
            return metric;
        }

        private Metric MeasureParallel<T>(Func<Sample, bool> samplingPredicate, TextWriter status, string namePrefix, Func<Stream, T> prepInput, Action<IEngine, T> callMatcher)
        {
            var metric = new Metric($"{namePrefix} parallel",
                new Axis("Pattern", Patterns),
                new Axis("Matcher", Matchers.Select(x => x.Item1)));
            var compilers = (from matcher in Matchers
                             select new { matcher.Item1, Item2 = matcher.Item2() }).ToDictionary(x => x.Item1, x => x.Item2);
            status.Write($"Reading dataset..."); status.Flush();
            var inputs = new List<T>();
            foreach (var stream in Dataset)
            {
                stream.Seek(0, SeekOrigin.Begin);
                inputs.Add(prepInput(stream));
            }
            status.WriteLine($" done.");
            foreach (var pattern in Patterns)
            {
                foreach (var entry in Matchers)
                {
                    var compiler = compilers[entry.Item1];
                    var matcher = compiler.Compile(pattern);
                    var matchingTimeSample = SampleWhile(samplingPredicate, () =>
                    {
                        var partitioner = Partitioner.Create(inputs, true);
                        partitioner.AsParallel().ForAll(x => callMatcher(matcher, x));
                    });
                    metric.Add(matchingTimeSample, pattern, entry.Item1);
                }
            }
            return metric;
        }

        private Sample SampleWhile(Func<Sample, bool> predicate, Action a)
        {
            var sample = new Sample();
            var watch = new Stopwatch();
            while (predicate(sample))
            {
                watch.Start();
                a();
                watch.Stop();
                sample.Observations.Add(watch.Elapsed);
            }
            return sample;
        }
    }
}
