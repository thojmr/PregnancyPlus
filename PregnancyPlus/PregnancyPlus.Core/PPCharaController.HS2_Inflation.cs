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
        public int currentInflationLevel = 0;
        const int maxInflationLevel = 6;


        /// <summary>
        /// Trigger belly inflation in HS2 after hsceneflag hook fired
        /// </summary>
        public void HS2Inflation(bool deflate = false)
        {                
            if (deflate) currentInflationLevel = 0;
            if (currentInflationLevel < maxInflationLevel) 
            {
                //Increment inflation count
                currentInflationLevel += 1;
            }
                  
            //Re-use the kk pregnancy inflation code here to smooth the inflation animation
            OnInflationChanged(currentInflationLevel, maxInflationLevel);                    
        }
    }
}
