using System;
using BepInEx.Logging;

namespace KK_PregnancyPlus
{
    // Simple debug timer with start and stop
    public class Timer
    {
        private string label;
        private ManualLogSource logger;

        //Tracks when the timer started
        private double startMilliseconds = 0;
        private double endMilliseconds = 0;


        /// <summary>
        ///Use like:
        ///var timer = new Timer("label", PregnancyPlusPlugin.Logger);
        /// </summary>
        public Timer(string _label, ManualLogSource _logger)
        {
            label = _label;
            logger = _logger;
            Start();
        }


        public void Start()
        {
            startMilliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }


        //Stop timer and show time taken
        public void Stop()
        {
            if (startMilliseconds == 0)
            {
                if (PregnancyPlusPlugin.DebugLog.Value) logger.LogWarning($" Timer has not been started, call .Start() first");
                return;
            }

            endMilliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (PregnancyPlusPlugin.DebugLog.Value) logger.LogWarning($" {label} took {Math.Round(endMilliseconds - startMilliseconds, 2)}ms");

            startMilliseconds = 0;
        }


        public void End()
        {
            Stop();
        }
    }

}