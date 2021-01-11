using BepInEx;
using BepInEx.Configuration;
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
        public const string Version = "0.11";
        internal static new ManualLogSource Logger { get; private set; }
        public static ConfigEntry<bool> StoryMode { get; private set; }
        public static ConfigEntry<bool> HDSmoothing { get; private set; }
        public static ConfigEntry<float> MaxStoryModeBelly { get; private set; }
        public static ConfigEntry<float> StoryModeInflationShiftZ { get; private set; }
        public static ConfigEntry<float> StoryModeInflationTaperY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationMultiplier { get; private set; }

#if DEBUG
        internal static bool debugLog = true;
#else
        internal static bool debugLog = false;
#endif        


        private void Start()
        {
            Logger = base.Logger;     

            HDSmoothing = Config.Bind<bool>("Character Studio", "HD Crease Smoothing (CPU heavy)", false, "This will reduce the hard edges you sometimes see along the characters body after using a slider.  Typically only noticable on HD models like in HS2 or AI.  Turn it off if you are bothered by the added CPU load, or don't mind the edges.  It basically adds a custom vector norm method that handles UV boundaries better tha Unity's solution.");
            HDSmoothing.SettingChanged += HDSmoothing_SettingsChanged;

#if KK
            StoryMode = Config.Bind<bool>("Experimental Story Mode (Requires KK_Pregnancy)", "Story mode", false, "Nothing crazy, but I figured someone out there might want to try it.  No further plans at the moment, it was just simple to implement.\r\n\r\nThis will combine the effects of KK_PregnancyPlus with KK_Pregnancy (larger max belly overall), but be aware that you will see lots of clothes clipping at large sizes.");
            StoryMode.SettingChanged += StoryMode_SettingsChanged;

            MaxStoryModeBelly = Config.Bind<float>("Experimental Story Mode (Requires KK_Pregnancy)", "Max additional belly size", 20f, 
                new ConfigDescription("The maximum additional belly size that this plugin will add to the original KK_Pregnancy belly. The character must be pregnant.\r\n0 will result in the original KK_Pregnancy belly, while 40 will be the original + an additional 40",
                new AcceptableValueRange<float>(0f, 40f)));
            MaxStoryModeBelly.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationMultiplier = Config.Bind<float>("Experimental Story Mode (Requires KK_Pregnancy)", "Global Multiplier", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Multiplier' amount in story mode for all characters",
                new AcceptableValueRange<float>(-2f, 2f)));
            StoryModeInflationMultiplier.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftZ = Config.Bind<float>("Experimental Story Mode (Requires KK_Pregnancy)", "Global Shift Z", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Z' amount in story mode for all characters",
                new AcceptableValueRange<float>(-0.15f, 0.15f)));
            StoryModeInflationShiftZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationTaperY = Config.Bind<float>("Experimental Story Mode (Requires KK_Pregnancy)", "Global Taper Y", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Y' amount in story mode for all characters",
                new AcceptableValueRange<float>(-0.075f, 0.075f)));
            StoryModeInflationTaperY.SettingChanged += InflationConfig_SettingsChanged;
                                
#endif

            CharacterApi.RegisterExtraBehaviour<PregnancyPlusCharaController>(GUID);

            var hi = new Harmony(GUID);
            Hooks.InitHooks(hi);
            PregnancyPlusGui.Init(hi, this);
        }

        internal void StoryMode_SettingsChanged(object sender, System.EventArgs e) 
        {            
            //TODO
        }

        internal void HDSmoothing_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (!StudioAPI.InsideStudio) return;

            foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                ctrl.MeshInflate(true);                             
            }
        }

        internal void InflationConfig_SettingsChanged(object sender, System.EventArgs e) {
            if (StudioAPI.InsideStudio) return;

            // foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
            //     ctrl.MeshInflate(true);                             
            // }                  
        }

        /// <summary>
        /// Provides access to methods for getting and setting clothes state changes to a specific CharCustomFunctionController.
        /// </summary>
        /// <param name="chaControl"></param>
        /// <returns>KKAPI character controller</returns>
        public static PregnancyPlusCharaController GetCharaController(ChaControl chaControl) => chaControl == null ? null : chaControl.gameObject.GetComponent<PregnancyPlusCharaController>();
    
    }
}
