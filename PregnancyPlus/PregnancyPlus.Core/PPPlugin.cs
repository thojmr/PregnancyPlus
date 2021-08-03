using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using KKAPI;
using KKAPI.Studio;
using KKAPI.Chara;

namespace KK_PregnancyPlus
{       
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", BepInDependency.DependencyFlags.SoftDependency)]
    #if KKS
        [BepInDependency(KoikatuAPI.GUID, "1.20")]
        [BepInDependency("KKPE", BepInDependency.DependencyFlags.SoftDependency)]
        [BepInDependency("KK_Pregnancy", BepInDependency.DependencyFlags.SoftDependency)]
    #elif KK
        [BepInDependency(KoikatuAPI.GUID, "1.14")]
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
        public const string Version = "4.6";
        internal static new ManualLogSource Logger { get; private set; }
   

        //Used to hold the last non zero belly shape slider values that were applied to any character for Restore button
        public static PregnancyPlusData copiedBelly = null;       
        public static ErrorCodeController errorCodeCtrl;


        internal void Start()
        {
            Logger = base.Logger;    
            DebugTools.logger = Logger;          
            errorCodeCtrl = new ErrorCodeController(Logger, PregnancyPlusPlugin.DebugLog != null ? PregnancyPlusPlugin.DebugLog.Value : false);  
            //Initilize the Bepinex F1 ConfigurationManager options
            PluginConfig();                    

            //Attach the mesh inflation logic to each character
            CharacterApi.RegisterExtraBehaviour<PregnancyPlusCharaController>(GUID);

            var hi = new Harmony(GUID);
            Hooks.InitHooks(hi);
            Hooks_Uncensor.InitHooks(hi);
            #if KK || AI
                Hooks_KK_Pregnancy.InitHooks(hi);
            #elif HS2
                Hooks_HS2_Inflation.InitHooks(hi);
            #endif

            //Set up studio/malker GUI sliders
            PregnancyPlusGui.InitStudio(hi, this);
            PregnancyPlusGui.InitMaker(hi, this);       
        }

    
        /// <summary>
        /// Triggers any charCustFunCtrl GUI components when blendshape GUI is opened in studio
        /// </summary>
        internal void OnGUI()
        {                
            if (!StudioAPI.InsideStudio) return;

            //Need to trigger all children GUI that should be active. 
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
            if (handlers == null || handlers.Instances == null) return;

            //For each character controller, update GUI
            foreach (var charCustFunCtrl in handlers.Instances)
            {            
                PregnancyPlusCharaController ctrl;                          
                try 
                {
                    //Try casting.  ScriptEngine reloads will cause errors here otherwise
                    ctrl = (PregnancyPlusCharaController) charCustFunCtrl;
                }      
                catch(Exception e)
                {
                    //If fails to cast then the charController is old and can be skipped
                    continue;
                }    

                //Update any active gui windows
                ctrl.blendShapeGui.OnGUI(this);                                                                                 
            }
        }
    
    }
}
