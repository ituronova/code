using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Automata
{
    /// <summary>
    /// Implements a bounded set on integers that supports incermenting all elements adding 0 and 1.
    /// </summary>
    public class BasicCountingSet : ICountingSet
    {
        /// <summary>
        /// Upper limit on what the maximum value in the set can be.
        /// </summary>
        readonly int upperBound;

        /// <summary>
        /// Upper limit on what the maximum value in the set can be.
        /// </summary>
        public int UpperBound
        {
            get
            {
                return upperBound;
            }
        }

        public int Flag
        {
            get
            {
                return flag;
            }
        }


        int size;
        int start;
        int end;
        int[] basicSet;
        int offset;
        public int flag;

        public bool Equals(BasicCountingSet sc)
        {
            if (sc.size == this.size)
            {
                if (sc.Max == this.Max)
                {
                    int j = sc.end;
                    for (int i = end; i != start; i++)
                    {
                        if ((offset - basicSet[i]) != (sc.offset - basicSet[j]))
                            return false;
                        j++;
                    }
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInitFlag(ICounter counter)
        {
            if (counter.LowerBound != 0)
                this.flag = (int)CsCondition.LOW;
            else
                this.flag = (int)CsCondition.MIDDLE;
        }

        public void SetFlag(ICounter counter)
        {


            if (this.Max < counter.LowerBound)
                this.flag = (int)CsCondition.LOW;
            else if (this.Max >= counter.LowerBound && !(this.IsSingleton && this.Max == counter.UpperBound))
                this.flag = (int)CsCondition.MIDDLE;
            else if (this.IsSingleton && this.Max == counter.UpperBound)
                this.flag = (int)CsCondition.HIGH;
        }


        public int GetFlag(ICounter counter)
        {
            if (this.Max >= counter.LowerBound && !(this.IsSingleton && this.Max == counter.UpperBound))
                return (int)CsCondition.MIDDLE;
            else if (this.Max < counter.LowerBound)
                return (int)CsCondition.LOW;
            else if (this.IsSingleton && this.Max == counter.UpperBound)
                return (int)CsCondition.HIGH;
            return -1;
        }









        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlag(CsCondition flag, ICounter counter)
        {
            switch (flag)
            {
                case CsCondition.LOW:
                    if (this.Max < counter.LowerBound)
                        return true;
                    break;
                case CsCondition.MIDDLE:
                    if (this.Max >= counter.LowerBound && !(this.IsSingleton && this.Max == counter.UpperBound))
                        return true;
                    break;
                case CsCondition.HIGH:
                    if (this.IsSingleton && this.Max == counter.UpperBound)
                        return true;
                    break;
            }
            return false;
        }

        /// <summary>
        /// True iff the counting set is empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return start == end;
            }
        }



        /// <summary>
        /// True iff the counting set is a singleton set.
        /// </summary>
        public bool IsSingleton
        {
            get
            {
                return start == (end + 1) % size;
            }
        }

        /// <summary>
        /// True iff the counting set is full.
        /// </summary>
        public bool IsFull
        {
            get
            {
                return (start + 1) % size == end;
            }
        }

        /// <summary>
        /// Size of counting set
        /// </summary>
        public int CountElements
        {
            get
            {
                return ((start+size)-end)%size;
            }
        }
        
        /// <summary>
        /// Create a counting set, max is the maximum element size, max must be at least 2 and initialize it to 0.
        /// </summary>
        public BasicCountingSet(ICounter c)
        {
            this.upperBound = c.UpperBound;
            this.size = c.UpperBound + 1;
            this.basicSet = new int[size];            
            this.basicSet[start] = 0;
            this.start = 1;
            this.end = 0;
            this.offset = 0;
            this.SetInitFlag(c);
        }

        public bool IsAbove()
        {
            if (this.Max > this.UpperBound)
                return true;
            return false;
        }

        /// <summary>
        /// Gets the maximum value in the set. Set must be nonempty.
        /// </summary>
        public int Max
        {
            get
            {
                return offset - basicSet[end];
            }
        }

        /// <summary>
        /// Gets the minimum value in the set. Set must be nonempty.
        /// </summary>
        public int Min
        {
            get
            {
                return offset - basicSet[(start - 1) % size];
            }
        }

        /// <summary>
        /// Set the counting set to the value [0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set0()
        {
            this.basicSet[0] = 0;
            start = 1;
            end = 0;
            this.offset = 0;
        }

        /// <summary>
        /// Set the counting set to the value [1].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set1()
        {
            this.basicSet[0] = 0;
            start = 1;
            end = 0;
            this.offset = 1;
        }


        /// <summary>
        /// Increment all values in the set.
        /// If Max becomes greater than UpperBound then remove it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Incr()
        {
            if (Max == upperBound)
            {
                //remove the first element
                end = (end + 1) % size;
                /* if (this.IsEmpty)
                 {
                     basicSet[0] = 0;
                     start = 1;
                     end = 0;
                     offset = 0;
                 }
                 else*/
                offset += 1;
            }
            else
                offset += 1;
        }

        /// <summary>
        /// Push 0 into the set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push0()
        {
            if (basicSet[(start + size - 1) % size] != offset)
            {
                basicSet[start] = offset;
                start = (start + 1) % size;
            }         
        }


        /// <summary>
        /// Push 1 into the set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push1()
        {
            if (this.IsEmpty)
            {
                basicSet[start] = 0;
                start = 1;
                offset = 1;
            }
            else if (offset - basicSet[end] == 0)  // maximum is equal to 0
            {
                Incr();  // increment 0
                Push0(); // push 0
            }
            else if (offset - basicSet[end] == 1)  // maximum is equal to 1
            {
                return;
            }
            else
            {
                throw new AutomataException("Push 1 to counting set with more elements.\n");
            }
        }

        /// <summary>
        /// Empty the set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(ICounter counter)
        {
            
            basicSet[0] = 0;
            start = 1;
            end = 0;            
            offset = 0;
            this.SetFlag(counter);
        }

        /// <summary>
        /// Increment all values in the set and push 0 into the set.
        /// If Max becomes greater than UpperBound then remove it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrPush0()
        {
            if (Max == upperBound)
            {
                //remove the first element
                end = (end + 1) % size;
            }
            offset += 1;
            if (basicSet[(start + size - 1) % size] != offset)
            {
                basicSet[start] = offset;
                start = (start + 1) % size;
            }
        }

        /// <summary>
        /// Increment all values in the set and push 1 into the set.
        /// If Max becomes greater than UpperBound then remove it.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrPush1()
        {
            if (basicSet[(start + size - 1) % size] != offset)
            {
                basicSet[start] = offset;
                start = (start + 1) % size;           
               
            }
           
            if (Max == upperBound)
            {
                //remove the first element
                end = (end + 1) % size;
                /* if (this.IsEmpty)
                 {
                     basicSet[0] = 0;
                     start = 1;
                     end = 0;
                     offset = 0;
                 }
                 else*/
                offset += 1;
            }
            else
                offset += 1;
        }

        /// <summary>
        /// Increment all values in the set and push 0 and 1 into the set.
        /// If Max becomes greater than UpperBound then remove it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrPush01()
        {
            if (basicSet[(start + size - 1) % size] != offset)
            {
                basicSet[start] = offset;
                start = (start + 1) % size;
            }
            if (Max == upperBound)
            {
                //remove the first element
                end = (end + 1) % size;
                /* if (this.IsEmpty)
                 {
                     basicSet[0] = 0;
                     start = 1;
                     end = 0;
                     offset = 0;
                 }
                 else*/
                offset += 1;
            }
            else
                offset += 1;
            if (basicSet[(start + size - 1) % size] != offset)
            {
                basicSet[start] = offset;
                start = (start + 1) % size;
            }
        }


        /// <summary>
        /// Returns decimal representation of the elements in the set in decreasing order.
        /// </summary>
        public override string ToString()
        {
            var s = "[";
            for (int i = 0; (end + i) % size < start; i++)
            {
                if (s != "[")
                    s += ",";
                s += (offset - basicSet[(end + i) % size]).ToString();
            }
            s += "]";
            return s;
        }

        /* public BasicCountingSet Clone()
         {
             var cs = new BasicCountingSet(this.UpperBound);
             cs.basicSet = (int[])this.basicSet.Clone();
             cs.end = this.end;
             cs.offset = this.offset;
             cs.start = this.start;
             cs.size = this.size;
             return cs;
         }*/
    }
}
