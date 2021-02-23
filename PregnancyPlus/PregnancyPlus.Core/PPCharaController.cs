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
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the property declarations and override hooks
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {        

        internal bool initialized = false;//Prevent some actions from happening before character data loads   

        public BellyInfo bellyInfo;
        public string charaFileName = null;

        public PregnancyPlusBlendShapeGui blendShapeGui = new PregnancyPlusBlendShapeGui();

        //Holds the user entered slider values
        public PregnancyPlusData infConfig = new PregnancyPlusData();
        internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();        


        //Keeps track of all belly verticies, the dicts are indexed by the (meshRenderer.name + the vertex count) to make the indexes unique
        public Dictionary<string, Vector3[]> originalVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, Vector3[]> inflatedVertices = new Dictionary<string, Vector3[]>();//Max extended mesh verts, after all slider calculations
        public Dictionary<string, Vector3[]> currentVertices = new Dictionary<string, Vector3[]>();//Currently active visible mesh verts
        public Dictionary<string, bool[]> bellyVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area
        public Dictionary<string, bool[]> alteredVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area and within the current belly radius



        //For fetching uncensor body guid data (bugfix for uncensor body vertex positions)
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        public const string DefaultBodyFemaleGUID = "Default.Body.Female";
        public const string DefaultBodyMaleGUID = "Default.Body.Male";

        public const string KK_PregnancyPluginName = "KK_Pregnancy";//key that allows us to pull KK_pregnancy data values

        internal Guid debounceGuid;//Track multiple events with a debounce based on these id's



#region overrides/hooks

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            //only allow saving card inside maker or studio
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) return;
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnCardBeingSaved ");
            SetExtendedData(infConfig.Save());
        }


        protected override void Start() 
        {                            
            charaFileName = ChaFileControl.parameter.fullname;        
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= $Start {charaFileName}");
            ReadAndSetCardData();                       

            //Get the char measurements before they have a chance to move
            var success = MeasureWaistAndSphere(ChaControl);
            if (!success)
            {
                PregnancyPlusPlugin.errorCodeCtrl.LogErrorCode(ChaControl.chaID, ErrorCode.PregPlus_BadMeasurement, 
                    $"Start(): Could not get belly measurements from character");
            }

            base.Start();
        }


        //The HS2 / AI way to detect clothing change
        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate) 
        {
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnCoordinateBeingLoaded {coordinate.coordinateName}");
            OnCoordinateLoaded();

            base.OnCoordinateBeingLoaded(coordinate);
        }
        

        protected override void OnReload(GameMode currentGameMode)
        {
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnReload {currentGameMode}"); 
            ClearOnReload();

            //Check for swapping out character GO with new character, because we want to keep the current slider values
            var isNewCharFile = IsNewChar(ChaFileControl);
            charaFileName = ChaFileControl.parameter.fullname;

            ReadAndSetCardData();

            StartCoroutine(ReloadStoryInflation(0.5f));     
            StartCoroutine(ReloadStudioMakerInflation(0.5f));    
        }


        protected override void Update()
        {
            WatchForUserKeyPress();

            //just for debugging, pretty compute heavy for Update()
            if (PregnancyPlusPlugin.DebugAnimations.Value)
            {
                if (Time.frameCount % 60 == 0 && PregnancyPlusPlugin.debugLog) MeasureWaistAndSphere(ChaControl, true);
                if (Time.frameCount % 60 == 0 && PregnancyPlusPlugin.debugLog) MeshInflate(true, true);
            }
        }


        protected override void OnDestroy() {
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnDestroy {charaFileName}"); 
        }
        

#endregion overrides/hooks


        /// <summary>
        /// Some additional props that need to be cleared when laoding new character
        /// </summary>
        public void ClearOnReload()
        {
            meshWithBlendShapes = new List<SkinnedMeshRenderer>();
            blendShapeGui.CloseBlendShapeGui();
        }


        /// <summary>
        /// True when OnReload was triggerd by replacing the current character GameObject with another character file
        ///  We want to keep current slider settings when this happens
        /// </summary>
        internal bool IsNewChar(ChaFileControl chaFileControl) 
        {   var isNew = (charaFileName != chaFileControl.parameter.fullname);
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" IsNewChar {charaFileName} -> {chaFileControl.parameter.fullname}"); 
            return isNew;
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Story mode
        /// </summary>
        internal IEnumerator ReloadStoryInflation(float time)
        {
            yield return new WaitForSeconds(time);

            //Only reload when story mode enabled.
            if (PregnancyPlusPlugin.StoryMode != null && !PregnancyPlusPlugin.StoryMode.Value) yield break;            
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) yield break;
            
            #if KK
                GetWeeksAndSetInflation(true);                                 
            #elif HS2 || AI                  
                //For HS2 AI, we set global belly size from plugin config, or character card                    
                MeshInflate(true);                                       
            #endif                                           
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Studio or Maker
        /// </summary>
        internal IEnumerator ReloadStudioMakerInflation(float time)
        {                        
            yield return new WaitForSeconds(time);
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) yield break;   

            if (StudioAPI.InsideStudio || (MakerAPI.InsideMaker && MakerAPI.InsideAndLoaded))
            {
                //If either are fully loaded, start mesh inflate
                MeshInflate(true, true);    
            }
            else if (MakerAPI.InsideMaker && !MakerAPI.InsideAndLoaded)
            {
                StartCoroutine(WaitForMakerLoad());
            }
            
        }


        /// <summary>
        /// When maker is not loaded, but is loading, wait for it before setting belly sliders (Only needed on maker first load)
        /// </summary>
        internal IEnumerator WaitForMakerLoad()
        {
            while (!MakerAPI.InsideAndLoaded)
            {
                yield return new WaitForSeconds(0.01f);
            }

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMakerLoad done, setting initial sliders");         
            //Restore sliders to current state
            PregnancyPlusGui.OnRestore(PregnancyPlusGui.sliders, GetCardData());
        }


        /// <summary>
        /// Watch for user keypressed to trigger belly infaltion + or -
        /// </summary>
        internal void WatchForUserKeyPress() 
        {
            //When the user presses a key combo they set, it increases or decreases the belly inflation amount, only for story mode
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;        

            //Only continue if body is rendered on screen  (Dont want to do every loaded char)
            #if KK
                if (ChaControl.rendBody == null || !ChaControl.rendBody.isVisible) return;
            #elif HS2 || AI
                if (ChaControl.cmpBody == null || !ChaControl.cmpBody.isVisible) return;
            #endif
            
            if (PregnancyPlusPlugin.StoryModeInflationIncrease.Value.IsDown()) 
            {
                var newVal = infConfig.inflationSize + 2;
                MeshInflate(newVal);                
            }

            if (PregnancyPlusPlugin.StoryModeInflationDecrease.Value.IsDown()) 
            {
                var newVal = infConfig.inflationSize - 2;
                MeshInflate(newVal);
            }

            if (PregnancyPlusPlugin.StoryModeInflationReset.Value.IsDown()) 
            {
                MeshInflate(0);
            }            
        }


        /// <summary>
        /// Triggered when clothing state is changed, i.e. pulled aside or taken off.
        /// </summary>
        internal void ClothesStateChangeEvent(int chaID, int clothesKind, bool forceRecalcVerts = false)
        {
            //Wait for card data to load, and make sure this is the same character the clothes event triggered for
            if (!initialized || chaID != ChaControl.chaID) return;

            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($"+= ClothesStateChangeEvent {clothesKind}");

            #if KK
                var debounceTime = 0.1f;
            #elif HS2 || AI
                var debounceTime = 0.2f;
            #endif
            //Force recalc because of some cloth items in HS2 Maker that don't seem to want to follow the rules
            StartCoroutine(WaitForMeshToSettle(debounceTime, true, forceRecalcVerts));
        }

        
        /// <summary>
        /// Get card data and update this characters infConfig with it
        /// </summary>
        internal void ReadAndSetCardData()
        {
            infConfig = GetCardData();
            if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" ReadAndSetCardData > {infConfig.ValuesToString()}");
            
            //Load any blendshapes from card
            LoadBlendShapes(infConfig);
            initialized = true; 
        }


        /// <summary>
        /// Just fetch the data for comparison
        /// </summary>
        internal PregnancyPlusData GetCardData()
        {
            var data = GetExtendedData();
            return PregnancyPlusData.Load(data) ?? new PregnancyPlusData();
        }


        /// <summary>
        /// When clothing changes, need to recalculate inflation on that clothing
        /// </summary>
        internal void OnCoordinateLoaded()  
        {  
            //No loading coordinate changes before the pregnancydata values are fetched
            if (!initialized) return;

            //When clothing changes, reload inflation state
            StartCoroutine(WaitForMeshToSettle(0.05f, true));
        } 

        
        /// <summary>
        /// After clothes change you have to wait a second if you want mesh shadows to calculate correctly (longer in HS2, AI)
        /// </summary>
        internal IEnumerator WaitForMeshToSettle(float waitTime = 0.05f, bool checkNewMesh = false, bool forceRecalcVerts = false)
        {   
            //Allows us to debounce when multiple back to back request
            var guid = Guid.NewGuid();
            debounceGuid = guid;

            yield return new WaitForSeconds(waitTime);
            //If guid is the latest, trigger method
            if (debounceGuid == guid) {
                if (PregnancyPlusPlugin.debugLog)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMeshToSettle checkNewMesh:{checkNewMesh} forceRecalcVerts:{forceRecalcVerts}");
                MeshInflate(checkNewMesh, forceRecalcVerts);
            }
        }

        
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
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" GetWeeksAndSetInflation {ChaControl.name} >  Week:{week} checkNewMesh:{checkNewMesh} slidersChanged:{slidersChanged}");
            if (week < 0) return;

            //Compute the additonal belly size added based on user configured vallue from 0-40
            var additionalPregPlusSize = Mathf.Lerp(0, week, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);

            MeshInflate(additionalPregPlusSize, checkNewMesh, slidersChanged);
        }
        

    }
}


