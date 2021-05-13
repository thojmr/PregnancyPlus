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
        public bool uncensorChanged = false;
        public bool isReloading = false;//While character.Reload() is processing prevent other MeshInflate() instances
        internal bool ignoreNextUncensorHook = false;//When we want to ignore a single uncensor hook event
        internal string initialUncensorGUID;//Track the original guid when one is not present on saved blendshape, but the mesh matches the bledshaape

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

            CaptureNewBlendshapeWeights();
            SetExtendedData(infConfig.Save());
        }


        protected override void Start() 
        {  
            uncensorChanged = false;//reset value to signify its not a change made manually by the user
            
            //Character card name 
            charaFileName = ChaFileControl.parameter.fullname;        
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $Start {charaFileName}");
            ReadAndSetCardData();      

            #if KK || AI

                //When HScene starts, pre compute inflated size blendshape
                GameAPI.StartH += (object sender, EventArgs e) => 
                { 
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $StartH {charaFileName}");
                    //Trigger inflation at current size to create any Preg+ blendshapes that may be used.  Kind of like like pre processing.
                    MeshInflate(infConfig.inflationSize, "GameAPI.StartH", new MeshInflateFlags(this, _bypassWhen0: true));
                };

                //When HScene ends, clear any inflation data
                GameAPI.EndH += (object sender, EventArgs e) => 
                { 
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $EndH {charaFileName}");
                    ClearInflationStuff(true);
                };
         
            #endif

            // CharacterApi.CharacterReloaded += (object sender, CharaReloadEventArgs e) =>  
            // {  
            //     if (e.ReloadedCharacter == null || e.ReloadedCharacter.name != ChaControl.name) return;            
            //     if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= OnCharacterReloaded ");
            // };

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
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnReload {currentGameMode}");
            isReloading = true;
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

            // When changing a character (swapping in place) in studio carry over belly sliders/blendshapes
            //TODO there has to be a better way to detect swapping characters
            if (StudioAPI.InsideStudio && !infConfig.HasAnyValue() && infConfigHistory.HasAnyValue())
            {
                if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" -Character changed in place, preserving belly shape");
                infConfig = infConfigHistory;
            }

            //If the uncensor changed just before this Reload(), then is was probably a character swap.
            if (uncensorChanged)
            {
                uncensorChanged = false;

                //When in maker or studio we want to reset inflation values when uncensor changes to reset clothes
                if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) ResetInflation();        
                //Load any saved blendshapes from card, and can trigger uncensor change when necessary
                //Give any existing uncensor changes time to process first, incase we need to auto swap uncensors
                StartCoroutine(ILoadBlendshapes(0.1f, true));
            }   
            else 
            {
                //Load any saved blendshapes from card, and can trigger uncensor change when necessary
                LoadBlendShapes(infConfig);
            }         

            StartCoroutine(ReloadStoryInflation(0.5f, "Reload-story"));     
            StartCoroutine(ReloadStudioMakerInflation(1.5f, reMeasure: true, "Reload"));  //Give time for character to load, and settle  
        }


        protected override void Update()
        {
            WatchForUserKeyPress();
            ComputeInflationChange();

            //just for debugging belly during animations, very compute heavy for Update()
            if (PregnancyPlusPlugin.DebugAnimations.Value)
            {
                if (Time.frameCount % 60 == 0) MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _reMeasure: true), "Update");
            }
        }


        protected override void OnDestroy() 
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnDestroy {charaFileName}"); 
        }
        

#endregion overrides/hooks


        /// <summary>
        /// Whhen we want to delay loading blendshapes
        /// </summary>
        internal IEnumerator ILoadBlendshapes(float waitforSeconds, bool checkUncensor = false) 
        {   
            yield return new WaitForSeconds(waitforSeconds);
            yield return new WaitForEndOfFrame();
            LoadBlendShapes(infConfig, checkUncensor);
        }


        /// <summary>
        /// Some additional props that need to be cleared when laoding new character
        /// </summary>
        public void ClearOnReload()
        {
            meshWithBlendShapes = new List<MeshIdentifier>();
            blendShapeGui.CloseBlendShapeGui();
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

            StartCoroutine(ReloadStoryInflation(0.5f, "OnUncensorChanged-story"));     
            StartCoroutine(ReloadStudioMakerInflation(1f, reMeasure: false, "OnUncensorChanged"));  //Give time for character to load, and settle 
        }


        /// <summary>
        /// True when OnReload was triggerd by replacing the current character GameObject with another character file
        ///  We want to keep current slider settings when this happens
        /// </summary>
        internal bool IsNewChar(ChaFileControl chaFileControl) 
        {   
            var isNew = (charaFileName != chaFileControl.parameter.fullname);
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" IsNewChar {charaFileName} -> {chaFileControl.parameter.fullname}"); 
            return isNew;
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Story mode
        /// </summary>
        internal IEnumerator ReloadStoryInflation(float time, string callee)
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
            yield return new WaitForEndOfFrame();

            #if KK || AI
                GetWeeksAndSetInflation(true);  

            #elif HS2
                //For HS2 AI, we set global belly size from plugin config, or character card                    
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true), callee);   

            #endif      
            isReloading = false;                                     
        }


        /// <summary>
        /// Triggered by OnReload but only for logic in Studio or Maker
        /// </summary>
        internal IEnumerator ReloadStudioMakerInflation(float time, bool reMeasure, string callee)
        {                        
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) 
            {
                yield break;   
            }

            yield return new WaitForSeconds(time);
            //Waiting until end of frame lets bones settle so we can take accurate measurements
            yield return new WaitForEndOfFrame();

            if (StudioAPI.InsideStudio || (MakerAPI.InsideMaker && MakerAPI.InsideAndLoaded))
            {
                //If either are fully loaded, start mesh inflate
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _reMeasure: true), callee);    
                isReloading = false;//Allow cloth mesh events to continue triggering MeshInflate
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
            StartCoroutine(WaitForClothMeshToSettle(0.05f, true));
        } 

        
        /// <summary>
        /// After clothes change you have to wait a second if you want mesh shadows to calculate correctly (longer in HS2, AI)
        /// </summary>
        internal IEnumerator WaitForClothMeshToSettle(float waitTime = 0.05f, bool checkNewMesh = false, bool forceRecalcVerts = false)
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
                yield return new WaitForEndOfFrame();
                MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: checkNewMesh, _freshStart: forceRecalcVerts), "WaitForClothMeshToSettle");
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


