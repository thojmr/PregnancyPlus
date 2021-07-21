using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains generic threading methods used to multithread some heavy compute tasks
    public class Threading
    {   
        //List of active threaded functions, that we will watch for completion
        public List<Action> ThreadedFunctionQueue = new List<Action>();

        //Total active threads
        public int threadCount = 0;
        public bool lastTickHadThread = false;

        //Once the last thread in the list has been completed
        public bool AllDone 
        {
            get { return lastTickHadThread && threadCount == 0; }
        }



        /// <summary>
        ///     Start some function in a new thread
        ///         ThreadedFunction must call AddResultToThreadQueue in order for the results to return back to the main thread
        /// </summary>
        /// <param name="ThreadedFunction">
        ///     The method that contains the threaded logic, and should call the AddResultToThreadQueue() function when done
        /// </param>        
        public void Start(Action ThreadedFunction) 
        {
            Thread t = new Thread( ()=> { ThreadedFunction(); });
            threadCount += 1;
            lastTickHadThread = true;
            t.Start();
        }


        /// <summary>
        ///     Add a threaded function to the watch queue, to watch for completion, and run on main thread with results of the threaded function
        ///         Ex: a lambda function that assigns the computed Vecrtor3 back to the unity Game object it belongs to
        /// </summary>
        /// <param name="MainThreadResultsFunction">
        ///     The method that contains the result logic to be triggered after the thread is done.  It plugs the results back into unity
        /// </param>
        public void AddResultToThreadQueue(Action MainThreadResultsFunction) 
        {
            ThreadedFunctionQueue.Add(MainThreadResultsFunction);
        }


        /// <summary>
        ///     When a thread is done, execute the results function in the main thread.  Runs in Update()
        /// </summary>
        public void WatchAndExecuteThreadResults() 
        {
            //Reset last tick tracker when already at 0 this tick
            if (threadCount == 0 && lastTickHadThread) lastTickHadThread = false;

            //Watch for completed thread tasks
            while (ThreadedFunctionQueue.Count > 0) 
            {
                //Get the latest finished thread task
                var resultFunction = ThreadedFunctionQueue[0];
                ThreadedFunctionQueue.RemoveAt(0);
                threadCount -= 1;                

                //Execute the "result function" in this main thread to complete its tasks
                resultFunction();
            }            
        }
    }
}