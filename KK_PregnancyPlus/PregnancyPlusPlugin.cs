using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;

namespace KK_PregnancyPlus
{
    [BepInPlugin(GUID, GUID, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public partial class PregnancyPlusPlugin : BaseUnityPlugin
    {
        public const string GUID = "KK_PregnancyPlus";
        public const string Version = "0.4";
        internal static new ManualLogSource Logger { get; private set; }
        public static ConfigEntry<bool> StoryMode { get; private set; }


        private void Start()
        {
            Logger = base.Logger;     

#if (Debug || DEBUG)
            StoryMode = Config.Bind<bool>("", "Enable in story mode (clothing bugs)", false, "This will combine the effects of KK_PregnancyPlus with KK_Pregnancy (larger and rounder belly overall), but be aware that you will see lots of clothes clipping at large sizes.");
            StoryMode.SettingChanged += StoryMode_SettingsChanged;
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
    }
}
