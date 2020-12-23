using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;

namespace KK_PregnancyPlus
{
    public static partial class PregnancyPlusGui
    {
        private static PregnancyPlusPlugin _pluginInstance;

        internal static void Init(Harmony hi, PregnancyPlusPlugin instance)
        {
            _pluginInstance = instance;

            if (StudioAPI.InsideStudio)
            {
                RegisterStudioControls();
            }
        }

        private static void RegisterStudioControls()
        {
            var cat = StudioAPI.GetOrCreateCurrentStateCategory(null);

            cat.AddControl(new CurrentStateCategorySlider("Inflate Mesh", c =>
                {                                       
                    if (c.charInfo == null) return 0;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 0;   
                    var exists = controller.infConfig.TryGetValue("inflationSize", out float inflationSize);  
                    if (!exists) controller.infConfig["inflationSize"] = 0;                        
                    return controller.infConfig["inflationSize"];
                }, 0, 40))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationSize"] == f) continue;    
                            ctrl.infConfig["inflationSize"] = f;                
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Move Y", c =>
                {                                       
                    if (c.charInfo == null) return 0;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 0;    
                    var exists = controller.infConfig.TryGetValue("inflationMoveY", out float inflationMoveY);  
                    if (!exists) controller.infConfig["inflationMoveY"] = 0; 
                    return controller.infConfig["inflationMoveY"];
                }, -0.20f, 0.20f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationMoveY"] == f) continue;                    
                            ctrl.infConfig["inflationMoveY"] = f;
                            ctrl.MeshInflate();                             
                        }
                    });
            
            cat.AddControl(new CurrentStateCategorySlider("        Move Z", c =>
                {                                       
                    if (c.charInfo == null) return 0;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 0; 
                    var exists = controller.infConfig.TryGetValue("inflationMoveZ", out float inflationMoveZ);  
                    if (!exists) controller.infConfig["inflationMoveZ"] = 0; 
                    return controller.infConfig["inflationMoveZ"];
                }, -0.1f, 0.1f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationMoveZ"] == f) continue;   
                            ctrl.infConfig["inflationMoveZ"] = f;                 
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Stretch X", c =>
                {                                       
                    if (c.charInfo == null) return 1;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 1;    
                    var exists = controller.infConfig.TryGetValue("inflationStretchX", out float inflationStretchX);  
                    if (!exists) controller.infConfig["inflationStretchX"] = 0; 
                    return controller.infConfig["inflationStretchX"];
                }, -0.25f, 0.25f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationStretchX"] == f) continue;                    
                            ctrl.infConfig["inflationStretchX"] = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Stretch Y", c =>
                {                                       
                    if (c.charInfo == null) return 1;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 1;    
                    var exists = controller.infConfig.TryGetValue("inflationStretchY", out float inflationStretchY);  
                    if (!exists) controller.infConfig["inflationStretchY"] = 0; 
                    return controller.infConfig["inflationStretchY"];
                }, -0.25f, 0.25f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationStretchY"] == f) continue;                    
                            ctrl.infConfig["inflationStretchY"] = f;
                            ctrl.MeshInflate();                             
                        }
                    });        
            
            cat.AddControl(new CurrentStateCategorySlider("        Shift Y", c =>
                {                                       
                    if (c.charInfo == null) return 0;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 0;    
                    var exists = controller.infConfig.TryGetValue("inflationShiftY", out float inflationShiftY);  
                    if (!exists) controller.infConfig["inflationShiftY"] = 0; 
                    return controller.infConfig["inflationShiftY"];
                }, -0.1f, 0.1f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationShiftY"] == f) continue;                    
                            ctrl.infConfig["inflationShiftY"] = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Shift Z", c =>
                {                                       
                    if (c.charInfo == null) return 0;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 0;    
                    var exists = controller.infConfig.TryGetValue("inflationShiftZ", out float inflationShiftZ);  
                    if (!exists) controller.infConfig["inflationShiftZ"] = 0; 
                    return controller.infConfig["inflationShiftZ"];
                }, -0.1f, 0.1f))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationShiftZ"] == f) continue;                    
                            ctrl.infConfig["inflationShiftZ"] = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Multiplier", c =>
                {                                       
                    if (c.charInfo == null) return 1;
                    var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
                    if (controller == null) return 1; 
                    var exists = controller.infConfig.TryGetValue("inflationMultiplier", out float inflationMultiplier);  
                    if (!exists) controller.infConfig["inflationMultiplier"] = 0; 
                    return controller.infConfig["inflationMultiplier"];
                }, -1, 1))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig["inflationMultiplier"] == f) continue;     
                            ctrl.infConfig["inflationMultiplier"] = f;               
                            ctrl.MeshInflate();                             
                        }
                    });
        }

    }
}
