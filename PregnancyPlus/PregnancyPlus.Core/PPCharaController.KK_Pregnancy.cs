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

    //This partial class contains the logic for KK_Pregnancy integration
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {                    
        internal float _inflationStartSize = 0;
        internal float _inflationChange = 0f;//The current inflation size
        internal float _targetPregPlusSize = 0f;//The final desired inflation size
        internal int currentWeeks = 0;
        
        //The final size to inflate to
        public float TargetPregPlusSize
        {
            get => _targetPregPlusSize;
            set => _targetPregPlusSize = Mathf.Clamp(value, 0, 40);
        }

        public float CurrentInflationChange
        {
            get => _inflationChange;
        }

        internal float timeElapsed = 0f;
        internal List<BlendShapeController> blendShapeCtrlList = new List<BlendShapeController>();//Holds the list of blendshapes we want to alter during inflation

        public bool isDuringInflationScene => TargetPregPlusSize > 0 || CurrentInflationChange > 0;


        /// <summary>
        /// Fetch KK_Pregnancy Data.Week value for story mode integration
        /// </summary>
        internal void GetWeeksAndSetInflation(bool checkNewMesh = false, bool slidersChanged = false) 
        {            

            //If a card value is set for inflation size, use that first, otherwise check KK_Pregnancy for Weeks value
            var cardData = GetCardData();
            if (cardData.inflationSize > 0 && cardData.GameplayEnabled) 
            {                
                MeshInflate(cardData, "GetWeeksAndSetInflation", new MeshInflateFlags(this, _checkForNewMesh: checkNewMesh, _pluginConfigSliderChanged: slidersChanged));
                return;
            }

            #if AI || KK
            
                var weeks = PregnancyPlusPlugin.Hooks_KK_Pregnancy.GetWeeksFromPregnancyPluginData(ChaControl, KK_PregnancyPluginName);
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetWeeksAndSetInflation {ChaControl.name} >  Week:{weeks} checkNewMesh:{checkNewMesh} slidersChanged:{slidersChanged}");
                
                //Set the initial target size to be the current week.  Otherwise inflation would always start at 0 weeks.
                currentWeeks = weeks;

                if (weeks < 0) 
                {
                    //Fix for when character gives birth, we potentially need to reset belly
                    if (infConfig.inflationSize > 0) MeshInflate(0, "GetWeeksAndSetInflation");
                    return;
                }

                //If no infConfig is set for this character, use a predefined one for the best KK_Pregnancy look, since the default shape tend to look a little strange.
                if (!infConfig.HasAnyValue(false)) infConfig = GetDefaultShapeFor_KK_Pregnancy();

                //Compute the additonal belly size added based on user configured vallue from 0-40
                var additionalPregPlusSize = Mathf.Lerp(0, weeks, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);
                
                MeshInflate(additionalPregPlusSize, "GetWeeksAndSetInflation", new MeshInflateFlags(this, _checkForNewMesh: checkNewMesh, _pluginConfigSliderChanged: slidersChanged));

            #endif
        }
        

        /// <summary>
        /// Triggered when InflationAmount in KK_Pregnancy changes, and sets the Preg+ inflation size to match
        /// </summary>
        /// <param name="inflationAmount">TThe KK_Pregnancy inflation count, or number of times inflated</param>
        /// <param name="maxInflationSize">The Max number of inflations it takes to get to max size</param>
        public void OnInflationChanged(float inflationAmount, int maxInflationSize) 
        {
            //Convert the inflationAmount into a usable number
            var kkInflationSize = inflationAmount/maxInflationSize * 40;
            if (PregnancyPlusPlugin.DebugLog.Value) 
                PregnancyPlusPlugin.Logger.LogInfo($" InflationChanged {inflationAmount} maxInflationSize {maxInflationSize} kkInflationSize {kkInflationSize}");

            //No Preg+ inflation when the additional size is set to 0
            if (PregnancyPlusPlugin.MaxStoryModeBelly.Value == 0) return;
            //No additional preg+ inflation until the KKinflation amount is >= the current inflation.config.  We want both sizes to grow together.
            if (infConfig.inflationSize > kkInflationSize) return;

            //When char is already pregnant, make that the starting inflation size
            if (currentWeeks > _inflationStartSize) _inflationStartSize = _inflationChange = TargetPregPlusSize = currentWeeks;

            //If no infConfig is set for this character, use a predefined one for the best KK_Pregnancy look, since the default shape tend to look a little strange.
            if (!infConfig.HasAnyValue(false)) infConfig = GetDefaultShapeFor_KK_Pregnancy();            

            //Init and compile the list of blendshapes to target
            blendShapeCtrlList = ComputeInflationBlendShapes();

            //Compute the target belly size per inflation trigger
            //When TargetPregPlusSize changes, it will start ComputeInflationChange()
            TargetPregPlusSize = Mathf.Lerp(0, kkInflationSize, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);
            _inflationStartSize = _inflationChange;
        }


        /// <summary>
        /// When inflation is triggered we need to spread the effect over time
        /// </summary>
        public void ComputeInflationChange() 
        {
            #if KK || AI
                //Only in HScene
                #if !KKS//TODO remove later when KKS fully releases               
                if (!GameAPI.InsideHScene) 
                {
                    if (_inflationChange > 0 || TargetPregPlusSize > 0) ClearInflationStuff();
                    return;
                }
                #endif
                if (_inflationChange == TargetPregPlusSize) 
                {
                    //When inflation is done lerping, do a soft clear of values
                    ClearInflationStuff();
                    return;
                } 
                
                //Lerp the change in size over 3 seconds
                _inflationChange = Mathf.Lerp(_inflationStartSize, TargetPregPlusSize, timeElapsed / 3);
                timeElapsed += Time.deltaTime;  

                //Snap the value at the end, in case the lerp never reaches 100%
                if (Math.Abs(_inflationChange - TargetPregPlusSize) < 0.05f) _inflationChange = TargetPregPlusSize;
                
                //Update all blendshapes weights to this new size
                QuickInflate((float) _inflationChange, blendShapeCtrlList);                    
                
            #endif
        }


        /// <summary>
        /// Set the weights of all blendShapeControllers to allow quick and frequent mesh blendshape changes
        /// </summary>
        /// <param name="inflationWeight">The target inflation size, that will be used as a blendshape weight</param>
        /// <param name="bscList">The list of blendshapes which we will alter the weights</param>
        public void QuickInflate(float inflationWeight, List<BlendShapeController> bscList) 
        {
            foreach(var blendShapeCtrl in bscList)
            {
                var success = blendShapeCtrl.ApplyBlendShapeWeight(inflationWeight);
                if (!success && PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" QuickInflate > an smr was null, skipping mesh");
            }
        }


        /// <summary>
        /// When clothing changes in the middle of inflation, append it to the quick list so it will change shape with the other meshes
        /// </summary>
        public void AppendToQuickInflateList(SkinnedMeshRenderer smr)
        {
            var blendShapeName = MakeBlendShapeName(GetMeshKey(smr), blendShapeTempTagName);
            var blendshapeCtrl = new BlendShapeController(smr, blendShapeName);
            if (blendshapeCtrl.blendShape == null) return;

            blendShapeCtrlList.Add(blendshapeCtrl);
        }


        /// <summary>
        /// Reset inflation values between inflations, or a full reset when done with HScene
        /// </summary>
        /// <param name="fullReset">Used on HScne end to reset all temporary inflation stuff</param>   
        public void ClearInflationStuff(bool fullReset = false) 
        {
            timeElapsed = 0;
            _inflationStartSize = _inflationChange;            
            blendShapeCtrlList = new List<BlendShapeController>();

            if (fullReset)
            {
                _inflationStartSize = _inflationChange = TargetPregPlusSize = 0;
            }
        }


        /// <summary>
        /// Set a default belly shape for KK_Pregnancy integration, when one is not already set on the character's card
        /// </summary>
        public PregnancyPlusData GetDefaultShapeFor_KK_Pregnancy() 
        {
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" GetDefaultInflationShape>  Loading a Default belly shape");
            var customInfConfig = new PregnancyPlusData();
            customInfConfig.pluginVersion = PregnancyPlusPlugin.Version;
            
            #if KK
                //These values looked decent on most default characters, but they can always be changed.
                customInfConfig.inflationMultiplier = 0.3f;
                customInfConfig.inflationStretchX = -0.15f;
                customInfConfig.inflationTaperY = -0.02f;                
                customInfConfig.inflationTaperZ = -0.003f;
                customInfConfig.inflationShiftZ = -0.03f;
                customInfConfig.inflationDrop = 0.15f;

            #else
                customInfConfig.inflationMultiplier = 0.13f * 5;
                customInfConfig.inflationStretchX = -0.06f * 5;          
                customInfConfig.inflationStretchY = -0.02f * 5;
                customInfConfig.inflationTaperY = -0.018f * 5;            
                customInfConfig.inflationTaperZ = -0.02f * 5;
                customInfConfig.inflationShiftZ = -0.1f * 5;
                customInfConfig.inflationMoveZ = -0.1f * 5;
                customInfConfig.inflationDrop = 0.15f;

            #endif

            return customInfConfig;
        }

    }
}


