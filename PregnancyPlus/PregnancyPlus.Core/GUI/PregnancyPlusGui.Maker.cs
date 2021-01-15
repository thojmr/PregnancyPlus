using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //This partial class contains all of the Maker GUI
    public static partial class PregnancyPlusGui
    {        

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
            // Only female characters
            if (MakerAPI.GetMakerSex() == 0) return;

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


            var size = e.AddControl(new MakerSlider(cat, "Inflation Size", SliderRange.inflationSize[0], SliderRange.inflationSize[1], ppDataDefaults.inflationSize, _pluginInstance));
            size.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationSize, (controller, value) => {
                var oldVal = controller.infConfig.inflationSize;
                controller.infConfig.inflationSize = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("The equivalent to number of weeks pregnant for Pregnancy+.", cat, _pluginInstance) { TextColor = hintColor });


            var multiplier = e.AddControl(new MakerSlider(cat, "Inflation Multiplier", SliderRange.inflationMultiplier[0], SliderRange.inflationMultiplier[1], ppDataDefaults.inflationMultiplier, _pluginInstance));
            multiplier.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMultiplier, (controller, value) => {
                var oldVal = controller.infConfig.inflationMultiplier;
                controller.infConfig.inflationMultiplier = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Multiplies the base inflation size by this value.", cat, _pluginInstance) { TextColor = hintColor });


            var moveY = e.AddControl(new MakerSlider(cat, "Move Y", SliderRange.inflationMoveY[0], SliderRange.inflationMoveY[1], ppDataDefaults.inflationMoveY, _pluginInstance));
            moveY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMoveY, (controller, value) => {
                var oldVal = controller.infConfig.inflationMoveY;
                controller.infConfig.inflationMoveY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Moves the belly sphere up and down.", cat, _pluginInstance) { TextColor = hintColor });


            var moveZ = e.AddControl(new MakerSlider(cat, "Move Z", SliderRange.inflationMoveZ[0], SliderRange.inflationMoveZ[1], ppDataDefaults.inflationMoveZ, _pluginInstance));
            moveZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationMoveZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationMoveZ;
                controller.infConfig.inflationMoveZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Moves the belly sphere forward and back.", cat, _pluginInstance) { TextColor = hintColor });


            var stretchX = e.AddControl(new MakerSlider(cat, "Stretch X", SliderRange.inflationStretchX[0], SliderRange.inflationStretchX[1], ppDataDefaults.inflationStretchX, _pluginInstance));
            stretchX.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationStretchX, (controller, value) => {
                var oldVal = controller.infConfig.inflationStretchX;
                controller.infConfig.inflationStretchX = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Stretch the belly wider in the X direction.", cat, _pluginInstance) { TextColor = hintColor });


            var stretchY = e.AddControl(new MakerSlider(cat, "Stretch Y", SliderRange.inflationStretchY[0], SliderRange.inflationStretchY[1], ppDataDefaults.inflationStretchY, _pluginInstance));
            stretchY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationStretchY, (controller, value) => {
                var oldVal = controller.infConfig.inflationStretchY;
                controller.infConfig.inflationStretchY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Stretch the belly taller in the Y direction.", cat, _pluginInstance) { TextColor = hintColor });


            var shiftY = e.AddControl(new MakerSlider(cat, "Shift Y", SliderRange.inflationShiftY[0], SliderRange.inflationShiftY[1], ppDataDefaults.inflationShiftY, _pluginInstance));
            shiftY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationShiftY, (controller, value) => {
                var oldVal = controller.infConfig.inflationShiftY;
                controller.infConfig.inflationShiftY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Shift the front of the belly up and down.", cat, _pluginInstance) { TextColor = hintColor });


            var shiftZ = e.AddControl(new MakerSlider(cat, "Shift Z", SliderRange.inflationShiftZ[0], SliderRange.inflationShiftZ[1], ppDataDefaults.inflationShiftZ, _pluginInstance));
            shiftZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationShiftZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationShiftZ;
                controller.infConfig.inflationShiftZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Shift the front of the belly forward and back.", cat, _pluginInstance) { TextColor = hintColor });


            var taperY = e.AddControl(new MakerSlider(cat, "Taper Y", SliderRange.inflationTaperY[0], SliderRange.inflationTaperY[1], ppDataDefaults.inflationTaperY, _pluginInstance));
            taperY.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationTaperY, (controller, value) => {
                var oldVal = controller.infConfig.inflationTaperY;
                controller.infConfig.inflationTaperY = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Taper the sides of the belly in at the top and out at the bottom.  Makes an egg like shape.", cat, _pluginInstance) { TextColor = hintColor });


            var taperZ = e.AddControl(new MakerSlider(cat, "Taper Z", SliderRange.inflationTaperZ[0], SliderRange.inflationTaperZ[1], ppDataDefaults.inflationTaperZ, _pluginInstance));
            taperZ.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationTaperZ, (controller, value) => {
                var oldVal = controller.infConfig.inflationTaperZ;
                controller.infConfig.inflationTaperZ = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Taper the front of the belly in at the top and out at the bottom.  Gives the belly an angle at the front.", cat, _pluginInstance) { TextColor = hintColor });

            var clothOffset = e.AddControl(new MakerSlider(cat, "Cloth Offset", SliderRange.inflationClothOffset[0], SliderRange.inflationClothOffset[1], ppDataDefaults.inflationClothOffset, _pluginInstance));
            clothOffset.BindToFunctionController<PregnancyPlusCharaController, float>(controller => controller.infConfig.inflationClothOffset, (controller, value) => {
                var oldVal = controller.infConfig.inflationClothOffset;
                controller.infConfig.inflationClothOffset = value;
                if (oldVal != value) OnMakerSettingsChanged(controller);
            });
            e.AddControl(new MakerText("Control the distance between each clothing layer.  Will help reduce clipping.", cat, _pluginInstance) { TextColor = hintColor });
            
        }

        //On any slider change, trigger mesh inflaiton update
        internal static void OnMakerSettingsChanged(PregnancyPlusCharaController controller) {
            if (PregnancyPlusPlugin.debugLog) PregnancyPlusPlugin.Logger.LogInfo($" OnMakerSettingsChanged ");
            controller.MeshInflate(true);                                                                     
        }

    }
}
