using BepInEx.Configuration;
using KKAPI.Studio;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        public static ConfigEntry<bool> StoryMode { get; private set; }
        public static ConfigEntry<bool> HDSmoothing { get; private set; }
        public static ConfigEntry<bool> MakeBalloon { get; private set; }
        public static ConfigEntry<float> MaxStoryModeBelly { get; private set; }
        public static ConfigEntry<float> StoryModeInflationShiftZ { get; private set; }
        public static ConfigEntry<float> StoryModeInflationTaperY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationMultiplier { get; private set; }
     
        internal void PluginConfig()
        {

            HDSmoothing = Config.Bind<bool>("Character Studio", "HD Crease Smoothing (CPU heavy)", false, "This will reduce the hard edges you sometimes see along the characters body after using a slider.  Typically only noticable on HD models like in HS2 or AI.  Turn it off if you are bothered by the added CPU load, or don't mind the edges.  It basically adds a custom vector norm method that handles UV boundaries better tha Unity's solution.");
            HDSmoothing.SettingChanged += HDSmoothing_SettingsChanged;

            MakeBalloon = Config.Bind<bool>("Character Studio", "Make me a balloon", false, "Try it and see what happens, disable to go back to the original style.  (AKA debug mesh mode)");
            MakeBalloon.SettingChanged += MakeBalloon_SettingsChanged;

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
        }

        internal void HDSmoothing_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (!StudioAPI.InsideStudio) return;
            var ctrls = StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>();

            //Re trigger inflation and recalculate vert positions
            foreach (var ctrl in ctrls) {  
                ctrl.MeshInflate(true);                             
            }
        }

        internal void StoryMode_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (StudioAPI.InsideStudio) return;

            // var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
            // var instances = (System.Collections.Generic.IEnumerable<PregnancyPlusCharaController>)handlers.Instances;
        
            // if (StoryMode.Value) {
            //     //Re trigger inflation and recalculate vert positions
            //     foreach (var ctrl in instances) {  
            //         ctrl.GetWeeksAndSetInflation();                            
            //     }
            // } else {
            //     //Disable all infaltions
            //     foreach (var ctrl in instances) {  
            //         ctrl.MeshInflate(0);                             
            //     }
            // }
        }

        internal void InflationConfig_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (StudioAPI.InsideStudio) return;

            // var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
            // var instances = (System.Collections.Generic.IEnumerable<PregnancyPlusCharaController>)handlers.Instances;

            // //Re trigger infaltion when a value changes for each controller
            // foreach (var ctrl in instances) {  
            //     if (PregnancyPlusPlugin.StoryMode != null) {
            //         if (PregnancyPlusPlugin.StoryMode.Value) ctrl.GetWeeksAndSetInflation();
            //     }                             
            // }                  
        }

        internal void MakeBalloon_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (!StudioAPI.InsideStudio) return;

            var ctrls = StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>();
            //TODO why doesnt this work when there are multiple characters?
            
            //Re trigger inflation and recalculate vert positions
            foreach (var ctrl in ctrls) {  
                ctrl.MeshInflate(true, true);                             
            }          
        }

    }
}
