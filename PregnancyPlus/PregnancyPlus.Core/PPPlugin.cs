using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Studio;
using KKAPI.Chara;
#if AI || HS2
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#endif

namespace KK_PregnancyPlus
{
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency(KoikatuAPI.GUID, "1.12")]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", BepInDependency.DependencyFlags.SoftDependency)]
    #if KK
        [BepInDependency("KKPE", BepInDependency.DependencyFlags.SoftDependency)]
        [BepInDependency("KK_Pregnancy", BepInDependency.DependencyFlags.SoftDependency)]
    #elif HS2
        [BepInDependency("HS2PE", BepInDependency.DependencyFlags.SoftDependency)]
    #elif AI
        [BepInDependency("AIPE", BepInDependency.DependencyFlags.SoftDependency)]
    #endif
    public partial class PregnancyPlusPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_PregnancyPlus";
        public const string Version = "1.11";
        internal static new ManualLogSource Logger { get; private set; }

        #if DEBUG
            internal static bool debugLog = true;
        #else
            internal static bool debugLog = false;
        #endif        

        //Used to hold the last non zero belly shape slider values that were applied to any character for Restore button
        public static PregnancyPlusData lastBellyState =  new PregnancyPlusData();


        internal void Start()
        {
            Logger = base.Logger;    
            //Initilize the plugin config options 
            PluginConfig();

            //Attach the mesh inflation logic to each character
            CharacterApi.RegisterExtraBehaviour<PregnancyPlusCharaController>(GUID);

            var hi = new Harmony(GUID);
            Hooks.InitHooks(hi);

            //Set up studio/malker GUI sliders
            PregnancyPlusGui.InitStudio(hi, this);
            PregnancyPlusGui.InitMaker(hi, this);
        }

    
        /// <summary>
        /// Triggers all charCustFunCtrl GUI components when needed in studio
        /// </summary>
        internal void OnGUI()
        {                
            if (!StudioAPI.InsideStudio) return;

            //Need to trigger all children GUI that should be active. 
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);

            //I guess this is how its suppposed to be done?
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
            { 
                charCustFunCtrl.blendShapeGui.OnGUI(this);                                    
            }
        }
    
    }
}
