using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
    static class CsvUtils
    {
        public static void ToCsv(this Benchmark benchmark, TextWriter ow)
        {
            foreach (var metric in benchmark.Metrics)
            {
                foreach (var dt in MetricToDataTables(metric))
                {
                    ow.WriteLine(dt.TableName);
                    foreach (var col in dt.Columns)
                    {
                        var dcol = (DataColumn)col;
                        ow.Write($"{dcol.ColumnName},");
                    }
                    ow.WriteLine();
                    foreach (var row in dt.Rows)
                    {
                        var drow = (DataRow)row;
                        foreach (var item in drow.ItemArray)
                        {
                            ow.Write($"{item},");
                        }
                        ow.WriteLine();
                    }
                    ow.WriteLine();
                }
            }
        }

        private static IEnumerable<DataTable> MetricToDataTables(this Metric metric)
        {
            if (metric.Axes.Length == 2)
            {
                yield return AxesToDataTable(metric, metric.Name, 0, 1, (c1, c2) => new string[] { c1, c2 });
            }
            if (metric.Axes.Length == 3)
            {
                foreach (var c in metric.Classes[0])
                {
                    yield return AxesToDataTable(metric, $"{metric.Name} ({c})", 1, 2, (c1, c2) => new string[] { c, c1, c2 });
                }
            }
        }

        private static DataTable AxesToDataTable(Metric metric, string name, int axis1, int axis2, Func<string, string, string[]> getIndex)
        {
            Func<string, string> getMeanName = s => s + " (Mean)";
            Func<string, string> getStDevName = s => s + " (StDev)";

            var dt = new DataTable(name);
            dt.Columns.Add(new DataColumn(metric.Axes[axis1]));
            foreach (var c in metric.Classes[axis2])
            {
                dt.Columns.Add(new DataColumn(getMeanName(c), typeof(double)));
            }
            foreach (var c1 in metric.Classes[axis1])
            {
                var row = dt.NewRow();
                row[metric.Axes[axis1]] = c1;
                foreach (var c2 in metric.Classes[axis2])
                {
                    row[getMeanName(c2)] = metric.Samples[getIndex(c1, c2)].Mean.TotalSeconds;
                }
                dt.Rows.Add(row);
            }
            return dt;
        }
    }
}
