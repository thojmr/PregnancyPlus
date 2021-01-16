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
    #if KK
        [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    #endif
    public partial class PregnancyPlusPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_PregnancyPlus";
        public const string Version = "1.3";
        internal static new ManualLogSource Logger { get; private set; }

        #if DEBUG
            internal static bool debugLog = true;
        #else
            internal static bool debugLog = false;
        #endif        

        //Used to hold the last non zero belly shape slider values that were applied to any character for Restore button
        public static PregnancyPlusData lastBellyState =  new PregnancyPlusData();


        private void Start()
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
        /// Provides access to methods for getting and setting clothes state changes to a specific CharCustomFunctionController.
        /// </summary>
        /// <param name="chaControl"></param>
        /// <returns>KKAPI character controller</returns>
        public static PregnancyPlusCharaController GetCharaController(ChaControl chaControl) => chaControl == null ? null : chaControl.gameObject.GetComponent<PregnancyPlusCharaController>();
    
    }
}
