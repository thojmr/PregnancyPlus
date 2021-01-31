using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using UnityEngine;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    //This partial class contains all of the Maker GUI
    public static partial class PregnancyPlusGui
    {        
        internal static List<MakerSlider> sliders = new List<MakerSlider>();


        //Slider input titles, and GameObject identifiers
        private static string inflationSizeMaker = "Inflation Size";
        private static string inflationMultiplierMaker = "Inflation Multiplier";
        private static string inflationMoveYMaker = "Move Y";
        private static string inflationMoveZMaker = "Move Z";
        private static string inflationStretchXMaker = "Stretch X";
        private static string inflationStretchYMaker = "Stretch Y";
        private static string inflationShiftYMaker = "Shift Y";
        private static string inflationShiftZMaker = "Shift Z";
        private static string inflationTaperYMaker = "Taper Y";
        private static string inflationTaperZMaker = "Taper Z";
        private static string inflationClothOffsetMaker = "Cloth Offset";
        private static string inflationFatFoldMaker = "Fat Fold";
        private static string inflationRoundnessMaker = "Roundness";

        internal static void InitMaker(Harmony hi, PregnancyPlusPlugin instance)
        {
            _pluginInstance = instance;

            if (!StudioAPI.InsideStudio)
            {
                MakerAPI.RegisterCustomSubCategories += MakerAPI_MakerBaseLoaded;
            }
        }

        internal static void MakerAPI_MakerBaseLoaded(object sender, RegisterSubCategoriesEvent e)
        {
            // Only female characters, unless plugin config says otherwise
            if (!PregnancyPlusPlugin.AllowMale.Value && MakerAPI.GetMakerSex() == 0) return;

            //Set the menu location of the p+ sliders
            #if KK           
                var cat = new MakerCategory(MakerConstants.Parameter.Character.CategoryName, "Pregnancy+");
            #elif HS2 || AI
                var cat = new MakerCategory(MakerConstants.Body.CategoryName, "Pregnancy+");
            #endif
            
            e.AddSubCategory(cat);

            var hintColor = new Color(0.7f, 0.7f, 0.7f);

            var gameplayToggle = e.AddControl(new MakerToggle(cat, "Enable Pregnancy+", true, _pluginInstance));
            gameplayToggle.BindToFunctionController<PregnancyPlusCharaController, bool>(controller => controller.infConfig.GameplayEnabled, (controller, value) => {
                var oldVal = controller.infConfig.GameplayEnabled;
                controller.infConfig.GameplayEnabled = value; 
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("If disabled, you won't see any pregnant effects.", cat, _pluginInstance) { TextColor = hintColor });


            var size = e.AddControl(new MakerSlider(cat, inflationSizeMaker, SliderRange.inflationSize[0], SliderRange.inflationSize[1], ppDataDefaults.inflationSize, _pluginInstance));
            size.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationSize, (controller, value) => {
                var oldVal = controller.infConfig.inflationSize;
                controller.infConfig.inflationSize = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("The equivalent to number of weeks pregnant for Pregnancy+.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(size);


            var multiplier = e.AddControl(new MakerSlider(cat, inflationMultiplierMaker, SliderRange.inflationMultiplier[0], SliderRange.inflationMultiplier[1], ppDataDefaults.inflationMultiplier, _pluginInstance));
            multiplier.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMultiplier, (controller, value) => {
                var oldVal = controller.infConfig.inflationMultiplier;
                controller.infConfig.inflationMultiplier = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Multiplies the base inflation size by this value.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(multiplier);


            var moveY = e.AddControl(new MakerSlider(cat, inflationMoveYMaker, SliderRange.inflationMoveY[0] * scaleLimits, SliderRange.inflationMoveY[1] * scaleLimits, ppDataDefaults.inflationMoveY, _pluginInstance));
            moveY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMoveY, (controller, value) => {
                var oldVal = controller.infConfig.inflationMoveY;
                controller.infConfig.inflationMoveY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Moves the belly sphere up and down.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(moveY);


            var moveZ = e.AddControl(new MakerSlider(cat, inflationMoveZMaker, SliderRange.inflationMoveZ[0] * scaleLimits, SliderRange.inflationMoveZ[1] * scaleLimits, ppDataDefaults.inflationMoveZ, _pluginInstance));
            moveZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMoveZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationMoveZ;
                controller.infConfig.inflationMoveZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Moves the belly sphere forward and back.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(moveZ);


            var stretchX = e.AddControl(new MakerSlider(cat, inflationStretchXMaker, SliderRange.inflationStretchX[0] * scaleLimits, SliderRange.inflationStretchX[1] * scaleLimits, ppDataDefaults.inflationStretchX, _pluginInstance));
            stretchX.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationStretchX, (controller, value) => {
                var oldVal = controller.infConfig.inflationStretchX;
                controller.infConfig.inflationStretchX = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Stretch the belly wider in the X direction.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(stretchX);


            var stretchY = e.AddControl(new MakerSlider(cat, inflationStretchYMaker, SliderRange.inflationStretchY[0] * scaleLimits, SliderRange.inflationStretchY[1] * scaleLimits, ppDataDefaults.inflationStretchY, _pluginInstance));
            stretchY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationStretchY, (controller, value) => {
                var oldVal = controller.infConfig.inflationStretchY;
                controller.infConfig.inflationStretchY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Stretch the belly taller in the Y direction.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(stretchY);


            var shiftY = e.AddControl(new MakerSlider(cat, inflationShiftYMaker, SliderRange.inflationShiftY[0] * scaleLimits, SliderRange.inflationShiftY[1] * scaleLimits, ppDataDefaults.inflationShiftY, _pluginInstance));
            shiftY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationShiftY, (controller, value) => {
                var oldVal = controller.infConfig.inflationShiftY;
                controller.infConfig.inflationShiftY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Shift the front of the belly up and down.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(shiftY);


            var shiftZ = e.AddControl(new MakerSlider(cat, inflationShiftZMaker, SliderRange.inflationShiftZ[0] * scaleLimits, SliderRange.inflationShiftZ[1] * scaleLimits, ppDataDefaults.inflationShiftZ, _pluginInstance));
            shiftZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationShiftZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationShiftZ;
                controller.infConfig.inflationShiftZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Shift the front of the belly forward and back.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(shiftZ);


            var taperY = e.AddControl(new MakerSlider(cat, inflationTaperYMaker, SliderRange.inflationTaperY[0] * scaleLimits, SliderRange.inflationTaperY[1] * scaleLimits, ppDataDefaults.inflationTaperY, _pluginInstance));
            taperY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationTaperY, (controller, value) => {
                var oldVal = controller.infConfig.inflationTaperY;
                controller.infConfig.inflationTaperY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Taper the sides of the belly in at the top and out at the bottom.  Makes an egg like shape.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(taperY);


            var taperZ = e.AddControl(new MakerSlider(cat, inflationTaperZMaker, SliderRange.inflationTaperZ[0] * scaleLimits, SliderRange.inflationTaperZ[1] * scaleLimits, ppDataDefaults.inflationTaperZ, _pluginInstance));
            taperZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationTaperZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationTaperZ;
                controller.infConfig.inflationTaperZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Taper the front of the belly in at the top and out at the bottom.  Gives the belly an angle at the front.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(taperZ);


            var roundness = e.AddControl(new MakerSlider(cat, inflationRoundnessMaker, SliderRange.inflationRoundness[0] * scaleLimits, SliderRange.inflationRoundness[1] * scaleLimits, ppDataDefaults.inflationRoundness, _pluginInstance));
            roundness.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationRoundness, (controller, value) => {
                var oldVal = controller.infConfig.inflationRoundness;
                controller.infConfig.inflationRoundness = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Make the front of the belly more or less round", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(roundness);


            var clothOffset = e.AddControl(new MakerSlider(cat, inflationClothOffsetMaker, SliderRange.inflationClothOffset[0] * scaleLimits, SliderRange.inflationClothOffset[1] * scaleLimits, ppDataDefaults.inflationClothOffset, _pluginInstance));
            clothOffset.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationClothOffset, (controller, value) => {
                var oldVal = controller.infConfig.inflationClothOffset;
                controller.infConfig.inflationClothOffset = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Control the distance between each clothing layer.  Will help reduce clipping.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(clothOffset);


            var fatFold = e.AddControl(new MakerSlider(cat, inflationFatFoldMaker, SliderRange.inflationFatFold[0], SliderRange.inflationFatFold[1], ppDataDefaults.inflationFatFold, _pluginInstance));
            fatFold.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationFatFold, (controller, value) => {
                var oldVal = controller.infConfig.inflationFatFold;
                controller.infConfig.inflationFatFold = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Control the size of the fat fold on the characters belly.  0 for none.  Use the 'Inflation Size' slider first.", cat, _pluginInstance) { TextColor = hintColor });
            sliders.Add(fatFold);




            //Maker state buttons
            var resetBtn = e.AddControl(new MakerButton("Reset All", cat, _pluginInstance));        
            resetBtn.OnClick.AddListener(() => {
                OnResetAll(sliders);
            });
            e.AddControl(new MakerText("Will reset all Pregnancy+ sliders to their default value", cat, _pluginInstance) { TextColor = hintColor });


            var restoreBtn = e.AddControl(new MakerButton("Restore Last Shape", cat, _pluginInstance));
            restoreBtn.OnClick.AddListener(() => {
                OnRestore(sliders);
            });
            e.AddControl(new MakerText("Restores the last set belly shape", cat, _pluginInstance) { TextColor = hintColor });

        }

        //On any slider change, trigger mesh inflaiton update
        internal static void OnMakerSettingsChanged(PregnancyPlusCharaController controller) {
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" OnMakerSettingsChanged ");
            controller.MeshInflate(true);                                                                     
        }


        /// <summary>
        /// On reset all clicked, reset all sliders to default, and reset character belly state
        /// </summary>
        public static void OnResetAll(List<MakerSlider> _sliders)
        {
            if (!MakerAPI.InsideAndLoaded) return;
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" Resetting sliders ");

            //For each slider, reset to last stored character slider values
            foreach (var slider in _sliders) 
            {
                slider.SetValue(0);
            }
        }


        /// <summary>
        /// On Restore, set sliders to last non zero shape, and set characters belly state
        /// </summary>
        public static void OnRestore(List<MakerSlider> _sliders)
        {
            if (!MakerAPI.InsideAndLoaded) return;

            var chaControl = MakerAPI.GetCharacterControl();
            var charCustFunCtrl  = PregnancyPlusHelper.GetCharacterBehaviorController<PregnancyPlusCharaController>(chaControl, PregnancyPlusPlugin.GUID);
            if (charCustFunCtrl == null) return;

            var _infConfig = PregnancyPlusPlugin.lastBellyState;

            //For each slider, set to default which will reset the belly shape
            foreach (var slider in _sliders) 
            {
                //Get the private slider object name from the game GUI
                var settingName = Traverse.Create(slider).Field("_settingName").GetValue<string>();
                if (settingName == null) continue;
                
                if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" Restoring slider > {settingName}");

                //Set the correct slider with it's old config value
                switch (settingName) 
                {
#region Look away! im being lazy again                   
                    case var _ when settingName == inflationSizeMaker://Ohh boy, cant have const and static strings in switch case, thus this was created!
                        slider.SetValue(_infConfig.inflationSize);
                        continue;

                    case var _ when settingName == inflationMultiplierMaker:
                        slider.SetValue(_infConfig.inflationMultiplier);
                        continue;

                    case var _ when settingName == inflationMoveYMaker:
                        slider.SetValue(_infConfig.inflationMoveY);
                        continue;

                    case var _ when settingName == inflationMoveZMaker:
                        slider.SetValue(_infConfig.inflationMoveZ);
                        continue;

                    case var _ when settingName == inflationStretchXMaker:
                        slider.SetValue(_infConfig.inflationStretchX);
                        continue;

                    case var _ when settingName == inflationStretchYMaker:
                        slider.SetValue(_infConfig.inflationStretchY);
                        continue;

                    case var _ when settingName == inflationShiftYMaker:
                        slider.SetValue(_infConfig.inflationShiftY);
                        continue;

                    case var _ when settingName == inflationShiftZMaker:
                        slider.SetValue(_infConfig.inflationShiftZ);
                        continue;

                    case var _ when settingName == inflationTaperYMaker:
                        slider.SetValue(_infConfig.inflationTaperY);
                        continue;

                    case var _ when settingName == inflationTaperZMaker:
                        slider.SetValue(_infConfig.inflationTaperZ);
                        continue;

                    case var _ when settingName == inflationClothOffsetMaker:
                        slider.SetValue(_infConfig.inflationClothOffset);
                        continue;

                    case var _ when settingName == inflationFatFoldMaker:
                        slider.SetValue(_infConfig.inflationFatFold);
                        continue;

                    case var _ when settingName == inflationRoundnessMaker:
                        slider.SetValue(_infConfig.inflationRoundness);
                        continue;

                    default:
                        continue;
#endregion
                }
            }         
        }

    }
}
