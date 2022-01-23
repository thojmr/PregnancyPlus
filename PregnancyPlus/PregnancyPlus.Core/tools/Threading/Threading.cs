using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //Contains generic threading methods used to multithread some heavy compute tasks (Can't use Unity Jobs because Koikatsu is too old)
    public class Threading
    {   
        //List of active thread result functions, that we will execute on thread completion
        public List<Action> ThreadedFunctionQueue = new List<Action>();
        //Total active threads
        public int _threadCount = 0;
        public int ThreadCount 
        {
            get { return _threadCount; }
            set { _threadCount = Math.Max(value, 0); }
        }
        //Whether the last frame had running threads
        public bool lastFrameHadThread = false;

        //Once the last thread in the list has been completed this will be true the next frame
        //  This is only reliable because we start all mesh compute threads in the same frame
        public bool AllDone 
        {
            get { return lastFrameHadThread && ThreadCount == 0; }
        }



        /// <summary>
        /// Start some function in a new thread
        ///     ThreadedFunction must call AddResultToThreadQueue in order for the results to return back to the main thread
        /// </summary>
        /// <param name="ThreadedFunction">
        /// The method that contains the threaded logic, and should call the AddResultToThreadQueue() function when done
        /// </param>        
        public void Start(WaitCallback ThreadedFunction) 
        {
            ThreadCount += 1;
            lastFrameHadThread = true;
            ThreadPool.QueueUserWorkItem(ThreadedFunction);
        }


        /// <summary>
        /// Add a threaded function to the watch queue, to watch for completion, and run on main thread with results of the threaded function
        ///     Ex: a lambda function that assigns the computed Vecrtor3 back to the unity Game object it belongs to
        /// </summary>
        /// <param name="MainThreadResultsFunction">
        /// The method that contains the result logic, which will be triggered after the thread is done.  Plugs the results back into unity
        /// </param>
        public void AddResultToThreadQueue(Action MainThreadResultsFunction) 
        {
            ThreadedFunctionQueue.Add(MainThreadResultsFunction);
        }


        /// <summary>
        /// When a thread is done, execute the results function in the main thread.  Runs in Update()
        /// </summary>
        public void WatchAndExecuteThreadResults() 
        {
            //Reset last frame tracker when already at 0 this frame
            if (ThreadCount == 0 && lastFrameHadThread) lastFrameHadThread = false;

            //Watch for completed thread tasks
            while (ThreadedFunctionQueue.Count > 0) 
            {
                //Get the latest finished thread task
                var resultFunction = ThreadedFunctionQueue[0];
                ThreadedFunctionQueue?.RemoveAt(0);
                ThreadCount -= 1;                

                //Execute the "result function" in this main thread to complete its tasks
                resultFunction();
            }            
        }
    }
}