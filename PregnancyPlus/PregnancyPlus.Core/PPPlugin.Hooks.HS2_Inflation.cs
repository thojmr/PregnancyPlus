using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Studio;
using System;
using System.Linq;
using KKAPI.Chara;
using BepInEx.Configuration;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        //Contains all the hooks for HS2 inflation logic
        public static class Hooks_HS2_Inflation
        {
            //flags for deflate process
            private static bool _lastPullProc;
            private static string _lastInflatedFemaleName;


            public static void InitHooks(Harmony harmonyInstance)
            {                                    
                TryPatchHS2FinishProcs(harmonyInstance);
            }


            /// <summary>
            /// Because HS2 and HS2_VR Assembly-csharp methods differ in method arguments, we have to patch them this way
            /// </summary>
            public static void TryPatchHS2FinishProcs(Harmony hi)
            {                
                var procTypeF1M2 = Type.GetType($"MultiPlay_F1M2, Assembly-CSharp", false);
                if (procTypeF1M2 == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.MultiPlay_F1M2, (please report this)");
                        return;
                }

                var procMethodF1m2 = procTypeF1M2.GetMethod("Proc", AccessTools.all);
                if (procMethodF1m2 == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.MultiPlay_F1M2.Proc - something isn't right, please report this");
                    return;                        
                }

                var procTypeF2M1 = Type.GetType($"MultiPlay_F2M1, Assembly-CSharp", false);
                if (procTypeF2M1 == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.MultiPlay_F2M1, (please report this)");
                        return;
                }

                var procMethodF2m1 = procTypeF2M1.GetMethod("Proc", AccessTools.all);
                if (procMethodF2m1 == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.MultiPlay_F2M1.Proc - something isn't right, please report this");
                    return;                        
                }


                var procTypeSonyu = Type.GetType($"Sonyu, Assembly-CSharp", false);
                if (procTypeSonyu == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.Sonyu, (please report this)");
                        return;
                }

                var procMethodSonyu = procTypeSonyu.GetMethod("Proc", AccessTools.all);
                if (procMethodSonyu == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.Sonyu.Proc - something isn't right, please report this");
                    return;                        
                }


                var procTypeHoushi = Type.GetType($"Houshi, Assembly-CSharp", false);
                if (procTypeHoushi == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.Houshi, (please report this)");
                        return;
                }

                var procMethodHoushi = procTypeHoushi.GetMethod("Proc", AccessTools.all);
                if (procMethodHoushi == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find Assembly-Csharp.Houshi.Proc - something isn't right, please report this");
                    return;                        
                }
                

                hi.Patch(procMethodF1m2,
                        prefix: new HarmonyMethod(typeof(Hooks_HS2_Inflation), nameof(Hooks_HS2_Inflation.MultiPlay_F2M1_Proc)));                                            
                hi.Patch(procMethodF2m1,
                        prefix: new HarmonyMethod(typeof(Hooks_HS2_Inflation), nameof(Hooks_HS2_Inflation.MultiPlay_F1M2_Proc)));                                            
                hi.Patch(procMethodSonyu,
                        prefix: new HarmonyMethod(typeof(Hooks_HS2_Inflation), nameof(Hooks_HS2_Inflation.Sonyu_Proc)));                                            
                hi.Patch(procMethodHoushi,
                        prefix: new HarmonyMethod(typeof(Hooks_HS2_Inflation), nameof(Hooks_HS2_Inflation.Houshi_Proc)));                                            
            }


            //HS2 inflation trigger hooks
            public static void MultiPlay_F2M1_Proc(MultiPlay_F2M1 __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
            {
                //Get current user button click type
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                DetermineInflationState(ctrlFlag, _infoAnimList, "MultiPlay_F2M1_Proc");
            }

            public static void MultiPlay_F1M2_Proc(MultiPlay_F1M2 __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
            {
                //Get current user button click type
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                DetermineInflationState(ctrlFlag, _infoAnimList, "MultiPlay_F1M2_Proc");
            }

            public static void Sonyu_Proc(Sonyu __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
            {
                //Get current user button click type
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();                                    
                DetermineInflationState(ctrlFlag, _infoAnimList, "Sonyu_Proc");      
            }

            public static void Houshi_Proc(Houshi __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
            {
                //Get current user button click type
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();                
                DetermineInflationState(ctrlFlag, _infoAnimList, "Houshi_Proc");                                   
            }



            /// <summary>
            /// When user clicks finish button, set the inflation based on the button clicked
            /// </summary>
            private static void DetermineInflationState(HSceneFlagCtrl ctrlFlag, HScene.AnimationListInfo _infoAnimList,string source=null)
            {
                if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishInSide 
                    || ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishSame  
                    || ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"DetermineInflationState {ctrlFlag.click.ToString()} from {source}");

                    var InflationCharaTargetCtrl = DetermineInflationTarget(ctrlFlag, _infoAnimList);

                    if (InflationCharaTargetCtrl == null)
                        PregnancyPlusPlugin.Logger.LogWarning($"Cant determine which female to inflate");
                    else
                    {
                        TriggerInflation(InflationCharaTargetCtrl);
                        _lastPullProc = false;
                    }
                }     
                //spit/ finish outside clicked (deflate)
                else if (ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishOutSide 
                    || ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit) 
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"DetermineInflationState (deflate) {ctrlFlag.click.ToString()} from {source}");

                    var InflationCharaTargetCtrl = DetermineInflationTarget(ctrlFlag, _infoAnimList);

                    if (InflationCharaTargetCtrl == null)
                        PregnancyPlusPlugin.Logger.LogWarning($"Cant determine which female to deflate");
                    else
                    {
                        TriggerInflation(InflationCharaTargetCtrl, deflate: true);
                        _lastPullProc = false;
                    }
                } 

            }

            /// <summary>
            /// Trigger inflation/deflation effect in HS2.
            /// </summary>
            private static void TriggerInflation(PregnancyPlusCharaController charCustFunCtrl, bool deflate = false)
            {
                if (charCustFunCtrl == null) return;
                if (StudioAPI.InsideStudio || MakerAPI.InsideMaker) return;//Don't allow in studio/maker
                if (!StoryMode.Value || !AllowCumflation.Value) return;
                                    
                if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"Trigger {(deflate?"deflation": "inflation")} to {charCustFunCtrl.name}:{charCustFunCtrl.charaFileName}");

                charCustFunCtrl.HS2Inflation(deflate);
            }

            /// <summary>
            /// determine which female to inflate in HS2 includs multiplay mode.
            /// Check value _infoAnimList.fileFemale, 2rd value count from bottom up is what we need.
            /// example:h2_mf2_f1_01
            ///                 ^This value indicates which female is inserted.
            /// </summary>      
            private static PregnancyPlusCharaController DetermineInflationTarget(HSceneFlagCtrl ctrlFlag, HScene.AnimationListInfo _infoAnimList)
            {
                var CharaControllers = CharacterApi.GetRegisteredBehaviour(GUID).Instances;
                PregnancyPlusCharaController InflationCharaTarget = null;

                if (string.IsNullOrEmpty(_infoAnimList.fileFemale2) == false)
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"HScene is multiplay F2M1");

                    if (_infoAnimList.fileFemale == "h2_mf2_f1_01" || _infoAnimList.fileFemale == "h2_mf2_f2_04")
                    {
                        if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"Ignore inflation when sumata/intercrural sex.");
                    }
                    else
                    {
                    
                        var fileFemaleSplit = _infoAnimList.fileFemale.Split('_').Reverse().ToArray();
                        if (fileFemaleSplit.Length < 3)
                            PregnancyPlusPlugin.Logger.LogWarning($"bad _infoAnimList.fileFemale:{_infoAnimList.fileFemale}");
                        else
                        {
                            if (fileFemaleSplit[1] == "f1")
                                InflationCharaTarget = GetCharaCtrlByName("chaF_001");
                            else if (fileFemaleSplit[1] == "f2")
                                InflationCharaTarget = GetCharaCtrlByName("chaF_002");
                            else
                                PregnancyPlusPlugin.Logger.LogWarning($"bad fileFemaleSplit[1]:{fileFemaleSplit[1]}");
                        }
                    }
                }
                else // Normal mode
                {
                    InflationCharaTarget = GetCharaCtrlByName("chaF_001");
                    if (InflationCharaTarget == null) InflationCharaTarget = GetCharaCtrlByName("chaF_002");
                }   

                _lastInflatedFemaleName = InflationCharaTarget?.name;

                return InflationCharaTarget;
            }



            //HS2 deflation trigger logics
            [HarmonyPrefix]
            [HarmonyPatch(typeof(Sonyu), "PullProc", typeof(float), typeof(int))]
            public static void Sonyu_PullProc(Sonyu __instance)
            {
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                DetermineDeflationState(ctrlFlag, "Sonyu_PullProc");
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MultiPlay_F1M2), "PullProc", typeof(float), typeof(int))]
            public static void MultiPlay_F1M2_PullProc(MultiPlay_F1M2 __instance)
            {
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                DetermineDeflationState(ctrlFlag, "MultiPlay_F1M2_PullProc");
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MultiPlay_F2M1), "PullProc", typeof(float), typeof(int))]
            public static void MultiPlay_F2M1_PullProc(MultiPlay_F2M1 __instance)
            {
                var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                DetermineDeflationState(ctrlFlag, "MultiPlay_F2M1_PullProc");
            }

            /// <summary>
            /// deflate female chara when pull out
            /// </summary>
            private static void DetermineDeflationState(HSceneFlagCtrl ctrlFlag, string source = null)
            {

                if (ctrlFlag.isInsert
                    && _lastPullProc != ctrlFlag.isInsert
                    && !string.IsNullOrEmpty(_lastInflatedFemaleName))
                {
                    if (PregnancyPlusPlugin.DebugLog.Value) PregnancyPlusPlugin.Logger.LogInfo($"DetermineDeflationState from {source}");

                    var CharaTarget = GetCharaCtrlByName(_lastInflatedFemaleName);

                    if (CharaTarget == null)
                        PregnancyPlusPlugin.Logger.LogWarning($"Cant determine which female to deflate");
                    else
                    {
                        TriggerInflation(CharaTarget, deflate: true);
                        _lastInflatedFemaleName = null;
                    }
                }

                _lastPullProc = ctrlFlag.isInsert;
            }

            private static PregnancyPlusCharaController GetCharaCtrlByName(string CharaName)
            {
                if (string.IsNullOrEmpty(CharaName)) 
                    return null;

                var CharaControllers = CharacterApi.GetRegisteredBehaviour(GUID).Instances;
                return CharaControllers.FirstOrDefault(x => x.name == CharaName) as PregnancyPlusCharaController;
            }

        }
    }
}
