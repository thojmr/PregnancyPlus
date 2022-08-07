using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using KKAPI;
using KKAPI.Studio;
using KKAPI.Maker;
using KKAPI.Chara;
using KKAPI.Utilities;
using UnityEngine;
#if KKS || AI
    using KKAPI.MainGame;
#endif

namespace KK_PregnancyPlus
{       
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", BepInDependency.DependencyFlags.SoftDependency)]
    #if KKS
        [BepInDependency(KoikatuAPI.GUID, "1.26")]
        [BepInDependency("KKPE", BepInDependency.DependencyFlags.SoftDependency)]
        [BepInDependency("KK_Pregnancy", BepInDependency.DependencyFlags.SoftDependency)]
    #elif HS2
        [BepInDependency(KoikatuAPI.GUID, "1.15")]
        [BepInDependency("HS2PE", BepInDependency.DependencyFlags.SoftDependency)]
    #elif AI
        [BepInDependency(KoikatuAPI.GUID, "1.17")]
        [BepInDependency("AIPE", BepInDependency.DependencyFlags.SoftDependency)]
        [BepInDependency("AI_Pregnancy", BepInDependency.DependencyFlags.SoftDependency)]
    #endif
    public partial class PregnancyPlusPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_PregnancyPlus";
        public const string Version = "7.7";
        internal static new ManualLogSource Logger { get; private set; }
   

        //Used to hold the last non zero belly shape slider values that were applied to any character for the Paste button
        public static PregnancyPlusData copiedBelly = null;    
        //Logs important error messages to console   
        public static ErrorCodeController errorCodeCtrl;
        internal Harmony hi;
        //Used to fetch all active preg+ character controllers
        internal CharacterApi.ControllerRegistration charCustFunCtrlHandler;


        internal void Start()
        {
            //Set up logging config
            Logger = base.Logger;    
            DebugTools.logger = Logger;          
            errorCodeCtrl = new ErrorCodeController(Logger, PregnancyPlusPlugin.DebugLog != null ? PregnancyPlusPlugin.DebugLog.Value : false);  

            //Initilize the Bepinex F1 ConfigurationManager options
            PluginConfig();                    

            //Attach the Preg+ logic to each character
            CharacterApi.RegisterExtraBehaviour<PregnancyPlusCharaController>(GUID);
            #if KKS || AI
                GameAPI.RegisterExtraBehaviour<PregnancyPlusGameController>(GUID);
            #endif

            hi = new Harmony(GUID);
            Hooks.InitHooks(hi);
            HooksClothing.InitHooks(hi);
            HooksAccessory.InitHooks(hi);
            Hooks_Uncensor.InitHooks(hi);
            Hooks_HSPE.InitHooks(this);
            #if KKS || AI
                Hooks_KK_Pregnancy.InitHooks(hi);
            #elif HS2
                Hooks_HS2_Inflation.InitHooks(hi);
            #endif

            //Set up studio/malker GUI sliders
            PregnancyPlusGui.InitStudio(hi, this);
            PregnancyPlusGui.InitMaker(hi, this);       

            charCustFunCtrlHandler = CharacterApi.GetRegisteredBehaviour(GUID);

            //Requires KKAPI 1.30+ and Bepinex 5.4.15 to use the timeline interpolable, but its just a soft depencendy for this plugin
            try {
                //Set up the timeline imterpolable tool
                if (TimelineCompatibility.IsTimelineAvailable())
                {
                    const string timelineGUID = "PregnancyPlus";//Never change

                    TimelineCompatibility.AddCharaFunctionInterpolable<int, PregnancyPlusCharaController>(
                        timelineGUID, 
                        "0", 
                        "Pregnancy+",
                        (oci, ctrl, leftValue, rightValue, factor) => {
                            var inflationSize = Mathf.LerpUnclamped(leftValue, rightValue, factor);                            
                            ctrl.MeshInflate(inflationSize, "timeline_interpolable");
                        },
                        null,
                        (oci, ctrl) => (int)ctrl.infConfig.inflationSize
                        );
                }
            }
            catch {}
        }


        private void OnDestroy()
        {
            //For ScriptEngine reloads
            hi?.UnpatchAll(GUID);
            if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" PregnancyPlus.UnpatchAll "); 
            DebugTools.ClearAllThingsFromCharacter();
        }

    
        /// <summary>
        /// Updates any GUI components when blendshape GUI is opened in studio and maker
        /// </summary>
        internal void OnGUI()
        {                
            if (!StudioAPI.InsideStudio && !MakerAPI.InsideMaker) return;

            //Update method to track GUI text changes when needed
            PregnancyPlusGui.Update();

            //Need to trigger all children GUI that should be active. 
            if (charCustFunCtrlHandler == null || charCustFunCtrlHandler.Instances == null) return;

            //For each character controller with an open GUI, update their GUI
            foreach (PregnancyPlusCharaController ppcc in charCustFunCtrlHandler.Instances)
            {            
                //Update any active gui windows
                ppcc?.blendShapeGui.OnGUI(this);                                                                                 
                ppcc?.clothOffsetGui.OnGUI(this);                                                                                 
            }
        }
    
    }
}
