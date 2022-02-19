using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KKAPI.Maker;
using KKAPI.Studio;
#if KK
    using KKAPI.MainGame;
#elif HS2
    using AIChara;
#elif AI
    using KKAPI.MainGame;
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the methods triggered by PregnancyPlusCharaController Overrides
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {        

        /// <summary>
        /// When we want to delay loading blendshapes so uncensor can finish
        /// </summary>
        internal IEnumerator ILoadBlendshapes(float waitforSeconds, bool checkUncensor = false) 
        {   
            yield return new WaitForSeconds(waitforSeconds);
            if (!infConfig.UseOldCalcLogic()) yield return new WaitForEndOfFrame();
            LoadBlendShapes(infConfig, checkUncensor);
        }


        /// <summary>
        /// Some additional props that need to be cleared when laoding new character
        /// </summary>
        public void ClearOnReload()
        {
            meshWithBlendShapes = new List<MeshIdentifier>();
            blendShapeGui.CloseBlendShapeGui();
            PregnancyPlusGui.StopTextCountIncrement();
            lastMeshInflateFlags = null;
            isProcessing = false;

            #if DEBUG
                DebugTools.ClearAllThingsFromCharacter();
            #endif
        }


        /// <summary>
        /// After Uncensor changes, if Reload() is not called within the time below, try to reload the blendshape manually. Since we know the change was from user interacing with dropdown
        /// </summary>
        internal IEnumerator UserTriggeredUncensorChange() 
        {
            yield return new WaitForSeconds(0.1f);
            //If Reload() was already called the blendshape stuff is already taken care of. skip the rest of this
            if (!uncensorChanged) yield break;

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" -UserTriggeredUncensorChange");
            uncensorChanged = false;

            ClearOnReload();

            //When in maker or studio we want to reset inflation values when uncensor changes to reset clothes
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) ResetInflation();

            //Load any blendshapes from card.  If the uncensor matches the blendshapes they will load to the character visibly
            LoadBlendShapes(infConfig);

            StartCoroutine(ReloadStoryInflation(0.5f, "OnUncensorChanged-story", uncensorChanged: true));     
            StartCoroutine(ReloadStudioMakerInflation(1f, reMeasure: false, "OnUncensorChanged", uncensorChanged: true));  //Give time for character to load, and settle 
        }


        /// <summary>
        /// True when OnReload was triggerd by replacing the current character GameObject with another character file
        ///  We want to keep current slider settings when this happens
        /// </summary>
        internal bool IsNewChar(ChaFileControl chaFileControl) 
        {   
            var isNew = (charaFileName != chaFileControl.parameter.fullname);
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" IsNewChar {charaFileName} -> {chaFileControl.parameter.fullname} "); 
            return isNew;
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Story mode
        /// </summary>
        internal IEnumerator ReloadStoryInflation(float time, string callee, bool checkNewMesh = true, bool uncensorChanged = false)
        {
            //Only reload when story mode enabled.
            if (PregnancyPlusPlugin.StoryMode != null && !PregnancyPlusPlugin.StoryMode.Value) 
            {
                isReloading = false;
                yield break;         
            }   
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) 
            {
                yield break;
            }

            yield return new WaitForSeconds(time);
            //Waiting until end of frame lets bones settle so we can take accurate measurements
            if (!infConfig.UseOldCalcLogic()) yield return new WaitForEndOfFrame();

            #if KK || AI
                GetWeeksAndSetBellySize(checkNewMesh: checkNewMesh, callee: callee);  

            #elif HS2
                //For HS2 AI, we set global belly size from plugin config, or character card                    
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: checkNewMesh, _uncensorChanged: uncensorChanged), callee);   

            #endif      
            isReloading = false;                                     
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Studio or Maker
        /// </summary>
        internal IEnumerator ReloadStudioMakerInflation(float time, bool reMeasure, string callee, bool uncensorChanged = false)
        {                        
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) 
            {
                yield break;   
            }

            yield return new WaitForSeconds(time);
            //Waiting until end of frame lets bones settle so we can take accurate measurements
            if (!infConfig.UseOldCalcLogic()) yield return new WaitForEndOfFrame();

            if (StudioAPI.InsideStudio || (MakerAPI.InsideMaker && MakerAPI.InsideAndLoaded))
            {
                //If either are fully loaded, start mesh inflate
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _reMeasure: true, _uncensorChanged: uncensorChanged), callee);    
                isReloading = false;//Allow cloth mesh events to continue triggering MeshInflate
            }
            else if (MakerAPI.InsideMaker && !MakerAPI.InsideAndLoaded)
            {
                StartCoroutine(WaitForMakerLoad());
            }
        }


        /// <summary>
        /// When maker is not fully loaded yet, wait for it before setting belly sliders (Only needed on first load)
        /// </summary>
        internal IEnumerator WaitForMakerLoad()
        {
            while (!MakerAPI.InsideAndLoaded)
            {
                yield return new WaitForSeconds(0.01f);
            }

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMakerLoad done, setting initial sliders");         
            //Restore sliders to current state
            PregnancyPlusGui.OnPasteBelly(PregnancyPlusGui.sliders, GetCardData());
            yield return null;
            isReloading = false;//Allow cloth mesh events to continue triggering MeshInflate
        }


        /// <summary>
        /// Watch for user keypressed to trigger belly infaltion manually
        /// </summary>
        internal void WatchForUserKeyPress() 
        {
            //When the user presses a key combo they set, it increases or decreases the belly inflation amount, only for story mode
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;        

            //Only if body is rendered on screen  (Dont want to do every internally loaded char)
            #if KK
                if (ChaControl.rendBody == null || !ChaControl.rendBody.isVisible) return;
            #elif HS2 || AI
                if (ChaControl.cmpBody == null || !ChaControl.cmpBody.isVisible) return;
            #endif
            
            if (PregnancyPlusPlugin.StoryModeInflationIncrease.Value.IsDown()) 
            {
                if (isDuringInflationScene) 
                {
                    //Need special logic to append size to the TargetPregPlusSize during inflation scene
                    //Increase size by 2
                    TargetPregPlusSize += 2;
                    _inflationChange = TargetPregPlusSize;
                    MeshInflate(TargetPregPlusSize, "WatchForUserKeyPress");
                }
                else
                {
                    //Increase size by 2
                    var newVal = infConfig.inflationSize + 2;
                    MeshInflate(newVal, "WatchForUserKeyPress");                
                }
            }

            if (PregnancyPlusPlugin.StoryModeInflationDecrease.Value.IsDown()) 
            {
                if (isDuringInflationScene) 
                {
                    TargetPregPlusSize -= 2;
                    _inflationChange = TargetPregPlusSize;
                    MeshInflate(TargetPregPlusSize, "WatchForUserKeyPress");
                }
                else
                {
                    var newVal = infConfig.inflationSize - 2;
                    MeshInflate(newVal, "WatchForUserKeyPress");
                }
            }

            if (PregnancyPlusPlugin.StoryModeInflationReset.Value.IsDown()) 
            {
                //reset size
                MeshInflate(0, "WatchForUserKeyPress");
            }            
        }

        
        /// <summary>
        /// Get card data and update this characters infConfig with it
        /// </summary>
        internal void ReadAndSetCardData()
        {
            infConfig = GetCardData();
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ReadAndSetCardData > {infConfig.ValuesToString()}");
                        
            initialized = true; 
        }


        /// <summary>
        /// Read the card data
        /// </summary>
        internal PregnancyPlusData GetCardData()
        {
            var data = GetExtendedData();
            var pregCardData = PregnancyPlusData.Load(data);

            //When new card, no data will be set
            if (pregCardData == null)
            {
                pregCardData = new PregnancyPlusData();
                pregCardData.pluginVersion = PregnancyPlusPlugin.Version;
            }
            
            return pregCardData;
        }


        /// <summary>
        /// When clothing changes, need to recalculate inflation on that clothing
        /// </summary>
        internal void OnCoordinateLoaded()  
        {  
            //No loading coordinate changes before the pregnancydata values are fetched
            if (!initialized) return;

            //When clothing changes, reload inflation state
            StartCoroutine(WaitForClothMeshToSettle(0.05f, checkNewClothMesh: true));
        } 

        
        /// <summary>
        /// After clothes change you have to wait a second if you want mesh shadows to calculate correctly (longer in HS2, AI)
        /// </summary>
        internal IEnumerator WaitForClothMeshToSettle(float waitTime = 0.05f, bool checkNewClothMesh = false, bool forceRecalcVerts = false, bool checkNewAccessoryMesh = false, int identifier = -1)
        {   
            //Allows us to debounce when there are multiple back to back request
            var guid = Guid.NewGuid();
            debounceGuid = guid;

            yield return new WaitForSeconds(waitTime);
            yield return new WaitWhile(() => isReloading);
            yield return new WaitForSeconds(0.1f);

            //If guid is the latest, trigger method
            if (debounceGuid == guid)
            {
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMeshToSettle checkNewMesh:{checkNewMesh} forceRecalcVerts:{forceRecalcVerts}");        
                CheckMeshVisibility(); 
                if (!infConfig.UseOldCalcLogic()) yield return new WaitForEndOfFrame();

                //Make deug string to let us know what accessory slot, or cloth kind changed to trigger this update
                var clothItemChanged = identifier >= 0 ? $": {identifier}" : "N/A";
                var callee = $"WaitForMeshToSettle({(checkNewClothMesh ? "kind" : "slot") + clothItemChanged})";

                MeshInflate(new MeshInflateFlags(this, _checkForNewClothMesh: checkNewClothMesh, _checkForNewAccMesh: checkNewAccessoryMesh, _freshStart: forceRecalcVerts), callee);                
            }
        }


        /// <summary>
        /// Check whether the body mesh is currently rendered.  If any mesh changes were made while un-rendered, flag for re-apply belly on next visibility change
        /// </summary>
        internal void CheckMeshVisibility() 
        {
            #if KK
                if (ChaControl.rendBody == null || !ChaControl.rendBody.isVisible)  
                {
                    // PregnancyPlusPlugin.Logger.LogInfo($" chaControl.rendBody not visible for {charaFileName}");
                    lastVisibleState = false;
                }

            #elif HS2 || AI

                if (ChaControl.cmpBody == null || !ChaControl.cmpBody.isVisible) 
                {
                    // PregnancyPlusPlugin.Logger.LogInfo($" chaControl.rendBody not visible for {charaFileName}");
                    lastVisibleState = false;
                }
            #endif
        }


        /// <summary>
        /// When changing a character (swapping in place) in studio/maker, carry over belly sliders/blendshapes
        /// </summary>
        internal void CheckBellyPreservation()
        {
            //TODO there has to be a better way to detect swapping characters?
            //When in studio and the source character has a belly
            if (StudioAPI.InsideStudio && (infConfigHistory.HasAnyValue() || infConfigHistory.HasBlendShape()))
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" -Character changed in place, using source's belly shape");
                //Keep source character's card data (in case we are swapping out characters in a preg based scene)
                infConfig = infConfigHistory;
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" Updating Card Data > {infConfig.ValuesToString()}");            
            }


            //When in maker and the source character has a belly
            if (MakerAPI.InsideMaker && (infConfigHistory.HasAnyValue() || infConfigHistory.HasBlendShape()))
            {
                //Decide whether to preserve the belly based on user configured value
                if (PregnancyPlusPlugin.PerferTargetBelly.Value)
                {
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" -Character changed in place, using target's belly shape");   
                    //Use the target belly shape             
                } 
                else 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" -Character changed in place, using source's belly shape");
                    //Keep source character's card data
                    infConfig = infConfigHistory;
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" Updating Card Data > {infConfig.ValuesToString()}");
                }
            }
        }


        /// <summary>
        /// When loading or changing characters, check for any save blendshapes that need to be loaded from the character card
        /// </summary>
        internal void CheckBlendShapes() 
        {
            //If the uncensor changed just before this Reload(), then is was probably a character swap.
            if (uncensorChanged)
            {
                uncensorChanged = false;
                
                //When in maker or studio we want to reset inflation values when uncensor changes to reset clothes
                if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) ResetInflation();        
                //Load any saved blendshapes from card, and can trigger uncensor change when necessary
                //Give any existing uncensor changes time to process first
                StartCoroutine(ILoadBlendshapes(0.1f, checkUncensor: true));
            }   
            else 
            {
                //When character first loads in studio, always check for uncensor match if a saved blendshape exists
                var needsUncensorCheck = firstStart && (StudioAPI.InsideStudio || MakerAPI.InsideMaker);
                
                //Load any saved blendshapes from card, or trigger uncensor change when necessary
                StartCoroutine(ILoadBlendshapes(0f, checkUncensor: needsUncensorCheck));
            } 
        }

    }
}


