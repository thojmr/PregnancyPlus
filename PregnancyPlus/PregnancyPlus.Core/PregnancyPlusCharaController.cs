using KKAPI;
using KKAPI.Chara;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UniRx;
#if HS2 || AI
using AIChara;
#endif

namespace KK_PregnancyPlus
{

    //This partial class contains the property declarations and override hooks
    public partial class PregnancyPlusCharaController: CharaCustomFunctionController
    {
        
        internal bool initialized = false;//Prevent some actions from happening before character data loads   


        //Holds the user entered slider values
        public PregnancyPlusData infConfig = new PregnancyPlusData();
        internal PregnancyPlusData infConfigHistory = new PregnancyPlusData();        


        //Keeps track of all belly verticies, the dicts are indexed by the (meshRenderer.name + the vertex count) to make the indexes unique
        public Dictionary<string, Vector3[]> originalVertices = new Dictionary<string, Vector3[]>();
        public Dictionary<string, Vector3[]> inflatedVertices = new Dictionary<string, Vector3[]>();//Max extended mesh verts, after all slider calculations
        public Dictionary<string, Vector3[]> currentVertices = new Dictionary<string, Vector3[]>();//Currently active visible mesh verts
        public Dictionary<string, bool[]> bellyVerticieIndexes = new Dictionary<string, bool[]>();//List of verticie indexes that belong to the belly area



        //For fetching uncensor body guid data (bugfix for uncensor body vertex positions)
        public const string UncensorCOMName = "com.deathweasel.bepinex.uncensorselector";
        public const string DefaultBodyFemaleGUID = "Default.Body.Female";

        public const string KK_PregnancyPluginName = "KK_Pregnancy";//Allows us to pull KK_pregnancy data values




#region overrides
        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            SetExtendedData(infConfig.Save());
        }


        protected override void Awake() 
        {                    
            if (PregnancyPlusPlugin.StoryMode != null) {
                if (PregnancyPlusPlugin.StoryMode.Value) CharacterApi.CharacterReloaded += OnCharacterReloaded;            
            }

            base.Awake();
        }


        protected override void Start() 
        {
#if KK            
            //Detect clothing change in KK
            CurrentCoordinate.Subscribe(value => { OnCoordinateLoaded(); });
#endif
            ReadCardData();
            initialized = true;

            //Set the initial belly size if the character card has data
            if (infConfig.inflationSize > 0) StartCoroutine(WaitForMeshToSettle(0.5f));

            base.Start();
        }


#if HS2 || AI
        //The Hs2 way to detect clothing change in studio
        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate) 
        {
            // PregnancyPlusPlugin.Logger.LogInfo($" OnCoordinateBeingLoaded > "); 
            OnCoordinateLoaded();

            base.OnCoordinateBeingLoaded(coordinate);
        }
#endif


        protected override void OnReload(GameMode currentGameMode)
        {
            if (PregnancyPlusPlugin.StoryMode != null) {
                if (PregnancyPlusPlugin.StoryMode.Value) GetWeeksAndSetInflation();
            }
            ReadCardData();
        }


        protected override void Update()
        {
            //just for testing, pretty compute heavy for Update()
            // MeshInflate(true);
        }
        

#endregion


        internal void ReadCardData()
        {
            var data = GetExtendedData();
            infConfig = PregnancyPlusData.Load(data) ?? new PregnancyPlusData();
        }


        internal void OnCharacterReloaded(object sender, CharaReloadEventArgs e)  
        {  
            //When loading the character, if pregnant, apply the new inflated belly too
            if (ChaControl == null || e.ReloadedCharacter == null || e.ReloadedCharacter.name != ChaControl.name) return;

            GetWeeksAndSetInflation();
        } 


        internal void OnCoordinateLoaded()  
        {  
            //No loading coordinate changes before the pregnancydata values are fetched
            if (!initialized) return;
            //When clothing changes, reload inflation state
            // PregnancyPlusPlugin.Logger.LogInfo($" OnCoordinateLoaded > ");  
            StartCoroutine(WaitForMeshToSettle(0.10f, true));
        } 


        //After clothes change you have to wait a second if you want shadows to calculate correctly (longer in HS2, AI)
        IEnumerator WaitForMeshToSettle(float waitTime = 0.10f, bool force = false)
        {   
            yield return new WaitForSeconds(waitTime);
            MeshInflate(force);
        }


        //fetch KK_Pregnancy Data.Week value for experimental story mode integration (It works if you don't mind the clipping)
        internal void GetWeeksAndSetInflation() 
        {
            var week = PregnancyPlusHelper.GetWeeksFromPregnancyPluginData(ChaControl, KK_PregnancyPluginName);
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" Week >  {week}");
            if (week < 0) return;

            //Compute the additonal belly size added based on user configured vallue from 0-40
            var additionalPregPlusSize = Mathf.Lerp(0, week, PregnancyPlusPlugin.MaxStoryModeBelly.Value/40);

            MeshInflate(additionalPregPlusSize);
        }
        

    }
}


