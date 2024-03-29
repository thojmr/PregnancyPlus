﻿using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KKAPI.Maker;
using KKAPI.Studio;

#if KKS
    using KKAPI.MainGame;
#elif HS2
    using AIChara;
#elif AI
    using KKAPI.MainGame;
    using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the characters properties, fields, and overrides
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {        

        internal bool initialized = false;//Prevent some actions from happening before character data loads   
        internal bool firstStart = true;//When this class just loaded for the first time
        internal bool isProcessing = false;//When the MeshInflate logic is currently computing the mesh shape
        public BellyInfo bellyInfo;
        public string charaFileName = null;
        public bool lastVisibleState = false;//Track last mesh render state, to determine when to re-apply preg+ shape in main game
        public bool uncensorChanged = false;
        public bool isReloading = false;//While character.Reload() is processing prevent other MeshInflate() instances
        internal bool ignoreNextUncensorHook = false;//When we want to ignore a single uncensor hook event
        public PregnancyPlusBlendShapeGui blendShapeGui = new PregnancyPlusBlendShapeGui();
        public PregnancyPlusClothOffsetGui clothOffsetGui = new PregnancyPlusClothOffsetGui();

        //Holds the user entered slider values
        public PregnancyPlusData infConfig = new PregnancyPlusData();
        internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();      
        //Perserves cloth coefficients so they are not lost when appening blendshapes.  Seems like a Unity bug
        internal Dictionary<string, ClothSkinningCoefficient[]> preservedClothCoefficients = new Dictionary<string, ClothSkinningCoefficient[]>();      
        internal Dictionary<string, int> coefficientCounter = new Dictionary<string, int>();      
        internal MeshInflateFlags lastMeshInflateFlags = null;   


        //Keeps track of all belly verticie data for preg+, the dict is indexed by the (meshRenderer.name + the vertex count) to make the mesh indexes unique
        public Dictionary<string, MeshData> md = new Dictionary<string, MeshData>();
        public List<string> ignoreMeshList = new List<string>();//List of mesh names/keys to ignore since they dont have belly verts
        public BindPoseList bindPoseList;

        //For fetching uncensor body guid data (bugfix for uncensor body vertex positions)
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        public const string DefaultBodyFemaleGUID = "Default.Body.Female";
        public const string DefaultBodyMaleGUID = "Default.Body.Male";

        //key that allows us to pull KK_pregnancy data values
        #if KKS
            public const string KK_PregnancyPluginName = "KK_Pregnancy";
        #elif AI
            public const string KK_PregnancyPluginName = "AI_Pregnancy";
        #elif HS2
            public const string KK_PregnancyPluginName = "";
        #endif

        internal Guid debounceGuid;//Track multiple events with a debounce based on these id's

        //The list of bones that preg+ uses to determine if a mesh has vert weights to the belly area.  Ignoring any meshes that dont touch these bones.
        #if KKS            
            public static string[] bellyBones = { "cf_s_spine02", "cf_s_waist01", "cf_s_waist02" };//"cs_s_spine01" optionally for wider affected area
        #elif HS2 || AI
            public static string[] bellyBones =  { "cf_J_Spine02_s", "cf_J_Kosi01_s", "cf_J_Kosi02_s" };
        #endif

        //Determine body mesh name based on sex (0 is male)
        public string BodyMeshName {
            #if KKS
                get { return ChaControl.sex == 0 ?  "o_body_a" : "o_body_a"; }
            #elif HS2 || AI
                get { return ChaControl.sex == 0 ?  "o_body_cm" : "o_body_cf"; }
            #endif            
        }

        //This detour override the mesh canAccess state to make it readable/accessable
        public NativeDetourMesh nativeDetour;

        //Careful that none of these are threadsafe for mesh calculations, need to make ThreadsafeCurve copy first        
        public AnimationCurve FastLerpAC = AnimCurve.FastLerpAnimCurve();
        public AnimationCurve SlowLerpAC = AnimCurve.SlowLerpAnimCurve();
        public AnimationCurve BellyTopAC = AnimCurve.BellyTopAnimCurve();
        public AnimationCurve BellyEdgeAC = AnimCurve.BellyEdgeAnimCurve();
        public AnimationCurve BellySidesAC = AnimCurve.BellySidesAnimCurve();
        public AnimationCurve MediumLerpAC = AnimCurve.MediumLerpAnimCurve();
        public AnimationCurve BellyGapLerpAC = AnimCurve.BellyGapAnimCurve();



#region overrides

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
            firstStart = true;
            isProcessing = false;
            
            //Character card name 
            charaFileName = ChaFileControl.parameter.fullname;        
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" ");
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $Start {charaFileName}");
            ReadAndSetCardData();     

            //Initialize the list
            bindPoseList = new BindPoseList(charaFileName); 

            #if KKS || AI

                //When HScene starts, pre compute inflated size blendshape
                
                //This fires for all characters, even those not in Hscene...
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
                    ClearInflationStuff(fullReset: true);
                };
         
            #endif

            nativeDetour = new NativeDetourMesh();  
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
            #if HS2 || AI
                //Fix for the way AI injects new characters in Main Game by copying existing character first.  This will remove old blendshapes.
                ScrubTempBlendShapes();
            #endif

            //(no longer used) Check for swapping out character Game Object with new character
            IsNewChar(ChaFileControl);
            charaFileName = ChaFileControl.parameter.fullname;

            //Update chara name when it changes for error log
            bindPoseList.charaFileName = charaFileName;

            ReadAndSetCardData();
            //When char was swaped, determine which char's belly shape to use
            CheckBellyPreservation();
            //Check for saved blendshapes to load from char card
            CheckBlendShapes();        

            //Apply belly shape slider values saved on char card
            StartCoroutine(ReloadStoryInflation(0.5f, "Reload-story"));   
            StartCoroutine(ReloadStudioMakerInflation(1.5f, reMeasure: true, "Reload"));  //Give time for character to load, and settle  

            firstStart = false;
        }        


        protected override void Update()
        {
            //Used to inflate/deflate in main game
            WatchForUserKeyPress();
            //Used to incrementally inflate belly during HScene
            ComputeInflationChange();

            //just for debugging belly during animations, very compute heavy for Update()
            if (PregnancyPlusPlugin.DebugAnimations.Value)
            {
                if (Time.frameCount % 90 == 0) MeshInflate(new MeshInflateFlags(this, _checkForNewMesh: true, _freshStart: true, _reMeasure: true), "Update");
            }                         
        }


        protected override void OnDestroy() 
        {
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $OnDestroy {charaFileName}"); 

            //Remove the detour we made
            nativeDetour?.Dispose();         

            //Always clear debug objects from character in debug mode, just to be safe
            #if DEBUG
                DebugTools.ClearAllThingsFromCharacter();
            #else
                if (PregnancyPlusPlugin.AnyDebugPrimitivesToggled())
                    DebugTools.ClearAllThingsFromCharacter();
            #endif
        }
        

#endregion overrides

    }
}


