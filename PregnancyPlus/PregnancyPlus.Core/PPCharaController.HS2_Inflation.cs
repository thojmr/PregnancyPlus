using System;
using System.Collections.Generic;
using KKAPI.Chara;
using UnityEngine;
#if HS2
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#elif AI
    using AIChara;
    using KKAPI.MainGame;
#endif        

namespace KK_PregnancyPlus
{

    //This partial class contains the logic for HS2 belly infaltion
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {   
        //How many times the belly has been inflated, up to he max
        private int _currentInflationLevel = 0;
        //public int maxInflationLevel = 6;

        //public int inflationStep = 1;
        //public int deflationStep = 1;


        /// <summary>
        /// Trigger belly inflation in HS2 after hsceneflag hook fired
        /// </summary>
        public void HS2Inflation(bool deflate = false)
        {
            int _nextInflationLevel;

            if (deflate)
                _nextInflationLevel = _currentInflationLevel - PregnancyPlusPlugin.DeflationLevel.Value;
            else
                _nextInflationLevel = _currentInflationLevel + PregnancyPlusPlugin.InflationLevel.Value;

            //clip 
            _currentInflationLevel = Math.Max(0, Math.Min(_nextInflationLevel, PregnancyPlusPlugin.CumflationMaxLevel.Value));

            //Re-use the kk pregnancy inflation code here to smooth the inflation animation
            OnInflationChanged(_currentInflationLevel, PregnancyPlusPlugin.CumflationMaxLevel.Value);
        }

    }
}
