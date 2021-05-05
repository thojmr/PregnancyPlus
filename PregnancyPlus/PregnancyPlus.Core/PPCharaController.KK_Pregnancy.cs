using System;
using System.Collections.Generic;
using KKAPI.Chara;
using KKAPI.Maker;
using UnityEngine;
#if HS2 || AI
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the logic for KK_Pregnancy integration
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {                    
        internal float _inflationStart = 0;
        internal float _inflationChange = 0f;//The current inflation size lerping towards the target size
        public float _targetPregPlusSize = 0f;//The final size to inflate to
        internal float timeElapsed = 0f;
        internal List<BlendShapeController> blendShapeCtrlList = new List<BlendShapeController>();//Holds the list of blendshapes we want to inflaate


        /// <summary>
        /// fetch KK_Pregnancy Data.Week value for KK story mode integration (It works if you don't mind the clipping)
        /// </summary>
        internal void GetWeeksAndSetInflation(bool checkNewMesh = false, bool slidersChanged = false) 
        {            

            //If a card value is set for inflation size, use that first, otherwise check KK_Pregnancy for Weeks value
            var cardData = GetCardData();
            if (cardData.inflationSize > 0 && cardData.GameplayEnabled) 
            {
                MeshInflate(cardData, checkNewMesh, slidersChanged);
                return;
            }

            var week = PregnancyPlusHelper.GetWeeksFromPregnancyPluginData(ChaControl, KK_PregnancyPluginName);
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetWeeksAndSetInflation {ChaControl.name} >  Week:{week} checkNewMesh:{checkNewMesh} slidersChanged:{slidersChanged}");
            if (week < 0) {
                //Fix for when character gives birth, we need to reset belly
                if (infConfig.inflationSize > 0) MeshInflate(0);
                return;
            }

            //Compute the additonal belly size added based on user configured vallue from 0-40
            var additionalPregPlusSize = Mathf.Lerp(0, week, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);

            MeshInflate(additionalPregPlusSize, checkNewMesh, slidersChanged);
        }
        

        /// <summary>
        /// Triggered when InflationAmount in KK_Pregnancy changes, and sets the Preg+ additional inflation size
        /// </summary>
        public void InflationChanged(float inflationAmount, int maxInflationSize) {
            //Calculate the target inflation size from the KK_Pregnancy inflations state
            var inflationSize = inflationAmount/maxInflationSize * 40;
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" InflationChanged {inflationAmount} maxInflationSize {maxInflationSize} inflationSize {inflationSize}");
            
            blendShapeCtrlList = ComputeInflationBlendShapes();

            //Compute the additonal belly size added based on user configured vallue from 0-40
            //When this value changes it will start ComputeInflationChange()
            _targetPregPlusSize = Mathf.Lerp(0, inflationSize, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);
            _inflationStart = _inflationChange;
        }


        /// <summary>
        /// When inflation is triggered we need to spread the effect over time
        /// </summary>
        public void ComputeInflationChange() {
            #if KK
                if (!GameAPI.InsideHScene) return;
                if (_inflationChange == _targetPregPlusSize) 
                {
                    clearInflationStuff();
                    return;
                } 
                
                //Lerp the change in size over 3 seconds
                _inflationChange = Mathf.Lerp(_inflationStart, _targetPregPlusSize, timeElapsed / 3);
                timeElapsed += Time.deltaTime;  

                //Snap the value at the end
                if (Math.Abs(_inflationChange - _targetPregPlusSize) < 0.05f) _inflationChange = _targetPregPlusSize;

                //Update mesh shape as it changes (might want an optimized method to do this)
                var inflationSize = Math.Round(_inflationChange, 1); 
                
                //Update all blendshapes weights to this new size
                QuickInflate((float) inflationSize, blendShapeCtrlList);                    
                
            #endif
        }


        /// <summary>
        /// Set the weights of all blendShapeControllers in a list to allow quick and frequent mesh weight changes
        /// </summary>
        public void QuickInflate(float inflationWeight,  List<BlendShapeController> bscList) {
            foreach(var blendShapeCtrl in bscList)
            {
                var success = blendShapeCtrl.ApplyBlendShapeWeight(inflationWeight);
                if (!success && PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" QuickInflate > an smr was null, skipping mesh");
            }
        }


        public void clearInflationStuff() {
            timeElapsed = 0;
            _inflationStart = _inflationChange;
            blendShapeCtrlList = new List<BlendShapeController>();
        }

    }
}


