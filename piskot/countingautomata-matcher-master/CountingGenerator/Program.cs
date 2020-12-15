using Microsoft.Automata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace CountingGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Microsoft.Automata.DirectedGraphs.Options.MaxDgmlTransitionLabelLength = 1000000;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            bool graphs = false;
            var verbose = false;
            string exactMatch = "";
            if (args.Length < 10)
            {
                Console.WriteLine("\n---------------------------  HELP --------------------------\n");
                Console.WriteLine("Usage: ./CounterAutomataBench [inputFile] [outputFolder] [TIMEOUT] [TIMEOUT Gen] [lines] [maxLine] [probCut] [probShuffle] [probGenPattern] [-f|-nf]\n");
                Console.WriteLine("[inputFile]\t\tinput file");
                Console.WriteLine("[outputFolder]\t\toutnput folder");
                Console.WriteLine("[TIMEOUT]\t\ttimeout for generating DCA and computing wedghts for edges");
                Console.WriteLine("[TIMEOUTGen]\t\ttimeout for generating output text");
                Console.WriteLine("[lines]\t\t \tnumber of lines in output text");
                Console.WriteLine("[maxLine]\t\tapproximal maximal lenght of the line");
                Console.WriteLine("[probCut]\t\tprobability of stopping generating a string");
                Console.WriteLine("[probShuffle]\t\tprobability of shuffling successors while generating");
                Console.WriteLine("[probGenPattern]\tprobability of inserting an exact match to the line");
                Console.WriteLine("[-f|-nf]\t if [-nf] the whole string should not be generated (no exact match should be generated)");
                Console.WriteLine("\n-------------------------------------------------------------\n");
                Console.Read();
                return;
            }
            string readme = "[inputFile]\t\t\t" + args[0] + "\n"
                              + "[outputFolder]\t\t" + args[1] + "\n"
                              + "[TIMEOUT]\t\t\t" + args[2] + "\n"
                              + "[TIMEOUTGen]\t\t" + args[3] + "\n"
                              + "[lines]\t\t\t\t" + args[4] + "\n"
                              + "[maxLine]\t\t\t" + args[5] + "\n"
                              + "[probCut]\t\t\t" + args[6] + "\n"
                              + "[probShuffle]\t\t" + args[7] + "\n"
                              + "[probGenPattern]\t" + args[8] + "\n"
                              + "[-f|-nf]\t" + args[9] + "\n";

            string inputFile = args[0];
            string outputFolder = args[1];
            System.IO.Directory.CreateDirectory("./results/results-" + DateTime.Now.ToString("yyyy-MM-dd") + "/");
            System.IO.Directory.CreateDirectory(outputFolder);
            // create README file
            File.AppendAllText(outputFolder + "/readme.txt", readme);

            //string outputFile =  "./results/results-"+ DateTime.Now.ToString("yyyy-MM-dd") +"/" + Path.GetFileNameWithoutExtension(inputFile) + "-" + LongRandom(100000, 100050, new Random()) +".csv";
            string outputFile = "./results/results-" + DateTime.Now.ToString("yyyy-MM-dd") + "/genText-" + Path.GetFileNameWithoutExtension(inputFile) + "-" + LongRandom(100000, 100050, new Random()) + ".csv";
            string outputTextFile = "./results/results-" + DateTime.Now.ToString("yyyy-MM-dd") + "/" + Path.GetFileNameWithoutExtension(inputFile) + ".txt";

            // read input parameters to variables
            int maxLenght = Int32.Parse(args[5]);
            double randomCut = double.Parse(args[6]);
            double randomShuffle = double.Parse(args[7]);
            double randomGenPattern = double.Parse(args[8]);
            int lines = Int32.Parse(args[4]);
            int TIMEOUT = Int32.Parse(args[2]);
            int TIMEOUT2 = Int32.Parse(args[3]);
            bool finalFlag = (args[9] == "-f") ? true : false;
            int index = 0;
            int cntErrors = 0;
            int cntTimeout = 0;
            string outputText = "";
            CsAutomatonOpt<ulong> dca = null;

            using (StreamReader sr = File.OpenText(inputFile))
            {
                string s = String.Empty;
                string line = String.Empty;
                List<Tuple<double, int>>[] nextTable = null;
                File.AppendAllText(outputFile, "pattern;max elems;average;max counter;sum counter;DCA states;time [ms]\n");
                dca = null;
                Regex rgx = null;
                SymbolicRegex<ulong> sRegex = null;
                CountingAutomaton<ulong> nca = null;
                double[] mapStateToCycles = null;
                while ((s = sr.ReadLine()) != null)
                {
                    index++;
                    IAsyncResult result;
                    Action action = () =>
                    {
                        try
                        {
                            dca = null;
                            rgx = null;
                            sRegex = null;
                            nca = null;

                            // convert regex to DCA
                            Console.WriteLine(s);
                            rgx = new Regex(s, RegexOptions.Singleline);
                            sRegex = ((SymbolicRegex<ulong>)rgx.Compile(false, false));
                            nca = sRegex.Pattern.CreateCountingAutomatonPushIncr(false);
                            dca = nca.CompileOpt(1, true);
                            dca.Preprocessing();
                            if (verbose)
                                Console.WriteLine("Preproccesing done.");
                            if (graphs)
                            {
                                nca.ShowGraph("NCA - chart", true);
                                dca.ShowGraph("DCA - chart", true);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error while converting regex to DCA: ");
                            Console.WriteLine(e.Message);
                            cntErrors++;
                            File.AppendAllText(outputFile, rgx.ToString() + ";;;;;;Error while converting regex to DCA: " + e.Message + "\n", Encoding.UTF8);
                            return;
                        }

                    };
                    result = action.BeginInvoke(null, null);

                    if (!result.AsyncWaitHandle.WaitOne(TIMEOUT))
                    {
                        File.AppendAllText(outputFile, s + ";;;;;;TIMEOUT while converting regex to DCA.\n", Encoding.UTF8);
                        Console.WriteLine("TIMEOUT while converting to DCA.");
                        continue;
                    }

                    if (dca == null)
                        continue;
                    Action action3 = () =>
                    {
                        try
                        {
                            mapStateToCycles = new double[dca.cntStates + 1];
                            // initialization of table fo weights
                            dca.InitCycleTable();
                            if (verbose)
                                Console.WriteLine("Initialization of edges done.");
                            // compute all SSCs
                            dca.Kosaraju();
                            // compute cycles for each state of DCA
                            for (int i = 1; i < dca.cntStates + 1; i++)
                            {
                                // if it has more than two incomming edges or if it is initial than if it has more than one edge
                                if ((i == 1 && dca.HasMoreThanOneEdge(i)) || dca.HasTwoIncommingEdges(i))
                                {
                                    var nodeList = new List<int> { i };
                                    bool[] isVisited = new bool[dca.cntStates + 1];
                                    var cycles = new List<List<int>>();
                                    // get SSC for the state i
                                    var ssc = dca.GetSSC(i);
                                    if (ssc == null)
                                        continue;   // skip state that are not a part of ssc
                                    // find all cycles started in state i
                                    dca.FindAllCycles(i, i, isVisited, nodeList, cycles, ssc);
                                    // if there is a cycle
                                    if (cycles.Count != 0)
                                    {
                                        var weightedCycles = new List<Tuple<List<int>, List<int>, double>>();
                                        // for each cycle compute its weight
                                        for (int c = 0; c < cycles.Count(); c++)
                                        {
                                            double w = dca.ComputeWeight(cycles[c]);
                                            weightedCycles.Add(new Tuple<List<int>, List<int>, double>(null, cycles[c], w));
                                        }
                                        // sort cycles according their weight
                                        weightedCycles.Sort(CompareCycle);
                                        // remember the cycle for state i
                                        mapStateToCycles[i] = weightedCycles[0].Item3;
                                        // distribute weight along each cycle
                                        for (int j = 0; j < weightedCycles.Count(); j++)
                                        {
                                            var cycle = weightedCycles[j];
                                            // iterate over the cycle
                                            for (int sn = 0; sn < cycle.Item2.Count - 1; sn++)
                                            {
                                                // change weight for maximum
                                                if (dca.weightTable[cycle.Item2[sn]][cycle.Item2[sn + 1]] < cycle.Item3)
                                                    dca.weightTable[cycle.Item2[sn]][cycle.Item2[sn + 1]] = cycle.Item3;
                                            }
                                        }
                                    }
                                }
                            }
                            if (verbose)
                            {
                                Console.WriteLine("Find cycles finished.");
                            }

                            // update weight table if there is a path to better cycle
                            for (int i = dca.cntStates; i >= 1; i--)
                            {
                                for (int j = 1; j < dca.cntStates + 1; j++)
                                {
                                    if (i == j)
                                        continue;
                                    // check if there j is a state with cycle
                                    if (mapStateToCycles[j] > 0)
                                    {
                                        var nodeList = new List<int> { i };
                                        bool[] visited = new bool[dca.cntStates + 1];
                                        // find path to the cycle
                                        dca.FindPath(i, j, visited, nodeList, mapStateToCycles);
                                        // if there is a path
                                        if (nodeList.Count > 1)
                                        {
                                            // add 0.5 penalty for the path
                                            var weight = mapStateToCycles[j] - 0.5;
                                            // update weight table
                                            if (dca.weightTable[nodeList.First()][nodeList[1]] < weight)
                                            {
                                                dca.weightTable[nodeList.First()][nodeList[1]] = weight;
                                            }
                                            // update cycle table
                                            if (mapStateToCycles[i] < weight)
                                            {
                                                mapStateToCycles[i] = weight;
                                            }
                                        }
                                    }
                                }
                            }
                            if (verbose)
                            {
                                Console.WriteLine("Add cycles finished.");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error while finding cycles: ");
                            cntErrors++;
                            File.AppendAllText(outputFile, rgx.ToString() + ";;;" + dca.MaxCounter + ";" + dca.SumCounter + ";" + dca.cntStates + ";Error while finding cycles: " + e.Message + "\n", Encoding.UTF8);
                            return;
                        }
                        // compute table for each outgoing edge of the state
                        nextTable = new List<Tuple<double, int>>[dca.cntStates + 1];
                        for (int i = 1; i < dca.cntStates + 1; i++)
                        {
                            // there is no cycle
                            if (dca.weightTable[i] == null)
                                continue;
                            nextTable[i] = new List<Tuple<double, int>>();
                            for (int j = 1; j < dca.cntStates + 1; j++)
                            {
                                // there is a cycle for the edge going from i to j
                                if (dca.weightTable[i][j] != -double.MaxValue)
                                {
                                    if (graphs)
                                        dca.AddMove(Move<CsLabel<ulong>>.Create(i, j, CsLabel<ulong>.MkWTrans(null, (int)dca.weightTable[i][j])));
                                    nextTable[i].Add(Tuple.Create<double, int>(dca.weightTable[i][j], j));
                                }
                                else if (!dca.deltaPre[i][j].IsEmpty())  // there is an edge with no weight
                                {
                                    if (graphs)
                                        dca.AddMove(Move<CsLabel<ulong>>.Create(i, j, CsLabel<ulong>.MkWTrans(null, 0)));
                                    nextTable[i].Add(Tuple.Create<double, int>(0.0, j));
                                }
                            }
                            // sort possible cycles
                            nextTable[i].Sort();
                            nextTable[i].Reverse();
                        }
                        if (graphs)
                            dca.ShowGraph("DCA - weight", true);
                    };
                    result = action3.BeginInvoke(null, null);

                    if (!result.AsyncWaitHandle.WaitOne(TIMEOUT))
                    {
                        File.AppendAllText(outputFile, s + ";;;" + dca.MaxCounter + ";" + dca.SumCounter + ";" + dca.cntStates + ";TIMEOUT while finding cycles.\n", Encoding.UTF8);
                        Console.WriteLine("TIMEOUT");
                        cntTimeout++;
                        continue;
                    }

                    // generate exact match
                    if (randomGenPattern > 0)
                    {
                        exactMatch = rgx.GenerateRandomMember(null, 1000);
                        if (dca.cntCount != 0)
                        {   // initialize counting sets
                            dca.cs = new BasicCountingSet[dca.cntCount];
                            for (int j = 0; j < dca.cntCount; j++)
                            {
                                dca.cs[j] = new BasicCountingSet(dca.counters[j]);    // initialization to 0   
                            }
                        }
                        if (!sRegex.IsMatchCA(exactMatch, dca))
                        {
                            File.AppendAllText(outputFile, s + ";;;;" + dca.SumCounter + ";" + dca.SumCounter + ";" + dca.cntStates + ";Error: Exact match not generated.\n", Encoding.UTF8);
                            Console.WriteLine("Error: Exact match not generated");
                            cntErrors++;
                            continue;
                        }
                    }

                    var gen = true;
                    // generate testing line for computing max elements in CS                  
                    Action action2 = () =>
                    {
                        try
                        {
                            dca.GenerateRandomText(rgx, nextTable, null, null, 0, 0, 0, 1, 4000, verbose, true);
                            outputText = s + ";" + dca.MaxElemCS + ";" + dca.AvrgCS.ToString().Replace(".", ",") + ";" + dca.MaxCounter + ";" + dca.SumCounter + ";" + dca.cntStates;
                            if (verbose)
                            {
                                Console.WriteLine(outputFile, s + ";" + dca.MaxElemCS + ";" + dca.AvrgCS.ToString().Replace(".", ",") + ";" + dca.MaxCounter + ";" + dca.SumCounter + ";" + dca.cntStates + ";" + sw.ElapsedMilliseconds + "\n");
                                for (int i = 0; i < dca.cntCount; i++)
                                {
                                    Console.WriteLine("C" + i + ": " + string.Join(",", dca.elemCS[i]));
                                }
                            }
                        }
                        catch (InvalidCastException e)
                        {
                            File.AppendAllText(outputFile, s + ";;;;" + dca.SumCounter + ";" + dca.cntStates + ";Error while generating text: " + e.Message + "\n", Encoding.UTF8);
                            Console.WriteLine("Error while generating text: " + e.Message);
                            cntErrors++;
                            gen = false;
                            return;
                        }
                        catch
                        {
                            File.AppendAllText(outputFile, s + ";;;;" + dca.SumCounter + ";" + dca.cntStates + ";Error while generating text: Empty string generated.\n", Encoding.UTF8);
                            Console.WriteLine("Error: Empty string generated.");
                            cntErrors++;
                            gen = false;
                            return;
                        }

                    };
                    result = action2.BeginInvoke(null, null);

                    if (!result.AsyncWaitHandle.WaitOne(TIMEOUT))
                    {
                        File.AppendAllText(outputFile, outputText + ";TIMEOUT while generating line.\n", Encoding.UTF8);
                        Console.WriteLine("TIMEOUT while generating line.");
                        continue;
                    }
                    if (!gen)    // line not generated
                    {
                        continue;
                    }
                    Action action4 = () =>
                    {
                        // generate file of x lines
                        try
                        {
                            var pathToFile = Path.GetDirectoryName(inputFile);
                            string fileName = Path.GetFileNameWithoutExtension(inputFile);
                            var name = outputFolder + "/" + fileName + "_" + index + ".txt";

                            dca.GenerateRandomText(rgx, nextTable, name, exactMatch, randomCut, randomShuffle, randomGenPattern, lines, maxLenght, verbose, finalFlag);
                            Console.WriteLine("Text generated.");
                            File.AppendAllText(outputFile, outputText + ";" + sw.ElapsedMilliseconds + ";text generated\n", Encoding.UTF8);
                            if (verbose)
                            {
                                Console.WriteLine(outputText + ";" + sw.ElapsedMilliseconds + "\n");
                                for (int i = 0; i < dca.cntCount; i++)
                                {
                                    Console.WriteLine("C" + i + ": " + string.Join(",", dca.elemCS[i]));
                                }
                            }
                        }
                        catch (InvalidCastException e)
                        {
                            File.AppendAllText(outputFile, outputText + ";Error while generating text file: " + e.Message + "\n", Encoding.UTF8);
                            Console.WriteLine("Error while generating text file: " + e.Message);
                            return;
                        }
                        catch
                        {
                            File.AppendAllText(outputFile, outputText + ";Error while generating text file: Empty string generated.\n", Encoding.UTF8);
                            Console.WriteLine("Error: Empty string generated.");
                            cntErrors++;
                            return;
                        }
                    };

                    result = action4.BeginInvoke(null, null);

                    if (!result.AsyncWaitHandle.WaitOne(TIMEOUT2))
                    {
                        File.AppendAllText(outputFile, outputText + ";TIMEOUT while generating text file.\n", Encoding.UTF8);
                        Console.WriteLine("TIMEOUT while generating text file.");
                        continue;
                    }
                }

            }

            Console.Read();
        }
    }
}

