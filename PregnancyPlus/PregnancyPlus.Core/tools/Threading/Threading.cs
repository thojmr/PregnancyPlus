using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains generic threading methods used to multithread some heavy compute tasks
     public static class Threading
    {
        /// <summary>
        ///     Apply a function to a collection of data by spreading the work evenly between all threads.
        ///     Outputs of the functions are returned to the current thread and returned when all threads are done.
        ///         This version of RunParallel, preserves the input/output order by tracking the index
        /// </summary>
        /// <typeparam name="TIn">Type of the input values.</typeparam>
        /// <typeparam name="TOut">Type of the output values.</typeparam>
        /// <param name="data">Input values for the work function.</param>
        /// <param name="work">Function to apply to the data on multiple threads at once.</param>
        /// <exception cref="Exception">
        ///     An exception was thrown inside one of the threads, and the operation was
        ///     aborted.
        /// </exception>
        public static TOut[] RunParallel<TIn, TOut>(this IList<TIn> data, Func<TIn, int, TOut> work, int workerCount = -1)
        {
            if (workerCount < 0)
                workerCount = Mathf.Max(2, Environment.ProcessorCount/8);
            else if (workerCount == 0)
                throw new ArgumentException("Need at least 1 worker", nameof(workerCount));

            var outArr = new TOut[data.Count];

            var currentIndex = data.Count;

            var are = new ManualResetEvent(false);
            var runningCount = workerCount;
            Exception exceptionThrown = null;

            void DoWork(object _)
            {
                try
                {
                    while (true)
                    {
                        if (exceptionThrown != null)
                            return;

                        var decrementedIndex = Interlocked.Decrement(ref currentIndex);
                        if (decrementedIndex < 0)
                            return;

                        outArr[decrementedIndex] = work(data[decrementedIndex], decrementedIndex);
                    }
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }
                finally
                {
                    var decCount = Interlocked.Decrement(ref runningCount);
                    if (decCount <= 0)
                        are.Set();
                }
            }

            // Start threads to process the data
            for (var i = 0; i < workerCount - 1; i++)
                ThreadPool.QueueUserWorkItem(DoWork);

            //So some of the work on main thread, while threadpools are queueing
            DoWork(null);

            are.WaitOne();

            if (exceptionThrown != null)
                throw new Exception("An exception was thrown inside one of the threads", exceptionThrown);

            return outArr;
        }
    }
}