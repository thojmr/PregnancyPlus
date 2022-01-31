using HarmonyLib;
#if AI || HS2
    using AIChara;
    using CharaCustom;
#elif KK
    using KKAPI.MainGame; 
    using ChaCustom;   
#endif


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {

        //Hooks for Character Accessory changes (Add, Remove, Copy)
        private static class HooksAccessory
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                harmonyInstance.PatchAll(typeof(HooksAccessory));
            }


        #if KKS || KKS

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CustomAcsSelectKind), nameof(CustomAcsSelectKind.ChangeSlot))]
            public static void ChangeSlotPostfix(CustomAcsSelectKind __instance, int _no)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeSlotPostfix {_no}");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, _no); 
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryKind))]
            public static void UpdateSelectAccessoryKindPostfix(CvsAccessory __instance, ref int __state)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $UpdateSelectAccessoryKindPostfix {__state}");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, (int)__instance.slotNo); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryType))]
            public static void UpdateSelectAccessoryTypePostfix(CvsAccessory __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $UpdateSelectAccessoryTypePostfix");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, (int)__instance.slotNo); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsAccessoryCopy), "CopyAcs")]
            public static void CopyCopyAcsPostfix(CvsAccessoryCopy __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $CopyCopyAcsPostfix");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, -1); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
            public static void ChangeCoordinateTypePostfix(ChaControl __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeCoordinateTypePostfix");
                var controller = GetCharaController(__instance);
                if (controller == null) return;
                controller.AccessoryStateChangeEvent(__instance.chaID, -1); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsAccessoryChange), "CopyAcs")]
            public static void ChangeCopyAcsPostfix(CvsAccessoryChange __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeCopyAcsPostfix");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                #if KKS
                    var slotNum = __instance.selDst;
                #elif KK
                    var slotNum = Traverse.Create(__instance).Field("selDst").GetValue<int>();
                #endif

                controller.AccessoryStateChangeEvent(chaControl.chaID, slotNum); 
            }

        #endif


        #if AI || HS2

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Slot), nameof(CvsA_Slot.ChangeMenuFunc))]
            public static void ChangeMenuFuncPostfix(CvsA_Slot __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // OnSelectedMakerSlotChanged(__instance, __instance.SNo);
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeMenuFuncPostfix");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, __instance.SNo); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Slot), nameof(CvsA_Slot.ChangeAcsId), typeof(int))]
            public static void ChangeAcsIdPostfix(CvsA_Slot __instance, int id)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // OnAccessoryKindChanged(__instance, __instance.SNo);
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeAcsIdPostfix");
                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, __instance.SNo); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Slot), nameof(CvsA_Slot.ChangeAcsType), typeof(int))]
            public static void ChangeAcsTypePostfix(CvsA_Slot __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // OnAccessoryKindChanged(__instance, __instance.SNo);
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $ChangeAcsTypePostfix");
                                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, __instance.SNo); 
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Copy), "CopyAccessory")]
            public static void CopyAccessory(CvsA_Copy __instance)
            {
                if (PregnancyPlusPlugin.IgnoreAccessories.Value) return;

                // OnChangeAcs(__instance, __instance.selSrc, __instance.selDst);
                // if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($"+= $CopyAccessory");
                                var chaControl = Traverse.Create(__instance).Property("chaCtrl").GetValue<ChaControl>();
                var controller = GetCharaController(chaControl);
                if (controller == null) return;

                controller.AccessoryStateChangeEvent(chaControl.chaID, __instance.SNo); 
            }

        #endif

        }
    }
}
