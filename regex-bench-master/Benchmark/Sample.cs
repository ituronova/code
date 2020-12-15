using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    class Sample
    {
        public List<TimeSpan> Observations { get; } = new List<TimeSpan>();
        public TimeSpan Mean => new TimeSpan((long)Math.Round(Observations.Average(x => x.Ticks)));
        public TimeSpan StDev
        {
            get
            {
                var mean = Mean.Ticks;
                var sumSqDiffAvg = Observations.Select(x => (x.Ticks - mean) * (x.Ticks - mean)).Average();
                return new TimeSpan((long)Math.Round(Math.Sqrt(sumSqDiffAvg)));
            }
        }
    }
}
