using BepInEx.Configuration;
using KKAPI.Studio;
using KKAPI.Chara;
using System.Reflection;

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
        public static ConfigEntry<float> StoryModeInflationSize { get; private set; }
     
        internal void PluginConfig()
        {            

            HDSmoothing = Config.Bind<bool>("Character Studio", "HD Crease Smoothing (CPU heavy)", false, "This will reduce the hard edges you sometimes see along the characters body after using a slider.  Typically only noticable on HD models like in HS2 or AI.  Turn it off if you are bothered by the added CPU load, or don't mind the edges.  It basically adds a custom vector norm method that handles UV boundaries better tha Unity's solution.");
            HDSmoothing.SettingChanged += HDSmoothing_SettingsChanged;

            MakeBalloon = Config.Bind<bool>("Character Studio", "Make me a balloon", false, "Try it and see what happens, disable to go back to the original style.  (AKA debug mesh mode)");
            MakeBalloon.SettingChanged += MakeBalloon_SettingsChanged;

#if KK
            var storyConfigTitle = "Experimental Story Mode (Requires KK_Pregnancy)";     
            var storyConfigDescription = "Nothing crazy, but I figured someone out there might want to try it.  No further plans at the moment, it was just simple to implement.\r\n\r\nThis will combine the effects of KK_PregnancyPlus with KK_Pregnancy (larger max belly overall), but be aware that you will see lots of clothes clipping at large sizes.";
            var additionalSliderText = " for all pregnant characters";

#elif HS2 || AI
            var storyConfigTitle = "Experimental Story Mode";
            var storyConfigDescription = "Nothing crazy, but I figured someone out there might want to try it.  No further plans at the moment, it was just simple to implement.\r\n\r\nThis will let you set a global belly size and shape, but because it is global all characters will have the same size and shape as the slider.  No individuial customization like Studio mode.  At least for now.";
            var additionalSliderText = "";
#endif

            StoryMode = Config.Bind<bool>(storyConfigTitle, "Story mode", false, storyConfigDescription);
            StoryMode.SettingChanged += StoryMode_SettingsChanged;

#if KK 
            //This config is for KK_Pregnancy integration to set the additional size this plugin will add to KK_Pregnancy (KK only)
            MaxStoryModeBelly = Config.Bind<float>(storyConfigTitle, "Max additional belly size", 20f, 
                new ConfigDescription("The maximum additional belly size that this plugin will add to the original KK_Pregnancy belly. The character must be pregnant.\r\n0 will result in the original KK_Pregnancy belly, while 40 will be the original + an additional 40",
                new AcceptableValueRange<float>(0f, 40f)));
            MaxStoryModeBelly.SettingChanged += InflationConfig_SettingsChanged;
#elif HS2 || AI
            //Allow setting base size of the belly directly, since there is no KK_Pregnancy for HS2 and AI
            StoryModeInflationSize = Config.Bind<float>(storyConfigTitle, "Global Belly Size", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Belly Size' in story mode" + additionalSliderText,
                new AcceptableValueRange<float>(0, 40)));
            StoryModeInflationSize.SettingChanged += InflationConfig_SettingsChanged;
#endif

            StoryModeInflationMultiplier = Config.Bind<float>(storyConfigTitle, "Global Multiplier", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Multiplier' amount in story mode" + additionalSliderText,
                new AcceptableValueRange<float>(-2f, 2f)));
            StoryModeInflationMultiplier.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftZ = Config.Bind<float>(storyConfigTitle, "Global Shift Z", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Z' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(-0.15f, 0.15f)));
            StoryModeInflationShiftZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationTaperY = Config.Bind<float>(storyConfigTitle, "Global Taper Y", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Y' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(-0.075f, 0.075f)));
            StoryModeInflationTaperY.SettingChanged += InflationConfig_SettingsChanged; 

        }

        internal void HDSmoothing_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (!StudioAPI.InsideStudio) return;
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" HDSmoothing_SettingsChanged ");
            var charCustFunCtrls = StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>();

            //Re trigger inflation and recalculate vert positions
            foreach (var charCustFunCtrl in charCustFunCtrls) 
            {  
                charCustFunCtrl.MeshInflate(true);                             
            }
        }

        internal void StoryMode_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (StudioAPI.InsideStudio) return;
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" StoryMode_SettingsChanged > {StoryMode.Value}");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            if (StoryMode.Value) 
            {
                //Re trigger inflation and recalculate vert positions
                foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
                { 
#if KK              //In kk we want to use KK_pregnancy weeks to determine the belly size
                    charCustFunCtrl.GetWeeksAndSetInflation(true);                     
#elif HS2 || AI     //In HS2/AI we set the belly size based on the plugin config slider
                    charCustFunCtrl.MeshInflate(StoryModeInflationSize.Value);                     
#endif                    
                }
            } 
            else 
            {
                //Disable all mesh inflations
                foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances) 
                {  
                    charCustFunCtrl.infConfig = new PregnancyPlusData();
                    charCustFunCtrl.ResetInflation();        
                }
            }
        }

        internal void InflationConfig_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (StudioAPI.InsideStudio) return;
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" InflationConfig_SettingsChanged ");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);

            //Re trigger infaltion when a value changes for each controller
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances) 
            {  
                if (PregnancyPlusPlugin.StoryMode != null && PregnancyPlusPlugin.StoryMode.Value) 
                {            
#if KK                    
                    charCustFunCtrl.GetWeeksAndSetInflation(true);    
#elif HS2 || AI
                    charCustFunCtrl.MeshInflate(StoryModeInflationSize.Value, true);                     
#endif             
                }                             
            }                  
        }

        internal void MakeBalloon_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (!StudioAPI.InsideStudio) return;

            var charCustFunCtrls = StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>();
            //TODO why doesnt this work when there are multiple characters?
            
            //Re trigger inflation and recalculate vert positions
            foreach (var charCustFunCtrl in charCustFunCtrls) 
            {  
                charCustFunCtrl.MeshInflate(true, true);                             
            }          
        }

    }
}
