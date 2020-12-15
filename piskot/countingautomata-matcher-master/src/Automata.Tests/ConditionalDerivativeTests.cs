using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using Microsoft.Automata;
using Microsoft.Automata.Generated;
using Microsoft.Automata.Rex;
using Microsoft.Automata.Utilities;

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Security.Cryptography;

namespace Automata.Tests
{
    [TestClass]
    public class ConditionalDerivativeTests
    {
  
        [TestMethod]
        public void TestCountingSetAutomataMatch()
        {
            Microsoft.Automata.DirectedGraphs.Options.MaxDgmlTransitionLabelLength = 1000000;
            
            // create graphs
            bool graphs = true;
            //bool graphs = false;
            
            // test example
           // bool matchExample = true;
            bool matchExample = false;

            // try positive and negative samples
            bool verify = true;
           // bool verify = false;                    
           
            var regex = new Regex("[a-z0-9/\\+]{10,44}", RegexOptions.Singleline);
            //var regex = new Regex("[a-q][^u-z]{13}x", RegexOptions.Singleline);
            //var regex = new Regex(".{0,2}(Tom|Sawyer|Huckleberry|Finn)", RegexOptions.Singleline);
            //var regex = new Regex("[a]{1,20}", RegexOptions.Singleline); 
            //var regex = new Regex("(ba{8})*", RegexOptions.Singleline);
            //var regex = new Regex("((aa|aaa)d(a|aa)){2,4}", RegexOptions.Singleline);

            //var regex = new Regex("a[ab]{2,4}dd[bc]{2,4}", RegexOptions.Singleline);
            //var regex = new Regex(".*(b(aa|aaa)){2,4}", RegexOptions.Singleline);  

            //var regex = new Regex("[a-d][c-f]{2,4}", RegexOptions.Singleline); 
            //var regex = new Regex("Tom.{10,25}river", RegexOptions.Singleline);
            // var regex = new Regex("((a|b){1337}(a|b|c){8663})*", RegexOptions.Singleline);
            
            // convert to symbolic regex
            var sr = (SymbolicRegex<ulong>)regex.Compile(false, false);
          
            // create NCA
            CountingAutomaton<ulong> aut = sr.Pattern.CreateCountingAutomatonPushIncr(false);
            CsAutomatonOpt<ulong> detAutOpt = aut.CompileOpt(1, graphs);

            if (graphs)
            {
                aut.ShowGraph("NCA", true);
                detAutOpt.ShowGraph("DCA", true);
            }
            if(matchExample)
            {
                // try example
                CSMatcher matcher = new CSMatcher();
                Assert.IsFalse(sr.IsMatchCA("aabb", detAutOpt, true, matcher));
            }            
            if (verify)
            {
                // test samples
                var samples = regex.GenerateRandomDataSet(100);
                var negSamples = regex.Complement().GenerateRandomDataSet(100);
                CSMatcher matcher = null;
                matcher = new CSMatcher();
                foreach (var str in samples)
                {
                    //Console.Write(str);
                    if (!sr.IsMatchCA(str, detAutOpt, false))
                    {
                        Console.Write("E-accept - detAut: " + str + "\n");
                        break;
                    }
                }
                 foreach (var str in negSamples)
                   {
                       if (sr.IsMatchCA(str, detAutOpt, false))
                       {
                           Console.Write("E-reject - detAut: " + str + "\n");
                           break;
                       }    
                }
            }
            Console.Write("Press a button...");
            Console.Read();
        }

        [TestMethod]
        public void TestConditionalDerivativeEnumeration()
        {
            var regex = new Regex("((ab){3,9}){7}");
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
            var a = q1.builder.solver.MkCharConstraint('a');
            var b = q1.builder.solver.MkCharConstraint('b');
            //---
            var q1_a_derivs = q1.GetConditinalDerivatives(a);
            var q1_b_derivs = q1.GetConditinalDerivatives(b);
            Assert.IsTrue(q1_b_derivs.Count == 0);
            Assert.IsTrue(q1_a_derivs.Count == 1);
            Assert.IsTrue(q1_a_derivs[0].Condition.Length == 2);
            //---
            var q2 = q1_a_derivs[0].PartialDerivative;
            var q2_a_derivs = q2.GetConditinalDerivatives(a);
            var q2_b_derivs = q2.GetConditinalDerivatives(b);
            Assert.IsTrue(q2_a_derivs.Count == 0);
            Assert.IsTrue(q2_b_derivs.Count == 1);
            Assert.IsTrue(q2_b_derivs[0].Condition.Length == 0);
            //---
            var q3 = q2_b_derivs[0].PartialDerivative;
            var q3_a_derivs = q3.GetConditinalDerivatives(a);
            var q3_b_derivs = q3.GetConditinalDerivatives(b);
            Assert.IsTrue(q3_b_derivs.Count == 0);
            Assert.IsTrue(q3_a_derivs.Count == 2);
        }

        [TestMethod]
        public void TestConditionalDerivativeEnumeration2()
        {
            var regex = new Regex("ab{3,10}a");
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
            var a = q1.builder.solver.MkCharConstraint('a');
            var b = q1.builder.solver.MkCharConstraint('b');
            //---
            var q1_a_derivs = q1.GetConditinalDerivatives(a);
            var q1_b_derivs = q1.GetConditinalDerivatives(b);
            Assert.IsTrue(q1_b_derivs.Count == 0);
            Assert.IsTrue(q1_a_derivs.Count == 1);
            Assert.IsTrue(q1_a_derivs[0].Condition.Length == 0);
            //---
            var q2 = q1_a_derivs[0].PartialDerivative;
            var q2_a_derivs = q2.GetConditinalDerivatives(a);
            var q2_b_derivs = q2.GetConditinalDerivatives(b);
            Assert.IsTrue(q2_a_derivs.Count == 1);
            Assert.IsTrue(q2_b_derivs.Count == 1);
            Assert.IsTrue(q2_b_derivs[0].Condition.Length == 1);
            Assert.IsTrue(q2_a_derivs[0].Condition.Length == 1);
            //---
            var q3 = q2_b_derivs[0].PartialDerivative;
            Assert.IsTrue(q3.Equals(q2));
            var q4 = q2_a_derivs[0].PartialDerivative;
            Assert.IsTrue(q4.Equals(q4.builder.epsilon));
        }

        [TestMethod]
        public void TestConditionalDerivativeEnumeration3()
        {
            var regex = new Regex("((ac){10}|(ab){20}){50}");
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
            var a = q1.builder.solver.MkCharConstraint('a');
            var b = q1.builder.solver.MkCharConstraint('b');
            //---
            var q1_a_derivs = q1.GetConditinalDerivatives(a);
            var q1_b_derivs = q1.GetConditinalDerivatives(b);
            Assert.IsTrue(q1_b_derivs.Count == 0);
            Assert.IsTrue(q1_a_derivs.Count == 2);
            Assert.IsTrue(q1_a_derivs[0].Condition.Length == 2);
            //---
            var q2 = q1_a_derivs[1].PartialDerivative;
            var q2_a_derivs = q2.GetConditinalDerivatives(a);
            var q2_b_derivs = q2.GetConditinalDerivatives(b);
            Assert.IsTrue(q2_a_derivs.Count == 0);
            Assert.IsTrue(q2_b_derivs.Count == 1);
            Assert.IsTrue(q2_b_derivs[0].Condition.Length == 0);
            //---
        }

        [TestMethod]
        public void TestCACreation_Nonmonadic()
        {
            var regex = new Regex("a(xyz){4,9}ef");
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
           // var aut = q1.CreateCountingAutomaton(false);
            //Assert.IsTrue(aut.NrOfCounters == 1);
            //aut.ShowGraph("CA");
        }

        [TestMethod]
        public void TestCACreation_Nonmonadic2()
        {
            var regex = new Regex("ab(ab){4,9}a+");
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
           // var aut = q1.CreateCountingAutomaton(false);
            //Assert.IsTrue(aut.NrOfCounters == 1);
            //aut.ShowGraph("CA");
        }

        [TestMethod]
        public void TestCACreation_Nonmonadic3()
        {
            var regex = new Regex(".*(dedfg(abcdedfg){4,9}a+|abcdedfg(abcdedfg){4,9}a+)", RegexOptions.Singleline);
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
           // var aut = q1.CreateCountingAutomaton(false);
            //Assert.IsTrue(aut.NrOfCounters == 1);
            //aut.ShowGraph("CA",true);
        }  

        [TestMethod]
        public void TestCACreation_Nonmonadic4()
        {
            var regex = new Regex(".*(dedfg(abcdedfg){4,9}a+|abcdedfg(ab(cde){3,100}dfg){5,8}a+)", RegexOptions.Singleline);
            var q1 = ((SymbolicRegex<ulong>)regex.Compile(true, false)).Pattern;
           // var aut = q1.CreateCountingAutomaton(false);
            //Assert.IsTrue(aut.NrOfCounters == 3);
           // aut.ShowGraph("CA", true);
        }
    }
}
