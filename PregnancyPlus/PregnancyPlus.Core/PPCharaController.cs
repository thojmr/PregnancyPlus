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

    //This partial class contains the characters properties, overrides, and events
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {        

        internal bool initialized = false;//Prevent some actions from happening before character data loads   

        public BellyInfo bellyInfo;
        public string charaFileName = null;
        public bool lastVisibleState = false;//Track last mesh render state, to determine when to re-apply preg+ shape in main game

        public PregnancyPlusBlendShapeGui blendShapeGui = new PregnancyPlusBlendShapeGui();

        //Holds the user entered slider values
        public PregnancyPlusData infConfig = new PregnancyPlusData();
        internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();        


        //Keeps track of all belly verticies, the dicts are indexed by the (meshRenderer.name + the vertex count) to make the indexes unique
        public Dictionary<string, Vector3[]> originalVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, Vector3[]> inflatedVertices = new Dictionary<string, Vector3[]>();//Max extended mesh verts, after all slider calculations        
        public Dictionary<string, Vector3[]> currentVertices = new Dictionary<string, Vector3[]>();//Currently active visible mesh verts
        public Dictionary<string, float[]> clothingOffsets = new Dictionary<string, float[]>();//The distance we want to offset each vertex fromt the body mesh when inflated
        public Dictionary<string, bool[]> bellyVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area
        public Dictionary<string, bool[]> alteredVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area and within the current belly radius
        public List<string> ignoreMeshList = new List<string>();//List of mesh names/keys to ignore since they dont have belly verts


        //For fetching uncensor body guid data (bugfix for uncensor body vertex positions)
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        public const string DefaultBodyFemaleGUID = "Default.Body.Female";
        public const string DefaultBodyMaleGUID = "Default.Body.Male";

        #if KK //key that allows us to pull KK_pregnancy data values
            public const string KK_PregnancyPluginName = "KK_Pregnancy";
        #elif AI
            public const string KK_PregnancyPluginName = "AI_Pregnancy";
        #elif HS2
            public const string KK_PregnancyPluginName = "";
        #endif

        internal Guid debounceGuid;//Track multiple events with a debounce based on these id's



#region overrides/hooks

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            //only allow saving card inside maker or studio
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) return;
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnCardBeingSaved ");
            SetExtendedData(infConfig.Save());
        }


        protected override void Start() 
        {  
            //Character card name used to detect switching characters  
            charaFileName = ChaFileControl.parameter.fullname;        
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $Start {charaFileName}");
            ReadAndSetCardData();      

            #if KK || AI

                GameAPI.StartH += (object sender, EventArgs e) => 
                { 
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $StartH {charaFileName}");
                    //Trigger inflation at current size to create any Preg+ blendshapes that may be used.  Kind of like like pre processing.
                    MeshInflate(infConfig.inflationSize, new MeshInflateFlags(this, _bypassWhen0: true));
                };

                //When HScene ends, clear any inflation data
                GameAPI.EndH += (object sender, EventArgs e) => 
                { 
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $EndH {charaFileName}");
                    clearInflationStuff(true);
                };
         
            #endif

            base.Start();
        }        


        //The HS2 / AI way to detect clothing change
        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnCoordinateBeingLoaded {coordinate.coordinateName}");
            OnCoordinateLoaded();

            base.OnCoordinateBeingLoaded(coordinate);
        }
        

        protected override void OnReload(GameMode currentGameMode)
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnReload {currentGameMode}"); 
            lastVisibleState = false;
            ClearOnReload();
            #if AI || HS2
                //Fix for the way AI addds new characters by copying existing character first.  This will remove the old blendshapes.
                ScrubBlendShapes();
            #endif

            //Check for swapping out character Game Object with new character
            var isNewCharFile = IsNewChar(ChaFileControl);
            charaFileName = ChaFileControl.parameter.fullname;

            ReadAndSetCardData();

            StartCoroutine(ReloadStoryInflation(0.5f));     
            StartCoroutine(ReloadStudioMakerInflation(1.5f));  //Give time for character to load, and settle  
        }


        protected override void Update()
        {
            WatchForUserKeyPress();

            //just for debugging belly during animations, very compute heavy for Update()
            if (PregnancyPlusPlugin.DebugAnimations.Value)
            {
                if (Time.frameCount % 60 == 0) MeasureWaistAndSphere(ChaControl, true);
                if (Time.frameCount % 60 == 0) MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true));
            }

            ComputeInflationChange();
        }


        protected override void OnDestroy() {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnDestroy {charaFileName}"); 
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
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" IsNewChar {charaFileName} -> {chaFileControl.parameter.fullname}"); 
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
            
            #if KK || AI
                GetWeeksAndSetInflation(true);  

            #elif HS2
                //For HS2 AI, we set global belly size from plugin config, or character card                    
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true));   

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
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true));    
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

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMakerLoad done, setting initial sliders");         
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
                    MeshInflate(TargetPregPlusSize);
                }
                else
                {
                    //Increase size by 2
                    var newVal = infConfig.inflationSize + 2;
                    MeshInflate(newVal);                
                }
            }

            if (PregnancyPlusPlugin.StoryModeInflationDecrease.Value.IsDown()) 
            {
                if (isDuringInflationScene) 
                {
                    TargetPregPlusSize -= 2;
                    _inflationChange = TargetPregPlusSize;
                    MeshInflate(TargetPregPlusSize);
                }
                else
                {
                    var newVal = infConfig.inflationSize - 2;
                    MeshInflate(newVal);
                }
            }

            if (PregnancyPlusPlugin.StoryModeInflationReset.Value.IsDown()) 
            {
                //reset size
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

            // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= ClothesStateChangeEvent {clothesKind}");

            #if KK
                var debounceTime = 0.1f;
            #elif HS2 || AI
                var debounceTime = 0.15f;
            #endif
            //Force recalc because of some cloth items in HS2 Maker that don't seem to want to follow the rules
            StartCoroutine(WaitForMeshToSettle(debounceTime, true, forceRecalcVerts));
        }


        /// <summary>
        /// Check whether the characters visibility state has changed, via chaControl hook
        /// </summary>
        internal void CheckVisibilityState(bool newState)
        {
            //If the character was already visible, ignore this until next reload
            if (lastVisibleState) return;
            if (!newState) return;

            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= CheckVisibilityState {charaFileName} {newState}");

            lastVisibleState = true;

            //Re trigger mesh inflation when character first becomes visible
            MeshInflate(new MeshInflateFlags(this, _visibilityUpdate: true));
        }

        
        /// <summary>
        /// Get card data and update this characters infConfig with it
        /// </summary>
        internal void ReadAndSetCardData()
        {
            infConfig = GetCardData();
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ReadAndSetCardData > {infConfig.ValuesToString()}");
            
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
            if (debounceGuid == guid) 
            {
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" WaitForMeshToSettle checkNewMesh:{checkNewMesh} forceRecalcVerts:{forceRecalcVerts}");        
                CheckMeshVisibility(); 
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: checkNewMesh, _freshStart: forceRecalcVerts));
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

    }
}


