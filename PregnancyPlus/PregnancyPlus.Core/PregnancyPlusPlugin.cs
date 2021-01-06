using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;

namespace KK_PregnancyPlus
{
    [BepInPlugin(GUID, GUID, Version)]
#if KK
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
#endif
    public partial class PregnancyPlusPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_PregnancyPlus";
        public const string Version = "0.8";
        internal static new ManualLogSource Logger { get; private set; }
        public static ConfigEntry<bool> StoryMode { get; private set; }
        public static ConfigEntry<bool> HDSmoothing { get; private set; }

#if DEBUG
        internal static bool debugLog = true;
#else
        internal static bool debugLog = false;
#endif


        private void Start()
        {
            Logger = base.Logger;     

#if DEBUG
            StoryMode = Config.Bind<bool>("", "Enable in story mode (clothing bugs)", false, "This will combine the effects of KK_PregnancyPlus with KK_Pregnancy (larger and rounder belly overall), but be aware that you will see lots of clothes clipping at large sizes.");
            StoryMode.SettingChanged += StoryMode_SettingsChanged;
#endif

            HDSmoothing = Config.Bind<bool>("", "Experimental HD smoothing", false, "This will reduce the hard edges you sometimes see on characters after using sliders.  But will dramatically slow down the slider performance.  Only use it if you can see the edges.  Typically only noticable on HD models like in HS2 or AI.");
            HDSmoothing.SettingChanged += HDSmoothing_SettingsChanged;


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
            //TODO
        }
    }
}
