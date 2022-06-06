using System;
using System.Collections.Generic;
using KKAPI.Chara;
using UnityEngine;
#if HS2
    using AIChara;
#elif KKS
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

        /// <summary>
        /// Trigger belly inflation in HS2 after hsceneflag hook fired
        /// </summary>
        public void HS2Inflation(bool deflate = false)
        {
            int _nextInflationLevel;

            if (deflate)
                _nextInflationLevel = 0;
            else
                _nextInflationLevel = _currentInflationLevel + 1;

            //clip 
            _currentInflationLevel = Math.Max(0, Math.Min(_nextInflationLevel, PregnancyPlusPlugin.HS2InflationMaxLevel.Value));

            //Re-use the kk pregnancy inflation code here to smooth the inflation animation
            OnInflationChanged(_currentInflationLevel, PregnancyPlusPlugin.HS2InflationMaxLevel.Value, 0);
        }

    }
}
