using Microsoft.Automata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;


namespace CounterAutomataBench
{
    public enum Version
    {
        /// <summary>
        /// All algorithms
        /// </summary>
        ALL = 0,
        /// <summary>
        /// Margus version of the algorithm
        /// </summary>
        MARGUS = 1,
        /// <summary>
        /// Our version of the algorithm
        /// </summary>
        CHIPMUNK = 2,
        /// <summary>
        /// .NET regex
        /// </summary>
        REGEX = 4,
    }

    public enum Mode
    {
        /// <summary>
        /// NOOP
        /// </summary>
        NOOP = 0,
        /// <summary>
        /// Filter
        /// </summary>
        FILTER = 1,
        /// <summary>
        /// Test
        /// </summary>
        TEST = 2,
        /// <summary>
        /// Validate
        /// </summary>
        VALIDATE = 4,
        /// <summary>
        /// Decode characters
        /// </summary>
        DECODE = 5,
    }

    class Stats
    {
        List<string> header = new List<string>();
        Dictionary<string, List<string>> columns = new Dictionary<string, List<string>>();

        public void Add(string name, string value)
        {
            List<string> column;
            if (!columns.TryGetValue(name, out column))
            {
                header.Add(name);
                column = new List<string>();
                columns[name] = column;
            }
            if (value == double.NaN.ToString())
                column.Add(string.Empty);
            else
                column.Add(value);
        }

        public void Validate()
        {
            if (header.Count > 0)
            {
                Debug.Assert(columns.Count == header.Count);
                Debug.Assert(header.All(x => columns.ContainsKey(x)));
                Debug.Assert(columns.All(x => x.Value.Count == columns[header[0]].Count));
            }
        }

        public void WriteTSV(string path)
        {
            Validate();
           // CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using (var fout = new StreamWriter(new FileStream(path, FileMode.CreateNew)))
            {
                Func<string, string, string> csvJoin = (l, r) => $"{l}\t{r}";

                fout.WriteLine(header.Aggregate(csvJoin));

                var rows = header.Select(x => columns[x]).Aggregate((c1, c2) => c1.Zip(c2, csvJoin).ToList());
                foreach (var row in rows)
                {
                    fout.WriteLine(row);
                }
            }
        }
    }

    class Stat : IDisposable
    {
        Stats stats;
        String name;

        public Stat(Stats stats, string name)
        {
            this.stats = stats;
            this.name = name;
        }

        public void Dispose()
        {
            stats.Add(name, this.ToString());
        }
    }

    class HotAverage : Stat
    {
        List<double> samples = new List<double>();

        public HotAverage(Stats stats, string name) : base(stats, name)
        {
        }

        public void Add(double value)
        {
            samples.Add(value);
        }

        public override string ToString()
        {
            double average = double.NaN;
            if (samples.Count == 1)
            {
                average = samples[0];
            }
            else if (samples.Count > 1)
            {
                average = (samples.Sum() - samples[0]) / (samples.Count - 1);
            }
            return average.ToString();
        }
    }

    class Message : Stat
    {
        string message = "";

        public Message(Stats stats, string name) : base(stats, name)
        {
        }

        public void Add(string newMessage)
        {
            message += newMessage.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        public override string ToString()
        {
            return message;
        }
    }

    class Exact<T> : Stat where T : struct, IEquatable<T>
    {
        T? value;

        public Exact(Stats stats, string name) : base(stats, name)
        {
        }

        public void Add(T newValue)
        {
            if (value != null)
            {
                Debug.Assert(newValue.Equals(value.Value));
            }
            value = newValue;
        }

        public override string ToString()
        {
            var v = value.ToString();
            return value.ToString();
        }
    }

    class Program
    {
        const int SAMPLES = 10;
        const int TIMEOUT = 10000;//60 000;   // 1 min
        const int MAXSAMPLES = 10;   // number of samples for validating the automata

        static IEnumerable<string> ReadRegexes(string path)
        {
            using (var fin = new StreamReader(new FileStream(path, FileMode.Open)))
            {
                string regex;
                while ((regex = fin.ReadLine()) != null)
                {
                    yield return regex;
                }
            }
        }
        static void WriteRegexes(string path, string[] regexes)
        {
            using (var fout = new StreamWriter(new FileStream(path, FileMode.Create)))
            {
                foreach (var regex in regexes)
                {
                    fout.WriteLine(regex);
                }
            }
        }

        static Stats Benchmark(string[] regexes, bool runOur = true, bool runClassical = true, bool runValidate = false, bool runTest = true, bool runMy = true, bool runAlgebra = false, bool runOptimal1 = false)
        {
            var stats = new Stats();

            foreach (var regex in regexes)
            {
                stats.Add("pattern", regex);
            }
            // Our pipeline
            if (runOur)
            {
                Console.WriteLine("Our pipeline");
                int count = 0;
                foreach (var regex in regexes)
                {
                    HashSet<string> samples = new HashSet<string>();

                    HashSet<string> negSamples = new HashSet<string>();
                    Regex rgx = new Regex(regex, RegexOptions.Singleline);
                    // generate a set of regexes
                    if (runValidate)
                    {
                        samples = rgx.GenerateRandomDataSet(MAXSAMPLES);
                        // TODO: takes too long
                        // negSamples = rgx.Complement().GenerateRandomDataSet(MAXSAMPLES);
                    }

                    ++count;
                    Console.WriteLine($"{count}/{regexes.Length}");
                    Stopwatch sw = new Stopwatch();
                    // using (var valNegA = new Exact<int>(stats, "Negative-errors-alg"))
                    //using (var valPosA = new Exact<int>(stats, "Positive-errors-alg"))
                    //using (var valNeg = new Exact<int>(stats, "Negative-errors"))
                    //using (var regPos = new HotAverage(stats, "Regex Matcher - time (milliseconds)"))
                    // using (var timeComp = new HotAverage(stats, "Regex Compile - time (milliseconds)"))
                    //using (var timePos2 = new HotAverage(stats, "DCA Matcher opt2 - time (milliseconds)"))
                    using (var timePos1 = new HotAverage(stats, "DCA Matcher opt - time (milliseconds)"))
                    using (var timePos = new HotAverage(stats, "DCA Matcher - time (milliseconds)"))
                    using (var lenPos = new HotAverage(stats, "Lenght string"))
                    //using (var valPosOpt2 = new Exact<int>(stats, "Positive-opt2-errors"))
                    using (var valPosOpt1 = new Exact<int>(stats, "Positive-opt-errors"))
                    using (var valPos = new Exact<int>(stats, "Positive-errors"))
                    using (var exception = new Message(stats, "Our exception"))
                    using (var dcaTimeouts = new Exact<int>(stats, "Timeouts-our"))
                    using (var dcaACounters = new Exact<long>(stats, "Counters-DCA-alg"))
                    using (var dcaCounters = new Exact<long>(stats, "Counters-DCA"))
                    using (var ncaCounters = new Exact<int>(stats, "Counters-NCA"))
                    using (var dcaATrans = new Exact<int>(stats, "DCA-trans-alg"))
                    //using (var dcaTransOpt2 = new Exact<int>(stats, "DCA-trans-opt2"))
                    using (var dcaTransOpt = new Exact<int>(stats, "DCA-trans-opt"))
                    using (var dcaTrans = new Exact<int>(stats, "DCA-trans"))
                    using (var ncaTrans = new Exact<int>(stats, "NCA-trans"))
                    using (var dcaASize = new Exact<int>(stats, "|DCA|-alg"))
                    //using (var dcaOpt2Size = new Exact<int>(stats, "|DCA|-opt2"))
                    using (var dcaOptSize = new Exact<int>(stats, "|DCA|-opt"))
                    using (var dcaSize = new Exact<int>(stats, "|DCA|"))
                    using (var ncaSize = new Exact<int>(stats, "|NCA|"))
                    using (var ncToDcaOpt = new HotAverage(stats, "NCA->DCA (milliseconds)-opt"))
                    using (var ncaToDcaA = new HotAverage(stats, "NCA->DCA (milliseconds)-alg"))
                    using (var ncaToDca = new HotAverage(stats, "NCA->DCA (milliseconds)"))
                    using (var srToNca = new HotAverage(stats, "SR->NCA (milliseconds)"))
                    using (var rgxToSR = new HotAverage(stats, "regex->SR (milliseconds)"))
                    {
                        try
                        {
                            CsAutomaton<ulong> dcaA = null;
                            CsAutomatonOpt<ulong> dca = null;
                            
                            for (int i = 0; i <= SAMPLES; ++i)
                            {
                                var task = Task.Run(() =>
                                {
                                    //---------------------------------------    
                                    // COUNTING SETS APPROACH
                                    //---------------------------------------
                                    sw.Restart();
                                    var sr = ((SymbolicRegex<ulong>)rgx.Compile(false, false)).Pattern;
                                    var rgxTime = sw.Elapsed.TotalMilliseconds;
                                    rgxToSR.Add(rgxTime);

                                    sw.Restart();
                                    var nca = sr.CreateCountingAutomaton(true);
                                    var ncaTime = sw.Elapsed.TotalMilliseconds;
                                    srToNca.Add(ncaTime);

                                    if (runMy)
                                    {
                                        sw.Restart();
                                        dca = nca.CompileOpt(1);
                                        var dcaTime = sw.Elapsed.TotalMilliseconds;
                                        ncaToDca.Add(dcaTime);

                                        // create statistic
                                        // counting sets approach
                                        ncaSize.Add(nca.StateCount);
                                        ncaTrans.Add(nca.CountTrans());
                                        ncaCounters.Add(nca.NrOfCounters);
                                        dcaSize.Add(dca.StateCount);
                                        dcaTrans.Add(dca.CountTrans());
                                        dcaCounters.Add(dca.NrOfCounters);
                                    }
                                   
                                    // show graphs
                                    // nca.ShowGraph("NCA");
                                    //dca.ShowGraph("DCA"); 
                                    //dcaA.ShowGraph("DetAut-algebra");

                                });
                                bool isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(TIMEOUT));

                                if (!isCompletedSuccessfully)
                                {
                                    throw new TimeoutException("The function has taken longer than the maximum time allowed.");
                                }
                            }
                            // run validation test
                            if (runValidate)
                            {
                                var listErrorPos = new List<string>();
                                var listErrorNeg = new List<string>();
                                var listErrorPosA = new List<string>();
                                var listErrorNegA = new List<string>();
                                var listErrorPos1 = new List<string>();
                                var listErrorPos2 = new List<string>();
                                var listErrorNeg1 = new List<string>();

                                foreach (var str in samples)
                                {
                                    sw.Restart();
                                    Assert.IsTrue(rgx.IsMatch(str));
                                    //var regTime = sw.Elapsed.TotalMilliseconds;
                                    //regPos.Add(regTime);
                                    if (runMy)
                                    {
                                        sw.Restart();
                                        if (!dca.Matches(str))
                                        {
                                            var posTime = sw.Elapsed.TotalMilliseconds;
                                            timePos.Add(posTime);
                                            listErrorPos.Add(str);
                                        }
                                        else
                                        {
                                            var posTime = sw.Elapsed.TotalMilliseconds;
                                            timePos.Add(posTime);
                                        }
                                        lenPos.Add(str.Length);
                                    }
                                   
                                    /* if (runAlgebra)
                                     {
                                         if (!dcaA.Matches(str))
                                             listErrorPosA.Add(str);
                                     } */
                                }
                                /* foreach (var str in negSamples)
                                 {
                                     Assert.IsFalse(rgx.IsMatch(str));
                                     if (runMy)
                                     {
                                         if (dca.Matches(str))
                                             listErrorNeg.Add(str);
                                     }
                                     if (runAlgebra)
                                     {
                                         if (dcaA.Matches(str))
                                             listErrorNegA.Add(str);
                                     }
                                 }*/
                                if (runMy)
                                {
                                    valPos.Add(listErrorPos.Count());
                                    //valNeg.Add(listErrorNeg.Count());
                                }
                                if (runOptimal1)
                                {
                                    valPosOpt1.Add(listErrorPos1.Count());
                                    //valPosOpt2.Add(listErrorPos2.Count());
                                    //valNegOpt.Add(listErrorNeg1.Count());
                                }

                                /*if (runAlgebra)
                                {
                                    valPosA.Add(listErrorPosA.Count());
                                    valNegA.Add(listErrorNegA.Count());
                                }*/
                            }
                        }
                        catch (Exception e)
                        {
                            if (e.InnerException != null)
                            {
                                exception.Add(e.InnerException.Message);
                                Console.WriteLine(e.InnerException.Message);
                            }
                            else // timeout
                            {
                                exception.Add(e.Message);
                                Console.WriteLine(e.Message);
                                dcaTimeouts.Add(1);
                            }
                        }
                    }

                }
            }

            // Classical pipeline
            if (runClassical)
            {
                Console.WriteLine("Classical pipeline");
                var solver = new CharSetSolver();
                ///var regexConverter = new RegexToAutomatonConverter<BDD>(solver);

                int count = 0;
                foreach (var regex in regexes)
                {
                    ++count;
                    Console.WriteLine($"{count}/{regexes.Length}");
                    Stopwatch sw = new Stopwatch();

                    using (var exception = new Message(stats, "Classical exception"))
                    using (var timeouts = new Exact<int>(stats, "Timeouts-classical"))
                    using (var minTrans = new Exact<int>(stats, "MIN-trans"))
                    using (var dfaTrans = new Exact<int>(stats, "DFA-trans"))
                    using (var nfaTrans = new Exact<int>(stats, "NFA-trans"))
                    using (var minSize = new Exact<int>(stats, "|MIN|"))
                    using (var dfaSize = new Exact<int>(stats, "|DFA|"))
                    using (var nfaSize = new Exact<int>(stats, "|NFA|"))
                    using (var dfaToMin = new HotAverage(stats, "DFA->min (milliseconds)"))
                    using (var nfaToDfa = new HotAverage(stats, "NFA->DFA (milliseconds)"))
                    using (var regexToNfa = new HotAverage(stats, "regex->NFA (milliseconds)"))
                    {
                        try
                        {
                            for (int i = 0; i <= SAMPLES; ++i)
                            {
                                var task = Task.Run(() =>
                                {
                                    sw.Restart();
                                    //var nfa = regexConverter.Convert(regex);
                                    var nfa = solver.Convert(regex);
                                    var timeRegex = sw.Elapsed.TotalMilliseconds;
                                    regexToNfa.Add(timeRegex);

                                    sw.Restart();
                                    var dfa = nfa.Determinize();
                                    var timeNFA = sw.Elapsed.TotalMilliseconds;
                                    nfaToDfa.Add(timeNFA);
                                    //dfa.ShowGraph("DFA");

                                    sw.Restart();
                                    var min = dfa.Minimize();
                                    var timeDFA = sw.Elapsed.TotalMilliseconds;
                                    dfaToMin.Add(timeDFA);
                                    //min.ShowGraph("CA");

                                    nfaSize.Add(nfa.StateCount);
                                    nfaTrans.Add(nfa.CountTrans());
                                    dfaSize.Add(dfa.StateCount);
                                    dfaTrans.Add(dfa.CountTrans());

                                    minSize.Add(min.StateCount);
                                    minTrans.Add(min.CountTrans());

                                });
                                bool isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(TIMEOUT));

                                if (!isCompletedSuccessfully)
                                {
                                    throw new TimeoutException("The function has taken longer than the maximum time allowed.");
                                }

                            }
                        }
                        catch (Exception e)
                        {
                            exception.Add(e.Message);
                            Console.WriteLine(e.Message);
                            timeouts.Add(1);
                        }
                    }
                }
            }

            // stats postprocessing
            return stats;
        }

        static string[] DecodeRegexes(string[] regexes)
        {
            //return regexes;
            var decoded = new List<string>();
            foreach (var regex in regexes)
            {
                string str = Decode(regex);
                decoded.Add(str);
            }
            return decoded.ToArray();
        }

        // Replace curly brackets and double backslashes
        static string Decode(string input)
        {
            var output = new System.Text.StringBuilder();
            int r0 = 0; int r1 = 0; int r2 = 0; int r3 = 0; int r4 = 0; int r5 = 0; int state = 0;
            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int c = (int)chars[i];
                switch (state)
                {
                    case (0):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append((char)c);
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (1):
                        {
                            switch (c)
                            {
                                case (0x78):
                                    {
                                        state = 2;
                                        break;
                                    }
                                case (0x75):
                                    {
                                        state = 3;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append((char)0x5C);
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (3):
                        {
                            switch (c)
                            {
                                case (0x7B):
                                    {
                                        state = 4;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (4):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r0 = c;
                                            state = 5;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (5):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x30, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r1 = c;
                                            state = 6;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (6):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r2 = c;
                                            state = 7;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (7):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r3 = c;
                                            state = 8;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (8):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r4 = c;
                                            state = 9;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (9):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r5 = c;
                                            state = 10;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (10):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (2):
                        {
                            switch (c)
                            {
                                case (0x7B):
                                    {
                                        state = 11;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (11):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r0 = c;
                                            state = 12;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (12):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x30, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r1 = c;
                                            state = 13;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (13):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r2 = c;
                                            state = 14;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (14):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r3 = c;
                                            state = 15;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (15):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r4 = c;
                                            state = 16;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    case (16):
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r5 = c;
                                            state = 17;
                                        }
                                        else
                                        {
                                            output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)c });
                                            r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                            state = 0;
                                        }
                                        break;
                                    }
                            }
                            break;
                        }
                    default:
                        {
                            switch (c)
                            {
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x7D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                }
            }
            switch (state)
            {
                case (0):
                    {
                        break;
                    }
                case (1):
                    {
                        output.Append((char)0x5C);
                        break;
                    }
                case (3):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75 });
                        break;
                    }
                case (4):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B });
                        break;
                    }
                case (5):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0 });
                        break;
                    }
                case (6):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1 });
                        break;
                    }
                case (7):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2 });
                        break;
                    }
                case (8):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3 });
                        break;
                    }
                case (9):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                        break;
                    }
                case (10):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                        break;
                    }
                case (2):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78 });
                        break;
                    }
                case (11):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B });
                        break;
                    }
                case (12):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0 });
                        break;
                    }
                case (13):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1 });
                        break;
                    }
                case (14):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2 });
                        break;
                    }
                case (15):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3 });
                        break;
                    }
                case (16):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                        break;
                    }
                default:
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                        break;
                    }
            }
            return Escape(output.ToString().Select(c => c).ToList());
        }

        public static string Escape(List<char> input)
        {
            List<char> output = new List<char>();
            for (int i = 0; i < input.Count(); i++)
            {
                if (i > 0)
                {
                    if (input[i - 1] == '\\' && input[i] == '\\')
                        continue;
                    output.Add(input[i]);
                }
                else
                    output.Add(input[i]);
            }
            return string.Join("", output.ToArray());
        }


        static string[] FilterRegexes(string[] regexes, out List<string> notSupported, out List<string> nonMonadic, out List<string> noCounters, Mode mode)
        {
            Predicate<SymbolicRegexNode<ulong>> isNonMonadic = node =>
            {
                return (node.Kind == SymbolicRegexKind.Loop &&
                        node.Left.Kind != SymbolicRegexKind.Singleton &&
                        !node.IsStar &&
                        !node.IsMaybe &&
                        !node.IsPlus);
            };

            Predicate<SymbolicRegexNode<ulong>> isCountingLoop = node =>
            {
                return (node.Kind == SymbolicRegexKind.Loop &&
                        !node.IsStar &&
                        !node.IsMaybe &&
                        !node.IsPlus);
            };

            var filtered = new List<string>();
            notSupported = new List<string>();
            nonMonadic = new List<string>();
            noCounters = new List<string>();
            var i = 0;

            foreach (var rgx in regexes)
            {
                string reasonwhynot = "";
                try
                {
                    // decode regular expression (remove double blackslash and remove curly brackets)
                    string regex = rgx;
                    if((mode & Mode.DECODE) == Mode.DECODE)
                    {
                        regex = Decode(rgx);
                    }
                    i++;
                    var sr = new Regex(regex, RegexOptions.Singleline);
                    SymbolicRegexUInt64 m = null;
                    if (sr.IsCompileSupported(out reasonwhynot))
                    {
                        m = sr.Compile(true, false) as SymbolicRegexUInt64;
                        if (!m.Pattern.ExistsNode(isCountingLoop))
                        {
                            noCounters.Add(rgx);    // store original string
                        }
                        else if (m.Pattern.ExistsNode(isNonMonadic))
                        {
                            nonMonadic.Add(rgx);    // store original string
                        }
                        else
                            filtered.Add(regex);    // store decoded string
                    }
                    else
                        notSupported.Add(rgx + "\t" + reasonwhynot);
                }
                catch (Exception e)
                {
                    notSupported.Add(rgx + "\t" + e.Message);
                }
            }
            return filtered.ToArray();
        }



        static void BenchmarkFile(string[] args, Version version, Mode mode)
        {
            string path = args[0];
            var regexes = ReadRegexes(path).ToArray();
            List<string> notSupported;
            List<string> nonMonadic;
            List<string> noCounters;

            
            var filtered = FilterRegexes(regexes, out notSupported, out nonMonadic, out noCounters, mode);
            var filenameNoExtension = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine("Benchmarking from " + path);

            if (filtered.Length < regexes.Length)
            {
                Console.WriteLine($"{filtered.Length}/{regexes.Length} regexes passed filter.");
                Console.WriteLine($"{notSupported.Count()}/{regexes.Length} regexes are not supported.");
                Console.WriteLine($"{noCounters.Count()}/{regexes.Length} regexes are without counters.");
                Console.WriteLine($"{nonMonadic.Count()}/{regexes.Length} regexes are non-monadic.");

                var filteredPath = $"{filenameNoExtension}-filtered.txt";
                Console.WriteLine($"Writing filtered set to {Path.GetFullPath(filteredPath)}");
                WriteRegexes(filteredPath, filtered);

                filteredPath = $"{filenameNoExtension}-notSupported.txt";
                filteredPath = $"{filenameNoExtension}-notSupported.xls";

                Console.WriteLine($"Writing filtered set to {Path.GetFullPath(filteredPath)}");
                WriteRegexes(filteredPath, notSupported.ToArray());

                filteredPath = $"{filenameNoExtension}-noCounters.txt";
                Console.WriteLine($"Writing filtered set to {Path.GetFullPath(filteredPath)}");
                WriteRegexes(filteredPath, noCounters.ToArray());

                filteredPath = $"{filenameNoExtension}-nonMonadic.txt";
                Console.WriteLine($"Writing filtered set to {Path.GetFullPath(filteredPath)}");
                WriteRegexes(filteredPath, nonMonadic.ToArray());
            }
            else
            {
                Console.WriteLine("All regexes passed filter.");
            }


            if (benchmark_mode)
            {
                var stats = Benchmark(filtered, runOur, runClassical, runValidate, runTest, runMy, runAlgebra, runOptimal1);
                //var outPath = $"{Path.GetFileNameWithoutExtension(path)}-results-{Guid.NewGuid()}.txt";
                //stats.WriteTSV(outPath);

                var outPath = $"{Path.GetFileNameWithoutExtension(path)}-results-{Guid.NewGuid()}.xls";
                stats.WriteTSV(outPath);

                Console.WriteLine("Wrote results to " + Path.GetFullPath(outPath));
            }
            var fileName = $"./README-" + filenameNoExtension;
            // Check if file already exists. If yes, delete it.     
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            // Create a new file     
            using (StreamWriter sw = File.CreateText(fileName))
            {
                string output = "All benchmarks were run with the following settings:\nTIMEOUT: " + (TIMEOUT / 1000) + " minute\n" + SAMPLES + " times for each regex\n\n";
                output += "Filtered first " + regexes.Length + " regexes. \n\nThe filter is set such that it filteres first regular, than from the correct\nregexes with counters and at the end monadic from the rest.\n\n";
                output += filenameNoExtension + "-filtered - passed regular, monadic with counter\n";
                output += filenameNoExtension + "-notSupported - non regular or such that we do not handle them\n";
                output += filenameNoExtension + "-nonMonadic - non monadic\n";
                output += filenameNoExtension + "-noCounters - no counters\n\n";
                output += filtered.Count() + " passed\n";
                output += notSupported.Count() + " not suppported\n";
                output += noCounters.Count() + " no counters\n";
                output += nonMonadic.Count() + " non monadic\n\n";
                output += (nonMonadic.Count() + noCounters.Count() + notSupported.Count() + filtered.Count()) + " in total.\n\n";
                sw.WriteLine(output);
                sw.WriteLine();
            }


        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: CounterAutomataBench.exe <regexes-file1> [<regexes-file2> ...]");
                System.Environment.Exit(1);
            }
            var version = Version.CHIPMUNK;
            if (args[0] == "--margus")
                version = Version.MARGUS;
            else if (args[0] == "--chipmunk")
                version = Version.CHIPMUNK;
            else if (args[0] == "--regex")
                version = Version.REGEX;
            else if (args[0] == "--all")
                version = Version.ALL;
            else
            {
                Console.Error.WriteLine("Usage: CounterTestBenchmarks.exe --[margus|chipmunk|regex|all] --[test] --[validate] --[decode] <regex> [<matching-file>]");
                System.Environment.Exit(1);
            }
            var mode = Mode.NOOP;
            if(args.Contains("--test"))
            {
                mode |= Mode.TEST;
            }
            if (args.Contains("--validate"))
            {
                mode |= Mode.VALIDATE;
            }
           /* if (args.Contains("--filter"))
            {
                mode |= Mode.FILTER;
            }*/
            if (args.Contains("--decode"))
            {
                mode |= Mode.DECODE;
            }
            BenchmarkFile(args, version, mode);

            Console.ReadLine();
        }
    }
}
