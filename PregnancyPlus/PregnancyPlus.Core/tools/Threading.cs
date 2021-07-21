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



        /// <summary>
        ///     Start some function in a new thread
        ///         ThreadedFunction must call AddFunctionToThreadQueue in order for the results to return back to the main thread
        /// </summary>
        /// <param name="ThreadedFunction">
        ///     The method that contains the threaded logic, and should call the AddFunctionToThreadQueue() function when done
        /// </param>        
        public void Start(Action ThreadedFunction) 
        {
            Thread t = new Thread( ()=> { ThreadedFunction(); });
            t.Start();
        }


        /// <summary>
        ///     Add a threaded function to the watch queue, to watch for completion, and run on main thread with results of the threaded function
        ///         Ex: a lambda function that assigns the computed Vecrtor3 back to the unity Game object it belongs to
        /// </summary>
        /// <param name="MainThreadResultsFunction">
        ///     The method that contains the result logic to be triggered after the thread is done.  It plugs the results back into unity
        /// </param>
        public void AddFunctionToThreadQueue(Action MainThreadResultsFunction) 
        {
            ThreadedFunctionQueue.Add(MainThreadResultsFunction);
        }


        /// <summary>
        ///     When a thread is done, execute the results function in the main thread.  Runs in Update()
        /// </summary>
        public void WatchAndExecuteThreadResults() 
        {
            //Watch for completed thread tasks
            while (ThreadedFunctionQueue.Count > 0) 
            {
                //Get the latest finished thread task
                var resultFunction = ThreadedFunctionQueue[0];
                ThreadedFunctionQueue.RemoveAt(0);

                //Execute the "result function" in this main thread to complete its tasks
                resultFunction();
            }
        }
    }
}