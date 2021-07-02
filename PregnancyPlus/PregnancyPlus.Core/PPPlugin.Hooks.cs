using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Studio;
using KKAPI.Chara;
using System.Linq;
#if AI || HS2
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#endif


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        /// <summary>
        /// Provides access to methods for getting and setting clothes state changes to a specific CharCustomFunctionController.
        /// </summary>
        /// <param name="chaControl"></param>
        /// <returns>KKAPI character controller</returns>
        public static PregnancyPlusCharaController GetCharaController(ChaControl chaControl) => (chaControl == null)
            ? null 
            : chaControl.gameObject.GetComponent<PregnancyPlusCharaController>();



        private static class Hooks
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                harmonyInstance.PatchAll(typeof(Hooks));
            }

            #if HS2


                //HS2 inflation trigger logics
                [HarmonyPrefix]
                [HarmonyPatch(typeof(MultiPlay_F2M1), "Proc", typeof(int), typeof(HScene.AnimationListInfo))]
                public static void MultiPlay_F2M1_Proc(MultiPlay_F2M1 __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
                {
                    //Get current user button click type
                    var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                    DetermineInflationState(ctrlFlag, _infoAnimList, "MultiPlay_F2M1_Proc");
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(MultiPlay_F1M2), "Proc", typeof(int), typeof(HScene.AnimationListInfo))]
                public static void MultiPlay_F1M2_Proc(MultiPlay_F1M2 __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
                {
                    //Get current user button click type
                    var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                    DetermineInflationState(ctrlFlag, _infoAnimList, "MultiPlay_F1M2_Proc");
                }

                [HarmonyPrefix]
                [HarmonyPatch(typeof(Sonyu), "Proc", typeof(int), typeof(HScene.AnimationListInfo))]
                public static void Sonyu_Proc(Sonyu __instance, int _modeCtrl, HScene.AnimationListInfo _infoAnimList)
                {
                    //Get current user button click type
                    var ctrlFlag = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();                                    
                    DetermineInflationState(ctrlFlag, _infoAnimList, "Sonyu_Proc");      
                }


                [HarmonyPrefix]
                [HarmonyPatch(typeof(Houshi), "Proc", typeof(int), typeof(HScene.AnimationListInfo))]
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
                        || ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishDrink
                        || ctrlFlag.click == HSceneFlagCtrl.ClickKind.FinishVomit) 
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
                    }   

                    _lastInflatedFemaleName = InflationCharaTarget?.name;

                    return InflationCharaTarget;
                }


                //flags for deflate process
                private static bool _lastPullProc;
                private static string _lastInflatedFemaleName;

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
                            TriggerInflation(CharaTarget, true);
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
#endif


            /// <summary>
            /// Trigger the ClothesStateChangeEvent for toggling on and off a clothing item
            /// </summary>
            [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetClothesState))]
            private static void ChaControl_SetClothesState(ChaControl __instance, int clothesKind)
            {
                //Ignore gloves, shoes, socks
                if (IsIgnoredClothing(clothesKind)) return;

                var controller = GetCharaController(__instance);
                if (controller == null) return;
            
                //Send event to the CustomCharaFunctionController that the clothes were changed on
                controller.ClothesStateChangeEvent(__instance.chaID, clothesKind);                                
            }


            /// <summary>
            /// Trigger the ClothesStateChangeEvent when changing custom outfits in maker
            /// </summary>
            [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCustomClothes))]
            private static void ChaControl_ChangeCustomClothes(ChaControl __instance, int kind)
            {

                //Ignore gloves, shoes, socks
                if (IsIgnoredClothing(kind)) return;

                if (MakerAPI.InsideAndLoaded)
                {
                    var controller = GetCharaController(__instance);
                    if (controller == null) return;
                
                    //Send event to the CustomCharaFunctionController that the clothes were changed on
                    controller.ClothesStateChangeEvent(__instance.chaID, kind);  
                }
            }

            #if HS2 || AI
                /// <summary>
                /// When HS2WearCustom changes clothing (catches clothes change that the above does not)
                /// </summary>
                [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothesAsync), typeof(int), typeof(int), typeof(bool), typeof(bool))]
                private static void ChaControl_ChangeClothesAsync(ChaControl __instance, int kind, int id, bool forceChange, bool asyncFlags)
                {
                    //Dont ignore any clothes types here, since they can come with additional uncensor mesh as well (like Squeeze Socks)
                    if (StudioAPI.InsideStudio || MakerAPI.InsideAndLoaded)
                    {
                        var controller = GetCharaController(__instance);
                        if (controller == null) return;
                    
                        //Send event to the CustomCharaFunctionController that the clothes were changed on
                        controller.ClothesStateChangeEvent(__instance.chaID, kind);  
                    }
                }
            #endif


            #if KK
                /// <summary>
                /// When a character becomes visible let preg+ controller know, in main game mode only
                /// </summary>
                [HarmonyPostfix]
                [HarmonyPatch(typeof(ChaControl), "UpdateForce")]
                private static void VisibilityStateEvent(ChaControl __instance)
                {
                    //Only continue in main game mode
                    if (!__instance.loadEnd || !PregnancyPlusPlugin.StoryMode.Value || StudioAPI.InsideStudio || MakerAPI.InsideAndLoaded)
                    {
                        return;
                    }

                    bool newState = __instance.rendBody.isVisible;

                    //Send current visible state to each character's preg+ controller                    
                    var controller = GetCharaController(__instance);
                    if (controller == null) return;

                    controller.CheckVisibilityState(newState);
                }

            #endif


            /// <summary>
            /// Ignore gloves, socks, and shoes since they dont affect the belly area
            /// </summary>
            internal static bool IsIgnoredClothing(int clothesKind) 
            {
                #if KK
                    return (clothesKind == (int)ChaFileDefine.ClothesKind.gloves || clothesKind == (int)ChaFileDefine.ClothesKind.socks || clothesKind == (int)ChaFileDefine.ClothesKind.shoes_inner || clothesKind == (int)ChaFileDefine.ClothesKind.shoes_outer);
                #elif HS2 || AI
                    return (clothesKind == (int)ChaFileDefine.ClothesKind.gloves || clothesKind == (int)ChaFileDefine.ClothesKind.socks || clothesKind == (int)ChaFileDefine.ClothesKind.shoes);
                #endif
            }

        }
    }
}
