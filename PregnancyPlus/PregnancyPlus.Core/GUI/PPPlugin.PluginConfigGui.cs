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
        public static ConfigEntry<bool> OverrideBelly { get; private set; }
        public static ConfigEntry<bool> AllowMale { get; private set; }
        public static ConfigEntry<bool> PerferTargetBelly { get; private set; }
        public static ConfigEntry<float> MaxStoryModeBelly { get; private set; }
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
        public static ConfigEntry<float> StoryModeInflationFatFold { get; private set; }    
        public static ConfigEntry<float> StoryModeInflationFatFoldHeight { get; private set; }    
        public static ConfigEntry<float> StoryModeInflationRoundness { get; private set; }    
        public static ConfigEntry<float> StoryModeInflationDrop { get; private set; }


        //Cumflation
        public static ConfigEntry<bool> AllowCumflation { get; private set; }
        public static ConfigEntry<int> CumflationMaxLevel { get; private set; }
        public static ConfigEntry<int> InflationLevel { get; private set; }
        public static ConfigEntry<int> DeflationLevel { get; private set; }


        //Debug config options
        public static ConfigEntry<bool> MakeBalloon { get; private set; }
        public static ConfigEntry<bool> DebugAnimations { get; private set; }
        public static ConfigEntry<bool> DebugLog { get; private set; }
        public static ConfigEntry<bool> DebugCalcs { get; private set; }
        public static ConfigEntry<bool> DebugVerts { get; private set; }


        //Keyboard shortcuts for inflation on the fly!    
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationIncrease { get; private set; }        
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationDecrease { get; private set; }    
        public static ConfigEntry<KeyboardShortcut> StoryModeInflationReset { get; private set; }    
    
     
        internal void PluginConfig()
        {            
            //**** Debug Config *******/
            //*************************/
            MakeBalloon = Config.Bind<bool>("Debug", "Balloon mode (Debug mode)", false,
                new ConfigDescription("Debug mesh mode, disable to go back to normal.  This will disable some Preg+ sliders temporarily.",
                    null,
                    new ConfigurationManagerAttributes { Order = 10, IsAdvanced = true })
                );
            MakeBalloon.SettingChanged += MakeBalloon_SettingsChanged;


            DebugAnimations = Config.Bind<bool>("Debug", "Refresh X Ticks (Debug mode)", false,
                new ConfigDescription( "Will force update the belly shape every x ticks to help debug belly shape during animations.  Don't leave enabled.",
                    null,
                    new ConfigurationManagerAttributes { Order = 9, IsAdvanced = true })
                );        


            DebugVerts = Config.Bind<bool>("Debug", "Entire Mesh Debugging (Debug mode)", false,
                new ConfigDescription( "Will cause all mesh verticies to be affected by sliders so I can narrow down which meshes are behaving, and which are not.  Don't leave enabled",
                    null,
                    new ConfigurationManagerAttributes { Order = 8, IsAdvanced = true })
                );
            DebugVerts.SettingChanged += DebugVerts_SettingsChanged;


            DebugLog = Config.Bind<bool>("Debug", "Enable Debug Logging (Debug mode)", false,
                new ConfigDescription( "Will log lots of Preg+ details to the console, but will condiserably slow down the game.  Don't leave enabled",
                    null,
                    new ConfigurationManagerAttributes { Order = 2, IsAdvanced = true })
                );
            DebugLog.SettingChanged += DebugLog_SettingsChanged;


            DebugCalcs = Config.Bind<bool>("Debug", "Enable Debug Logging of calculations (Debug mode)", false,
                new ConfigDescription( "Will log lots of Preg+ belly calculations to the console, but will condiserably slow down the game.  Don't leave enabled",
                    null,
                    new ConfigurationManagerAttributes { Order = 1, IsAdvanced = true })
                );


            //**** General Config *******/
            //***************************/

            AllowMale = Config.Bind<bool>("General", "Allow male", false,
                new ConfigDescription("When enabled, the sliders will work on male characters as well.",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 })
                );

            PerferTargetBelly = Config.Bind<bool>("General", "Prefer targets belly", true,
                new ConfigDescription("When changing characters in Maker, prefer the targets belly when both characters have a belly (Default).  When \'Disabled\' the source characters belly shape will be used.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 })
                );


            #if KK
                var storyConfigTitle = "Story/Main-Game Mode";     
                var additionalSliderText = " for all pregnant characters";

                var maxBellySizeTitle = "KK_Pregnancy Integration";
                var maxBellySizeDescription = "The maximum additional belly size/shape that this plugin will add to the original KK_Pregnancy belly. The character must be pregnant or inflated.\r\n0 will result in the original KK_Pregnancy belly, while 40 will be the original + the full Preg+ size/shape.";
                var overrideBellyShapeTitle = "Override KK_Pregnancy belly shape";
                var overrideBellyShapeDescription = "When enabled, the Preg+ belly shape will be the only shape used for pregnancy and inflation.  It disables the KK_Pregnancy shape in main gameplay.";
            
            #elif AI
                var storyConfigTitle = "Story/Main-Game Mode";     
                var additionalSliderText = " for all pregnant characters";

                var maxBellySizeTitle = "AI_Pregnancy Integration";
                var maxBellySizeDescription = "The maximum additional belly size/shape that this plugin will add to the original AI_Pregnancy belly. The character must be pregnant or inflated.\r\n0 will result in the original AI_Pregnancy belly, while 40 will be the original + the full Preg+ size/shape.";
                var overrideBellyShapeTitle = "Override AI_Pregnancy belly shape";
                var overrideBellyShapeDescription = "When enabled, the Preg+ belly shape will be the only shape used for pregnancy and inflation.  It disables the AI_Pregnancy shape in main gameplay.";

            #elif HS2
                var storyConfigTitle = "Story/Main-Game Mode";
                var additionalSliderText = "";

                var CumflationTitle = "Cumflation";


                //**** Inflation Config *******/

                AllowCumflation = Config.Bind<bool>(CumflationTitle, "Allow cumflation", false,
                new ConfigDescription("Automatically increase belly InflationSize when cum inside.",
                    null,
                    new ConfigurationManagerAttributes { Order = 4 })
                );

                CumflationMaxLevel = Config.Bind<int>(CumflationTitle, "Cumflation Max Level", 6,
                new ConfigDescription("Inflation levels required to reach max belly size.",
                    null,
                    new ConfigurationManagerAttributes { Order = 3 })
                );

                InflationLevel = Config.Bind<int>(CumflationTitle, "Inflation Level", 1,
                new ConfigDescription("Inflation levels increased when cum inside.",
                    null,
                    new ConfigurationManagerAttributes { Order = 2 })
                );

                DeflationLevel = Config.Bind<int>(CumflationTitle, "Deflation Level", 1,
                new ConfigDescription("Inflation levels decreased when pull out after cum inside.",
                    null,
                    new ConfigurationManagerAttributes { Order = 1 })
                );
            #endif


            //**** Story Mode Config *******/
            //******************************/
            StoryMode = Config.Bind<bool>(storyConfigTitle, "Gameplay Enabled", true,
                new ConfigDescription("Whether or not Preg+ is enabled in Main Game mode",
                    null,
                    new ConfigurationManagerAttributes { Order = 20 })
                );
            StoryMode.SettingChanged += StoryMode_SettingsChanged;

            StoryModeInflationMultiplier = Config.Bind<float>(storyConfigTitle, "Global Multiplier Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Multiplier' amount in story mode" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationMultiplier[0], PregnancyPlusGui.SliderRange.InflationMultiplier[1]),
                    new ConfigurationManagerAttributes { Order = 18 })
                );
            StoryModeInflationMultiplier.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationRoundness = Config.Bind<float>(storyConfigTitle, "Global Roundness Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Roundness' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationRoundness[0], PregnancyPlusGui.SliderRange.InflationRoundness[1]),
                    new ConfigurationManagerAttributes { Order = 17 })
                );
            StoryModeInflationRoundness.SettingChanged += InflationConfig_SettingsChanged;            
            
            StoryModeInflationMoveY = Config.Bind<float>(storyConfigTitle, "Global Move Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Move Y' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationMoveY[0], PregnancyPlusGui.SliderRange.InflationMoveY[1]),
                    new ConfigurationManagerAttributes { Order = 16 })
                );
            StoryModeInflationMoveY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationMoveZ = Config.Bind<float>(storyConfigTitle, "Global Move Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Move Z' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationMoveZ[0], PregnancyPlusGui.SliderRange.InflationMoveZ[1]),
                    new ConfigurationManagerAttributes { Order = 15 })
                );
            StoryModeInflationMoveZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationStretchX = Config.Bind<float>(storyConfigTitle, "Global Stretch X Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Stretch X' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationStretchX[0], PregnancyPlusGui.SliderRange.InflationStretchX[1]),
                    new ConfigurationManagerAttributes { Order = 14 })
                );
            StoryModeInflationStretchX.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationStretchY = Config.Bind<float>(storyConfigTitle, "Global Stretch Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Stretch Y' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationStretchY[0], PregnancyPlusGui.SliderRange.InflationStretchY[1]),
                    new ConfigurationManagerAttributes { Order = 13 })
                );
            StoryModeInflationStretchY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftY = Config.Bind<float>(storyConfigTitle, "Global Shift Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Y' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationShiftY[0], PregnancyPlusGui.SliderRange.InflationShiftY[1]),
                    new ConfigurationManagerAttributes { Order = 12 })
                );
            StoryModeInflationShiftY.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationShiftZ = Config.Bind<float>(storyConfigTitle, "Global Shift Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Shift Z' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationShiftZ[0], PregnancyPlusGui.SliderRange.InflationShiftZ[1]),
                    new ConfigurationManagerAttributes { Order = 11 })
                );
            StoryModeInflationShiftZ.SettingChanged += InflationConfig_SettingsChanged;

            StoryModeInflationTaperY = Config.Bind<float>(storyConfigTitle, "Global Taper Y Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Y' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationTaperY[0], PregnancyPlusGui.SliderRange.InflationTaperY[1]),
                    new ConfigurationManagerAttributes { Order = 10 })
                );
            StoryModeInflationTaperY.SettingChanged += InflationConfig_SettingsChanged; 

            StoryModeInflationTaperZ = Config.Bind<float>(storyConfigTitle, "Global Taper Z Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the 'Taper Z' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationTaperZ[0], PregnancyPlusGui.SliderRange.InflationTaperZ[1]),
                    new ConfigurationManagerAttributes { Order = 9 })
                );
            StoryModeInflationTaperZ.SettingChanged += InflationConfig_SettingsChanged;  
            
            StoryModeInflationDrop = Config.Bind<float>(storyConfigTitle, "Global Drop Adjustment", 0, 
                new ConfigDescription("Allows you to increase the 'Drop' amount in story mode for" + additionalSliderText,
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationDrop[0], PregnancyPlusGui.SliderRange.InflationDrop[1]),
                    new ConfigurationManagerAttributes { Order = 8 })
                );
            StoryModeInflationDrop.SettingChanged += InflationConfig_SettingsChanged;  

            StoryModeInflationClothOffset = Config.Bind<float>(storyConfigTitle, "Global Cloth Offset Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the cloth layer distance to reduce clipping",
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationClothOffset[0], PregnancyPlusGui.SliderRange.InflationClothOffset[1]),
                    new ConfigurationManagerAttributes { Order = 7 })
                );
            StoryModeInflationClothOffset.SettingChanged += InflationConfig_SettingsChanged; 

            StoryModeInflationFatFold = Config.Bind<float>(storyConfigTitle, "Global Fat Fold Adjustment", 0, 
                new ConfigDescription("Allows you to increase or decrease the fat fold size, 0 for none",
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationFatFold[0], PregnancyPlusGui.SliderRange.InflationFatFold[1]),
                    new ConfigurationManagerAttributes { Order = 6 })
                );
            StoryModeInflationFatFold.SettingChanged += InflationConfig_SettingsChanged;  


            StoryModeInflationFatFoldHeight = Config.Bind<float>(storyConfigTitle, "Global Fat Fold Height Adjustment", 0, 
                new ConfigDescription("Allows you to raise or lower the fat fold crease position, 0 is default",
                    new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationFatFoldHeight[0], PregnancyPlusGui.SliderRange.InflationFatFoldHeight[1]),
                    new ConfigurationManagerAttributes { Order = 5 })
                );
            StoryModeInflationFatFoldHeight.SettingChanged += InflationConfig_SettingsChanged;  
                    

            #if KK || AI
                //This config is for KK/AI_Pregnancy integration to set the additional size this plugin will add to KK/AI_Pregnancy
                MaxStoryModeBelly = Config.Bind<float>(maxBellySizeTitle, "Max additional belly size", 10f, 
                    new ConfigDescription(maxBellySizeDescription,
                        new AcceptableValueRange<float>(PregnancyPlusGui.SliderRange.InflationSize[0], PregnancyPlusGui.SliderRange.InflationSize[1]),
                        new ConfigurationManagerAttributes { Order = 1 })
                    );
                MaxStoryModeBelly.SettingChanged += InflationConfig_SettingsChanged;


                //Allows us to override the default KK_Pregnancy belly shape with out own.  For when we don't want to mix them.
                OverrideBelly = Config.Bind<bool>(maxBellySizeTitle, overrideBellyShapeTitle, false,
                new ConfigDescription(overrideBellyShapeDescription,
                    null,
                    new ConfigurationManagerAttributes { Order = 1 })
                );
                OverrideBelly.SettingChanged += OverrideBelly_SettingsChanged;
            #endif
                    

            //Live inflation in story mode.  Increase or decrease base inflationSize with a keybinding press
            StoryModeInflationIncrease  = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation + Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to increase the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters",
                    null,
                    new ConfigurationManagerAttributes { Order = 30 })
                );

            StoryModeInflationDecrease = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation - Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to decrease the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters",
                    null,
                    new ConfigurationManagerAttributes { Order = 29 })
                );

            StoryModeInflationReset = Config.Bind<KeyboardShortcut>("Live Inflation Shortcuts", "Inflation reset Key", new KeyboardShortcut(),
                new ConfigDescription("Allows you to reset the belly InflationSize in Story/Main-Game Mode\r\n\r\nCan be CPU heavy with many characters",
                    null,
                    new ConfigurationManagerAttributes { Order = 28 })
                );
        }


        internal void StoryMode_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//Don't allow toggle event in studio
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" StoryMode_SettingsChanged > {StoryMode.Value}");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            if (StoryMode.Value) 
            {
                //Re trigger inflation and recalculate vert positions
                foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
                { 
                    #if KK || AI //In kk we want to use KK_pregnancy weeks to determine the belly size
                        charCustFunCtrl.GetWeeksAndSetBellySize(checkNewMesh: true);                     

                    #elif HS2 //In HS2 we set the belly size based on the plugin config slider
                        charCustFunCtrl.MeshInflate(new MeshInflateFlags(charCustFunCtrl, _checkForNewMesh: true), "StoryMode_SettingsChanged");                     

                    #endif                    
                }
            } 
            else 
            {
                //Disable all mesh inflations
                foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances) 
                {  
                    charCustFunCtrl.ResetInflation();        
                }
            }
        }


        internal void OverrideBelly_SettingsChanged(object sender, System.EventArgs e) 
        {            
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//Don't allow toggle event in studio
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" OverrideBelly_SettingsChanged > {OverrideBelly.Value}");
        
            //Make sure the belly can reach its normal size when at full term 
            if (OverrideBelly != null && OverrideBelly.Value) MaxStoryModeBelly.Value = 40f;
            if (OverrideBelly != null && !OverrideBelly.Value) MaxStoryModeBelly.Value = 10f;//Otherwise back to default

            //The next time the KK_Pregnancy bone modifier triggers it will set the correct size based on OverrideBelly.Value
        }


        internal void InflationConfig_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//dont allow toggle event in studio
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" InflationConfig_SettingsChanged ");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);

            //Re trigger infaltion when a value changes for each controller
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances) 
            {  
                if (PregnancyPlusPlugin.StoryMode != null && PregnancyPlusPlugin.StoryMode.Value) 
                {            
                    #if KK || AI //custom integration with KK_Pregnancy    
                        charCustFunCtrl.GetWeeksAndSetBellySize(checkNewMesh: true, slidersChanged: true);    

                    #elif HS2
                        //Need to recalculate mesh position when sliders change here
                        charCustFunCtrl.MeshInflate(new MeshInflateFlags(charCustFunCtrl, _pluginConfigSliderChanged: true), "InflationConfig_SettingsChanged");       

                    #endif             
                }                             
            }                  
        }

        internal void MakeBalloon_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" MakeBalloon_SettingsChanged ");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
            {                 
                //Force recalculate all verts.  With balloon active it will automatically calaulcate the correct new boundaries
                charCustFunCtrl.MeshInflate(new MeshInflateFlags(charCustFunCtrl, _checkForNewMesh: true, _freshStart: true), "MakeBalloon_SettingsChanged");                                       
            }    
        }


        internal void DebugVerts_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" DebugVerts_SettingsChanged ");
            var handlers = CharacterApi.GetRegisteredBehaviour(GUID);
        
            foreach (PregnancyPlusCharaController charCustFunCtrl in handlers.Instances)
            {                 
                //Force recalculate all verts.  With MeshDebug active it will automatically calaulcate the correct new debug shape
                charCustFunCtrl.MeshInflate(new MeshInflateFlags(charCustFunCtrl, _checkForNewMesh: true, _freshStart: true), "DebugVerts_SettingsChanged");                                       
            }    
        }


        //Update Error Code log setting when changed
        internal void DebugLog_SettingsChanged(object sender, System.EventArgs e) 
        {
            if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($" DebugLog_SettingsChanged ");
            errorCodeCtrl.SetDebugLogState(PregnancyPlusPlugin.DebugLog.Value);
        }

    }
}
