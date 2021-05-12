using KKAPI.Chara;
using UnityEngine;
using KKAPI.Maker;
using KKAPI.Studio;
using System.Collections;
using System.Collections.Generic;
#if HS2
    using AIChara;
#elif AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the methods that are called after harmony hooks fire
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {        

        /// <summary>
        /// Check whether the characters visibility state has changed
        /// </summary>
        internal void CheckVisibilityState(bool newState)
        {
            //If the character was already visible, ignore this until next reload
            if (lastVisibleState) return;
            if (!newState) return;            

            lastVisibleState = true;

            //Only for main game
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;
            if (isReloading) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= CheckVisibilityState {charaFileName} {newState}");
            
            //Re trigger mesh inflation when character first becomes visible
            MeshInflate(new MeshInflateFlags(this, _visibilityUpdate: true), "CheckVisibilityState");
        }


        /// <summary>
        /// Triggered when clothing state is changed, i.e. pulled aside or taken off.
        /// </summary>
        internal void ClothesStateChangeEvent(int chaID, int clothesKind)
        {
            //Wait for card data to load, and make sure this is the same character the clothes event triggered for
            if (!initialized || chaID != ChaControl.chaID) return;

            // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= ClothesStateChangeEvent {clothesKind}");

            #if KK
                var debounceTime = 0.1f;
            #elif HS2 || AI
                var debounceTime = 0.15f;
            #endif

            //Force recalc because of some cloth items in HS2 Maker that don't seem to want to follow the rules
            StartCoroutine(WaitForClothMeshToSettle(debounceTime, checkNewMesh: true));
        }


        /// <summary>
        /// Triggered when Uncensor changed body mesh
        /// </summary>
        internal void OnUncensorChanged() 
        {
            if (ignoreNextUncensorHook)
            {
                ignoreNextUncensorHook = false;
                return;
            }
            
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= OnUncensorChanged {charaFileName}");
            //When first loading character, we dont care about uncensor changes
            if (!initialized) return;

            //Flag that the uncensor changed, so after Reload() is triggered we can do somehting about it
            uncensorChanged = true;     

            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) return;
            //Try to determine if user triggered uncensor change
            StartCoroutine(UserTriggeredUncensorChange());    
        }

    }
}


