using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    //This partial class contatins all of the Studi GUI 
    public static partial class PregnancyPlusGui
    {

        internal static void InitStudio(Harmony hi, PregnancyPlusPlugin instance)
        {

            if (StudioAPI.InsideStudio)
            {
                RegisterStudioControls();
            }
        }

        private static void RegisterStudioControls()
        {
            var cat = StudioAPI.GetOrCreateCurrentStateCategory("Pregnancy +");

            cat.AddControl(new CurrentStateCategorySwitch("Reset P+ Shape", c =>
                {                     
                    var ctrl = GetCharCtrl(c);  
                    return false;
                }))
                .Value.Subscribe(f => {
                    if (f == false) return;

                    foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {    
                        //Clear current config
                        ctrl.infConfig = new PregnancyPlusData();             
                        ctrl.MeshInflate();  
                    }
                });
            
            cat.AddControl(new CurrentStateCategorySwitch("Restore Last P+ Shape", c =>
                {                                         
                    var ctrl = GetCharCtrl(c);   
                    return false;
                }))
                .Value.Subscribe(f => {
                    if (f == false) return;

                    foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {     
                        if (PregnancyPlusPlugin.lastBellyState.HasAnyValue()) {
                            //Update confing with last stored non zero values values
                            ctrl.infConfig = PregnancyPlusPlugin.lastBellyState;             
                            ctrl.MeshInflate();                 
                        }                            
                    }
                 });

            cat.AddControl(new CurrentStateCategorySlider("Pregnancy +", c =>
                {   
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationSize : 0;

                }, 
                    SliderRange.inflationSize[0], 
                    SliderRange.inflationSize[1]
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationSize == f) continue;    
                            ctrl.infConfig.inflationSize = f;                
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Multiplier", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationMultiplier: 0;

                }, 
                    SliderRange.inflationMultiplier[0], 
                    SliderRange.inflationMultiplier[1]
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationMultiplier == f) continue;     
                            ctrl.infConfig.inflationMultiplier = f;               
                            ctrl.MeshInflate();                             
                        }
                    });                    

            cat.AddControl(new CurrentStateCategorySlider("        Move Y", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationMoveY: 0;

                }, 
                    SliderRange.inflationMoveY[0] * scaleLimits, 
                    SliderRange.inflationMoveY[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationMoveY == f) continue;                    
                            ctrl.infConfig.inflationMoveY = f;
                            ctrl.MeshInflate();                             
                        }
                    });
            
            cat.AddControl(new CurrentStateCategorySlider("        Move Z", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationMoveZ: 0;

                }, 
                    SliderRange.inflationMoveZ[0] * scaleLimits, 
                    SliderRange.inflationMoveZ[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationMoveZ == f) continue;   
                            ctrl.infConfig.inflationMoveZ = f;                 
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Stretch X", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationStretchX: 0;

                }, 
                    SliderRange.inflationStretchX[0] * scaleLimits, 
                    SliderRange.inflationStretchX[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationStretchX == f) continue;                    
                            ctrl.infConfig.inflationStretchX = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Stretch Y", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationStretchY: 0;
                    
                }, 
                    SliderRange.inflationStretchY[0] * scaleLimits, 
                    SliderRange.inflationStretchY[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationStretchY == f) continue;                    
                            ctrl.infConfig.inflationStretchY = f;
                            ctrl.MeshInflate();                             
                        }
                    });        
            
            cat.AddControl(new CurrentStateCategorySlider("        Shift Y", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationShiftY: 0;

                }, 
                    SliderRange.inflationShiftY[0]  * scaleLimits, 
                    SliderRange.inflationShiftY[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationShiftY == f) continue;                    
                            ctrl.infConfig.inflationShiftY = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Shift Z", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationShiftZ: 0;

                }, 
                    SliderRange.inflationShiftZ[0] * scaleLimits, 
                    SliderRange.inflationShiftZ[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationShiftZ == f) continue;                    
                            ctrl.infConfig.inflationShiftZ = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Taper Y", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationTaperY: 0;
                    
                }, 
                    SliderRange.inflationTaperY[0] * scaleLimits, 
                    SliderRange.inflationTaperY[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationTaperY == f) continue;                    
                            ctrl.infConfig.inflationTaperY = f;
                            ctrl.MeshInflate();                             
                        }
                    });

            cat.AddControl(new CurrentStateCategorySlider("        Taper Z", c =>
                {                                       
                    var ctrl = GetCharCtrl(c);                                                   
                    return ctrl != null ? ctrl.infConfig.inflationTaperZ: 0;
                    
                }, 
                    SliderRange.inflationTaperZ[0] * scaleLimits, 
                    SliderRange.inflationTaperZ[1] * scaleLimits
                ))
                    .Value.Subscribe(f => { 
                        foreach (var ctrl in StudioAPI.GetSelectedControllers<PregnancyPlusCharaController>()) {  
                            if (ctrl.infConfig.inflationTaperZ == f) continue;                    
                            ctrl.infConfig.inflationTaperZ = f;
                            ctrl.MeshInflate();                             
                        }
                    });
        }

        internal static PregnancyPlusCharaController GetCharCtrl(Studio.OCIChar c) {
            if (c.charInfo == null) return null;
            var controller = c.charInfo.GetComponent<PregnancyPlusCharaController>();
            if (controller == null) return null;    
            return controller;
        }

    }
}
