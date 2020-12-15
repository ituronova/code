using Microsoft.Automata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace CounterAutomataBench
{
    public enum Version
    {
        /// <summary>
        /// Margus version of the algorithm
        /// </summary>
        MARGUS = 0,
        /// <summary>
        /// Our version of the algorithm
        /// </summary>
        CHIPMUNK = 1,
        /// <summary>
        /// Both our and Margus version of the algorithm
        /// </summary>
        BOTH = 2,
        /// <summary>
        /// .NET regex
        /// </summary>
        REGEX = 3,
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
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            if (File.Exists(path))
            {
                File.Delete(path);
            }
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
        const int SAMPLES = 1;
        const int TIMEOUT = 60000;   // 1 min
        const int MAXSAMPLES = 1;   // number of samples for validating the automata

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

        static string[] ReadText(string path)
        {
            return System.IO.File.ReadAllLines(path);
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


        // Replace curly brackets and double backslashes
        static string Decode(string input)
        {
            string pattern = @"((\{)([0-9]*)([0-9]{4})(\}))";
            input = Regex.Replace(input, pattern, "$4");

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
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x30, (char)0x30, (char)0x30, (char)r0 });
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
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x30, (char)0x30, (char)r0, (char)r1 });
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
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)0x30, (char)r0, (char)r1, (char)r2 });
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
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r1, (char)r2, (char)r3, (char)r4 });
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
                                        output.Append(new char[] { (char)0x5C, (char)0x75, (char)r2, (char)r3, (char)r4, (char)r5 });
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
                                case (0x5B):
                                    {
                                        state = 11;
                                        break;
                                    }
                                case (0x7B):
                                    {
                                        state = 12;
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
                    case (12):
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
                                            state = 13;
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
                    case (13):
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x30, (char)0x30, (char)0x30, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r1 = c;
                                            state = 14;
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
                    case (14):
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x30, (char)0x30, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r2 = c;
                                            state = 15;
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
                    case (15):
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x30, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r3 = c;
                                            state = 16;
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
                    case (16):
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
                                            state = 17;
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
                    case (17):
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r1, (char)r2, (char)r3, (char)r4 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        if ((((0x30 <= c) && (((c >> 6) & 0x3FF) == 0) && ((c & 0x3F) <= 0x39)) || ((0x41 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x46)) || ((0x61 <= c) && (((c >> 7) & 0x1FF) == 0) && ((c & 0x7F) <= 0x66))))
                                        {
                                            r5 = c;
                                            state = 18;
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
                    case (18):
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)r2, (char)r3, (char)r4, (char)r5 });
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
                    case (11):
                        {
                            switch (c)
                            {
                                case (0x30):
                                    {
                                        r0 = 0x30;
                                        state = 19;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (19):
                        {
                            switch (c)
                            {
                                case (0x2D):
                                    {
                                        r1 = 0x2D;
                                        state = 20;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (20):
                        {
                            switch (c)
                            {
                                case (0x39):
                                    {
                                        r2 = 0x39;
                                        state = 21;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (21):
                        {
                            switch (c)
                            {
                                case (0x41):
                                    {
                                        r3 = 0x41;
                                        state = 22;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (22):
                        {
                            switch (c)
                            {
                                case (0x2D):
                                    {
                                        r4 = 0x2D;
                                        state = 23;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (23):
                        {
                            switch (c)
                            {
                                case (0x46):
                                    {
                                        r5 = 0x46;
                                        state = 24;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (24):
                        {
                            switch (c)
                            {
                                case (0x61):
                                    {
                                        state = 25;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (25):
                        {
                            switch (c)
                            {
                                case (0x2D):
                                    {
                                        state = 26;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                            }
                            break;
                        }
                    case (26):
                        {
                            switch (c)
                            {
                                case (0x66):
                                    {
                                        state = 27;
                                        break;
                                    }
                                case (0x5C):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D, (char)c });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
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
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D, (char)0x66 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 1;
                                        break;
                                    }
                                case (0x5D):
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x30, (char)0x30, (char)0x30, (char)r0 });
                                        r0 = 0; r1 = 0; r2 = 0; r3 = 0; r4 = 0; r5 = 0;
                                        state = 0;
                                        break;
                                    }
                                default:
                                    {
                                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D, (char)0x66, (char)c });
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
                case (12):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B });
                        break;
                    }
                case (13):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0 });
                        break;
                    }
                case (14):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1 });
                        break;
                    }
                case (15):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2 });
                        break;
                    }
                case (16):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3 });
                        break;
                    }
                case (17):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                        break;
                    }
                case (18):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x7B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                        break;
                    }
                case (11):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B });
                        break;
                    }
                case (19):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0 });
                        break;
                    }
                case (20):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1 });
                        break;
                    }
                case (21):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2 });
                        break;
                    }
                case (22):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3 });
                        break;
                    }
                case (23):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4 });
                        break;
                    }
                case (24):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5 });
                        break;
                    }
                case (25):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61 });
                        break;
                    }
                case (26):
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D });
                        break;
                    }
                default:
                    {
                        output.Append(new char[] { (char)0x5C, (char)0x78, (char)0x5B, (char)r0, (char)r1, (char)r2, (char)r3, (char)r4, (char)r5, (char)0x61, (char)0x2D, (char)0x66 });
                        break;
                    }
            }

            string str = output.ToString();
            try
            {
                str = ReplaceSubstrings(Regex.Unescape(str));
            }
            catch
            {
                //Console.WriteLine("Error in substring replacement.");
                return ReplaceSubstrings(str);
            }
            return ReplaceSubstrings(str);
        }

        static string ReplaceSubstrings(string str)
        {
            str = str.Replace("\\Q", "q");
            str = str.Replace("\\E", "e");
            str = str.Replace("\\G", "g");
            str = str.Replace("\\A", "^");
            str = str.Replace("\\Z", "$");
            str = str.Replace("\\z", "$");
            str = str.Replace("\\K", "K");
            str = str.Replace("\\k", "k");
            str = str.Replace("\\b", "b");
            str = str.Replace("\\B", "b");
            str = str.Replace("\\N", "n");
            str = str.Replace("\\e", "e");
            str = str.Replace("\\cc", "c");
            str = str.Replace("\\cC", "c");
            str = str.Replace("\\s", "s");
            str = str.Replace("\\_", ".*");
            str = str.Replace("\\g", "escapeG");
            str = Regex.Replace(str, "escapeG\\<[+-]*[0-9a-zA-Z]+\\>", "g");
            str = Regex.Replace(str, "escapeG\\{[+-]*[0-9a-zA-Z]+\\}", "g");
            str = Regex.Replace(str, "escapeG[0-9a-zA-Z]+", " g");
            str = str.Replace("\\p", "escapeP");
            str = Regex.Replace(str, "escapeP\\{[+-]*[0-9a-zA-Z]+\\}", "p");
            str = Regex.Replace(str, "escapeP[a-zA-Z]", " p");
            str = str.Replace("[:alpha:]", "[A-Za-z]");
            str = str.Replace("[:upper:]", "[A-Z]");
            str = str.Replace("[:lower:]", "[a-z]");
            str = str.Replace("[:digit:]", "[0-9]");
            str = str.Replace("[:alnum:]", "[A-Za-z0-9]");
            str = str.Replace("[:xdigit:]", "[A-Fa-f0-9]");
            str = str.Replace("[:space:]", "[ \t\r\n\v\f]");
            str = str.Replace("[:black:]", "[ \t]");
            str = str.Replace("[:print:]", "[\x20-\x7E]");
            str = str.Replace("[:punct:]", "[\\p{P}]");
            str = str.Replace("[:graph:]", "[\x21-\x7E]");
            str = str.Replace("[:word:]", "[A-Za-z0-9_]");
            str = str.Replace("[:ascii:]", "[\x00-\x7F]");
            str = str.Replace("[:cntrl:]", "[\x00-\x1F\x7F]");
            str = str.Replace("\\h", "h");
            str = str.Replace("\\H", "H");
            str = str.Replace("\\R", "R");
            str = str.Replace("\\d", "d");
            str = str.Replace("\\V", "V");
            str = str.Replace("\\C", "C");
            str = str.Replace("\\T", "T");
            str = str.Replace("\\i", "i");
            str = str.Replace("\\x[", "x[");
            str = str.Replace("\\u[", "u[");
            str = str.Replace("|*", "|.*");
            str = str.Replace("+?", "+");
            str = str.Replace("?+", "+");
            str = str.Replace("??", "?");
            str = str.Replace("?*", "*");
            str = str.Replace("++", "+");
            str = str.Replace("}?", "}");
            str = str.Replace("}+", "}");
            str = str.Replace("}*", "}");
            str = str.Replace("+{", "{");
            str = str.Replace("*{", "{");
            str = str.Replace("?{", "{");
            str = str.Replace("*?", "*");
            str = str.Replace("+*", "+");
            str = str.Replace("*+", "*");
            str = str.Replace("|?", "|.*");
            str = str.Replace("{ ", "{");
            str = str.Replace(" }", "}");
            str = str.Replace("{}", ".*");
            str = str.Substring(0, str.Length - 1).Replace("$", ".*") + str.Substring(str.Length - 1);

            //string pattern = @"(\(\?[\>\=\!]*)(.*?)(\))";
            //str = Regex.Replace(str, pattern, "$2");

            // (?xus)"  (?<=a(?=([bc]{2}(?<!a{2}))d)\\w{3})\\w\\w

            string pattern = @"(\(\?([^\)]+)\))";
            string oldStr = "";
            while (oldStr != str)
            {
                oldStr = str;
                str = Regex.Replace(str, pattern, "($2)");
            }
            str = str.Replace("(?)", "(.*)");

            // *compute tau *
            pattern = @"((\*)([^\*]*)(\*))";
            str = Regex.Replace(str, pattern, "$3");

            // {HAN}
            pattern = @"((\{)([^\}[0-9]]*)(\}))";
            str = Regex.Replace(str, pattern, "$3");

            // {1}{2}
            pattern = @"((\{[0-9,]+\})(\{[0-9,]+\}))*";
            str = Regex.Replace(str, pattern, "$2");

            // ?(3)
            str = Regex.Replace(str, "(\\?\\()([0-9]?)(\\))", ".*");

            // \1
            pattern = @"(\\)([0-9]+)";
            str = Regex.Replace(str, pattern, "$2");

            return str;
        }

        bool IsMonadic(string regex)
        {
            Predicate<SymbolicRegexNode<ulong>> isNonMonadic = node =>
            {
                return (node.Kind == SymbolicRegexKind.Loop &&
                        node.Left.Kind != SymbolicRegexKind.Singleton &&
                        !node.IsStar &&
                        !node.IsMaybe &&
                        !node.IsPlus);
            };

            var sr = new Regex(regex, RegexOptions.Singleline);
            SymbolicRegexUInt64 m = null;
            m = sr.Compile(true, false) as SymbolicRegexUInt64;
            return (!m.Pattern.ExistsNode(isNonMonadic));
        }

        static string[] FilterRegexes(string[] regexes, out List<string> notSupported, out List<string> nonMonadic, out List<string> noCounters, out List<string> simpleStrings, out List<string> nestedCounters, out List<string> notnestedCounters, bool banchmark_mode = false, bool filter_mode = true, bool decode_mode = false)
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

            Predicate<SymbolicRegexNode<ulong>> isNestedCounter = node =>
            {
                return (node.Kind == SymbolicRegexKind.Loop && node.Left.ExistsNode(isCountingLoop));
            };


            var filtered = new List<string>();
            notSupported = new List<string>();
            nonMonadic = new List<string>();
            noCounters = new List<string>();
            simpleStrings = new List<string>();
            nestedCounters = new List<string>();
            notnestedCounters = new List<string>();
            var i = 0;


            foreach (var str in regexes)
            {
                string reasonwhynot = "";
                string rgx = "";
                if (decode_mode)
                    rgx = str.Replace("\\n", "n");
                else
                    rgx = str;
                try
                {
                    // decode regular expression (remove double blackslash and remove curly brackets)
                    string regex = "";
                    if (decode_mode)
                        regex = Decode(rgx);
                    else
                        regex = rgx;
                    i++;
                    if (filter_mode)
                    {
                        //Console.WriteLine("Filtering...");
                        Console.WriteLine(regex);
                        Console.Write(i + "\n");

                        var sr = new Regex(regex, RegexOptions.Singleline);
                        SymbolicRegexUInt64 m = null;
                        if (sr.IsCompileSupported(out reasonwhynot))
                        {
                            try
                            {
                                m = sr.Compile(true, false) as SymbolicRegexUInt64;
                            }
                            catch (Exception e)
                            {
                                notSupported.Add(rgx + "\t" + e.Message);
                                continue;
                            }
                            if (!m.Pattern.ExistsNode(isCountingLoop))
                            {
                                noCounters.Add(rgx);    // store original string
                            }
                            else if (m.Pattern.ExistsNode(isNonMonadic))
                            {
                                nonMonadic.Add(rgx);    // store original string
                                if (m.Pattern.ExistsNode(isNestedCounter))
                                {
                                    nestedCounters.Add(rgx);
                                }
                                else
                                    notnestedCounters.Add(rgx);
                            }
                            else
                                filtered.Add(regex);    // store decoded string
                        }
                        else
                            notSupported.Add(rgx + "\t" + reasonwhynot);
                    }
                    else
                        filtered.Add(regex);    // store decoded string     
                }
                catch (Exception e)
                {
                    // TODO: how to indicate single strings?
                    if (e.Source == "CounterAutomataBench" && e.Message == "Odkaz na objekt není nastaven na instanci objektu.")
                        simpleStrings.Add(rgx);
                    else
                        notSupported.Add(rgx + "\t" + e.Message);
                    //Console.WriteLine("Error in filtering.");
                }
            }
            return filtered.ToArray();
        }


   

        static int IsMonadicExpr(string str)
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

            Predicate<SymbolicRegexNode<ulong>> isNestedCounter = node =>
            {
                return (node.Kind == SymbolicRegexKind.Loop && node.Left.ExistsNode(isCountingLoop));
            };
            try
            {
                // decode regular expression (remove double blackslash and remove curly brackets)
                string regex = "";
                var sr = new Regex(regex, RegexOptions.Singleline);
                SymbolicRegexUInt64 m = null;
                string error;
                if (sr.IsCompileSupported(out error))
                {
                    try
                    {
                        m = sr.Compile(false, false) as SymbolicRegexUInt64;
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine("Error in compilation.");
                        return -1;
                    }
                    if (m.Pattern.ExistsNode(isNonMonadic))
                    {
                        if (m.Pattern.ExistsNode(isNestedCounter))
                        {
                            return 0;
                        }
                        else
                            return 0;
                    }
                }
                else
                {
                    //Console.WriteLine("Expression is not supported.");
                    return -1;
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Expression is not supported.");
                return -1;
            }
            return 1;
        }



        //static void BenchmarkFile(string path, bool benchmark_mode, bool filter_mode, bool decode_mode, bool runOur, bool runClassical, bool runValidate, bool runMy, bool runAlgebra, bool runOptimal)
        /*  static void Main(string[] args)
          {
              int count = 0;
              Regex rgx = new Regex(args[1], RegexOptions.Singleline);
              var pattern = ((SymbolicRegex<ulong>)rgx.Compile(false, false));
              var nca = pattern.Pattern.CreateCountingAutomatonPushIncr(false);
              var dca = nca.CompileOpt(1);
              //var matcher = new CSMatcher();           

              // init counting sets
              if (dca.cntCount != 0)
              {   // initialize counting sets
                  dca.cs = new BasicCountingSet[dca.cntCount];
                  for (int j = 0; j < dca.cntCount; j++)
                  {
                      dca.cs[j] = new BasicCountingSet(dca.counters[j]);    // initialization to 0   
                  }
              }
              Stopwatch sw = new Stopwatch();
              sw.Restart();
              using (StreamReader sr = File.OpenText(args[2]))
               {
                   string s = String.Empty;
                   while ((s = sr.ReadLine()) != null)
                   {
                       if (pattern.IsMatchCA(s, dca))
                       {
                           count++;
                       }
                  }
               }
              Console.Write(sw.ElapsedMilliseconds + " ms");

              //Console.WriteLine("Time: 0");
              //Console.WriteLine("Matching lines: " + count);
              Console.Read();
          }*/

        private static int CompareCycle(Tuple<List<int>, List<int>, double> c1, Tuple<List<int>, List<int>, double> c2)
        {
            if (c1.Item3 < c2.Item3)
                return 1;
            if (c1.Item3 == c2.Item3)
                return 0;
            return -1;
        }


        static bool ReadLine(string line, out string s)
        {
            var values = line.Split(';');
            s = "";
            if (values.Length < 4)
                return false;
            int value = 0;
            int v = 1;
            s = values[0];
            while (v < values.Length)
            {
                if (values[v] == string.Empty)
                {
                    return false;
                }
                if (!Int32.TryParse(values[v], out value))
                {
                    s += ";" + values[v];
                }
                else
                {
                    if (Int32.TryParse(values[v], out value))
                    {
                        if (value >= 200)
                            return true;
                        else
                            return false;
                    }
                    return false;
                }
                v++;
            }
            return true;
        }

        static int GetFlagOfLine(string line, out string s, int bound)
        {
            char[] charSeparators = new char[] { ';' };
            Microsoft.Automata.DirectedGraphs.Options.MaxDgmlTransitionLabelLength = 1000000000;
            var values = line.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
            s = "";
            if (values.Length < 2)
                return -1;
            var error = values[values.Length - 1];

            if (error.Contains("TIMEOUT"))
            {
                return 1;
            }
            else if (error.Contains("Error"))
            {
                return 2;
            }
            int v = 1;
            int value = 0;
            var cnt = values[values.Length - 2];
            if (Int32.TryParse(cnt, out value))
            {
                if (value > 0)
                {
                    if (value >= bound)
                    {
                        s = values[0];
                        while (v < values.Length - 2)
                        {
                            s += ";" + values[v];
                            v++;
                        }
                        s += "\n";
                    }
                    return 0;
                }
                return -1;
            }
            return -1;


        }

        static long LongRandom(long min, long max, Random rand)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return (Math.Abs(longRand % (max - min)) + min);
        }

       
        static void Main(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            //Stopwatch s1 = new Stopwatch();

            //bool graphs = true;
            bool graphs = false;
            if (args.Length < 6)
            {
                //Console.WriteLine("\n---------------------------  HELP --------------------------\n");
                //Console.WriteLine("Usage: ./CounterAutomataBench [inputFile] [outputFolder] [bound] [TIMEOUT] [-v/-nv] [-hybrid/-nonhybrid]\n");
                //Console.WriteLine("\n-------------------------------------------------------------\n");
                Console.Read();
                return;
            }
            string inputFile = args[0];
            string outputFolder = args[1] ;
            int bound = Int32.Parse(args[2]);
            int TIMEOUT = Int32.Parse(args[3]);
            bool verbose = false;
            bool cntE = false;
            bool hybrid = false;
            int done = 0;
            int timeouted = 0;
            int error = 0;
            
            if (args[5].Contains("-hybrid"))
            {
                hybrid = true;
            }

            if (args[4].Contains("-v"))
            {
                verbose = true; 
                //Console.WriteLine("\n---------------------------  HELP --------------------------\n");
                //Console.WriteLine("Usage: ./CounterAutomataBench [inputFile] [outputFolder] [bound] [TIMEOUT]  [-v/-nv] [-hybrid/-nhybrid]\n");
                //Console.WriteLine("Input file:\t" + inputFile);
                //Console.WriteLine("Output folder:\t" + outputFolder);
                //Console.WriteLine("Bound for visited:\t" + bound);
                //Console.WriteLine("TIMEOUT:\t" + TIMEOUT); 
                //Console.WriteLine("Verbose:\ttrue");
                //Console.WriteLine("Hybrid:\t\ttrue");
                //Console.WriteLine("\n-------------------------------------------------------------\n");
            }
            System.IO.Directory.CreateDirectory("./results/results-" + DateTime.Now.ToString("yyyy-MM-dd") + "/");
            System.IO.Directory.CreateDirectory(outputFolder);

            string output = "./results/results-" + DateTime.Now.ToString("yyyy-MM-dd") + "/";
            string read = "Input file:\t" + inputFile + "\n";
            read += "Output folder:\t" + outputFolder + "\n";
            read += "Bound for visited:\t" + bound + "\n";
            read += "TIMEOUT:\t" + TIMEOUT + "\n";

            string name = Path.GetFileNameWithoutExtension(inputFile);
            string timeFile = output+ name + "-timeout.txt";
            string errorFile = output + name + "-error.csv";
            string errorRegex = output + name + "-error.txt";
            string readme = output + "README.txt";
            File.AppendAllText(errorFile, "Pattern;Message;\n");
            File.AppendAllText(readme, read);

            string ofAna = output+ "/analysis.csv";
            string outputFile = output + "/genText-" + Path.GetFileNameWithoutExtension(inputFile) + "-" + LongRandom(100000, 100050, new Random()) + ".csv";
            string outputRegex = outputFolder + "/" + Path.GetFileNameWithoutExtension(inputFile);
            File.AppendAllText(outputFile, "Regex;Lines;Visited;Cache Size(Found);Counters;Max Counter;Sum Counters;Max CS;Avrg CS;DCA-states;DCA-trans;Deter-TIME [milisec];Status\n");

            int index = 0;
            string n = ""; 
            // read input parameters to variables
            CsAutomatonOpt<ulong> dca = null;
            long time = 0;
            int count = 0;
            //s1.Reset();
            using (StreamReader sr = File.OpenText(inputFile))
            {
                string s = String.Empty;
                dca = null;
                Regex rgx = null;
                SymbolicRegex<ulong> sRegex = null;
                CountingAutomaton<ulong> nca = null;
                while ((s = sr.ReadLine()) != null)
                {
                    index++;
                    n = outputRegex + "_" + index + ".txt";
                    if (s == string.Empty)
                        return;
                    count++;
                    cntE = false;
                    dca = null;
                    rgx = null;
                    sRegex = null;
                    nca = null;
                    // convert regex to DCA
                    Console.WriteLine(s);
                    sw.Restart();

                    rgx = new Regex(s, RegexOptions.Singleline);

                    sRegex = ((SymbolicRegex<ulong>)rgx.Compile(false, false));

                    nca = sRegex.Pattern.CreateCountingAutomatonPushIncr(false);
                    dca = nca.CompileOpt(1, true, true);
                     nca.ShowGraph("nca", false);
                    //nca.ShowGraph("nca-f", false);
                    //dca.ShowGraph("dca", true);
                   // dca.ShowGraph("", false);
                   dca.ShowGraph("", true);                   
                }
            }
                          
                                
            Console.Read();
        }
    }
}
