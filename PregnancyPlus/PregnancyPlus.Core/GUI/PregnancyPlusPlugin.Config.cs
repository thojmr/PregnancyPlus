using System;
using BepInEx.Configuration;
using KKAPI.Studio;
using KKAPI.Maker;
using KKAPI.Chara;
using System.Reflection;
using UnityEngine;

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        public static ConfigEntry<bool> StoryMode { get; private set; }
        public static ConfigEntry<bool> MakeBalloon { get; private set; }
        public static ConfigEntry<float> MaxStoryModeBelly { get; private set; }
        public static ConfigEntry<float> StoryModeInflationSize { get; private set; }
        public static ConfigEntry<float> StoryModeInflationMultiplier { get; private set; }
        public static ConfigEntry<float> StoryModeInflationMoveY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationMoveZ { get; private set; }
        public static ConfigEntry<float> StoryModeInflationStretchX { get; private set; }
        public static ConfigEntry<float> StoryModeInflationStretchY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationShiftY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationShiftZ { get; private set; }
        public static ConfigEntry<float> StoryModeInflationTaperY { get; private set; }
        public static ConfigEntry<float> StoryModeInflationTaperZ { get; private set; }        
        public static ConfigEntry<float> StoryModeInflationClothOffset { get; private set; }        
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationIncrease { get; private set; }        
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationDecrease { get; private set; }    
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationReset { get; private set; }    
    
     
        internal void PluginConfig()
        {            
            MakeBalloon = Config.Bind<bool>("Character Studio", "Make me a balloon", false, "Try it and see what happens, disable to go back to the original style.  (AKA debug mesh mode)");
            MakeBalloon.SettingChanged += MakeBalloon_SettingsChanged;

            #if KK
                var storyConfigTitle = "Story/Main-Game Mode (Requires KK_Pregnancy)";     
                var storyConfigDescription = "Initial belly size will be loaded from character card, if enabled on the character card in Maker.\r\n\r\nIf KK_Pregnancy is also installed, this will combine the effects of KK_PregnancyPlus with KK_Pregnancy (larger max belly overall). \r\n\r\nThe below sliders can be used in tandem to adjust all characters shapes when this is enabled";
                var additionalSliderText = " for all pregnant characters";
            #elif HS2 || AI
                var storyConfigTitle = "Story/Main-Game Mode";
                var storyConfigDescription = "Initial belly size will be loaded from character card, if enabled on the character card in Maker.\r\n\r\nThe below sliders can be used in tandem to adjust all characters shapes when this is enabled.";
                var additionalSliderText = "";
            #endif

            StoryMode = Config.Bind<bool>(storyConfigTitle, "Gameplay Enabled", true, storyConfigDescription);
            StoryMode.SettingChanged += StoryMode_SettingsChanged;

            #if KK 
                //This config is for KK_Pregnancy integration to set the additional size this plugin will add to KK_Pregnancy (KK only)
                MaxStoryModeBelly = Config.Bind<float>(storyConfigTitle, "Max additional belly size", 10f, 
                    new ConfigDescription("The maximum additional belly size that this plugin will add to the original KK_Pregnancy belly. The character must be pregnant.\r\n0 will result in the original KK_Pregnancy belly, while 40 will be the original + an additional 40 added by this plugin.",
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationSize[0], PregnancyPlusGui.SliderRange.inflationSize[1])));
                MaxStoryModeBelly.SettingChanged += InflationConfig_SettingsChanged;
            #elif HS2 || AI
                //Allow setting base size of the belly directly, since there is no KK_Pregnancy for HS2 and AI
                StoryModeInflationSize = Config.Bind<float>(storyConfigTitle, "Global Belly Size Adjustment", 0, 
                    new ConfigDescription("Allows you to increase or decrease the 'Belly Size' in story mode" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationSize[0], PregnancyPlusGui.SliderRange.inflationSize[1])));
                StoryModeInflationSize.SettingChanged += InflationConfig_SettingsChanged;
            #endif

            StoryModeInflationMultiplier = Config.Bind<float>(storyConfigTitle, "Global Multiplier Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Multiplier' amount in story mode" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationMultiplier[0], PregnancyPlusGui.SliderRange.inflationMultiplier[1])));
            StoryModeInflationMultiplier.SettingChanged += InflationConfig_SettingsChanged;
            
            StoryModeInflationMoveY = Config.Bind<float>(storyConfigTitle, "Global Move Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Move Y' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationMoveY[0], PregnancyPlusGui.SliderRange.inflationMoveY[1])));
            StoryModeInflationMoveY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationMoveZ = Config.Bind<float>(storyConfigTitle, "Global Move Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Move Z' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationMoveZ[0], PregnancyPlusGui.SliderRange.inflationMoveZ[1])));
            StoryModeInflationMoveZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationStretchX = Config.Bind<float>(storyConfigTitle, "Global Stretch X Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Stretch X' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationStretchX[0], PregnancyPlusGui.SliderRange.inflationStretchX[1])));
            StoryModeInflationStretchX.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationStretchY = Config.Bind<float>(storyConfigTitle, "Global Stretch Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Stretch Y' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationStretchY[0], PregnancyPlusGui.SliderRange.inflationStretchY[1])));
            StoryModeInflationStretchY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftY = Config.Bind<float>(storyConfigTitle, "Global Shift Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Y' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationShiftY[0], PregnancyPlusGui.SliderRange.inflationShiftY[1])));
            StoryModeInflationShiftY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftZ = Config.Bind<float>(storyConfigTitle, "Global Shift Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Z' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationShiftZ[0], PregnancyPlusGui.SliderRange.inflationShiftZ[1])));
            StoryModeInflationShiftZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationTaperY = Config.Bind<float>(storyConfigTitle, "Global Taper Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Y' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationTaperY[0], PregnancyPlusGui.SliderRange.inflationTaperY[1])));
            StoryModeInflationTaperY.SettingChanged += InflationConfig_SettingsChanged; 

            StoryModeInflationTaperZ = Config.Bind<float>(storyConfigTitle, "Global Taper Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Z' amount in story mode for" + additionalSliderText,
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationTaperZ[0], PregnancyPlusGui.SliderRange.inflationTaperZ[1])));
            StoryModeInflationTaperZ.SettingChanged += InflationConfig_SettingsChanged; 

            StoryModeInflationClothOffset = Config.Bind<float>(storyConfigTitle, "Global Cloth Offset Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the cloth layer distance to reduce clipping",
                new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.inflationClothOffset[0], PregnancyPlusGui.SliderRange.inflationClothOffset[1])));
            StoryModeInflationClothOffset.SettingChanged += InflationConfig_SettingsChanged;  
                    

            //Live inflation in story mode.  Increase or decrease base inflationSize with a keybinding press
            StoryModeInflationIncrease  = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation + Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to increase the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters"));

            StoryModeInflationDecrease = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation - Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to decrease the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters"));

            StoryModeInflationReset = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation reset Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to reset the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters"));
        }

        internal void StoryMode_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//Don't allow in studio
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" StoryMode_SettingsChanged > {StoryMode.Value}");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            if (StoryMode.Value) 
            {
                //Re trigger inflation and recalculate vert positions
                foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
                { 
                    #if KK //In kk we want to use KK_pregnancy weeks to determine the belly size
                        charCustFunCtrl.GetWeeksAndSetInflation(true);                     
                    #elif HS2 || AI //In HS2/AI we set the belly size based on the plugin config slider
                        charCustFunCtrl.ReadCardData();
                        charCustFunCtrl.MeshInflate(StoryModeInflationSize.Value, true);                     
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
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//dont allow in studio
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
                        charCustFunCtrl.ReadCardData();
                        charCustFunCtrl.MeshInflate(StoryModeInflationSize.Value, true);                     
                    #endif             
                }                             
            }                  
        }

        internal void MakeBalloon_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" MakeBalloon_SettingsChanged ");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
            {                 
                //Force recalculate all verts.  With balloon active it will automatically calaulcate the correct new boundaries
                charCustFunCtrl.MeshInflate(true, true);                                       
            }    
        }

    }
}
