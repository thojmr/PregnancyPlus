using HarmonyLib;
using System;
using KKAPI.Chara;


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        //Contains all the hooks for detecting uncensor changes
        private static class Hooks_Uncensor
        {

            //Used to identifiy the assembly class methods during reflection
            #if KK
                internal static string pluginName = "KK_UncensorSelector";
            #elif AI
                internal static string pluginName = "AI_UncensorSelector";
            #elif HS2
                internal static string pluginName = "HS2_UncensorSelector";
            #endif


            public static void InitHooks(Harmony harmonyInstance)
            {            
                TryPatchUncensorChange(harmonyInstance);            
            }


            /// <summary>
            /// Manually Hook the Unensor Change method to trigger Preg+ updates
            /// </summary>
            public static void TryPatchUncensorChange(Harmony hi)
            {                
                var uncensorSelector = Type.GetType($"KK_Plugins.UncensorSelector, {pluginName}", false);
                if (uncensorSelector == null)
                {
                    PregnancyPlusPlugin.Logger.LogInfo(
                        $"Could not find {pluginName}.UncensorSelector - Not an issue");
                        return;
                }

                var uncensorCharaController = uncensorSelector.GetNestedType("UncensorSelectorController");
                if (uncensorCharaController == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.UncensorSelector.UncensorSelectorController - something isn't right, please report this");
                        return;
                }

                var ReloadCharacterBodyMethod = uncensorCharaController.GetMethod("ReloadCharacterBody", AccessTools.all);
                if (ReloadCharacterBodyMethod == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.UncensorSelector.UncensorSelectorController.ReloadCharacterBody - something isn't right, please report this");
                    return;                        
                }
                
                //If the reload char body method is found, then patch it
                hi.Patch(ReloadCharacterBodyMethod,
                        postfix: new HarmonyMethod(typeof(Hooks_Uncensor), nameof(Hooks_Uncensor.ReloadCharacterBodyPatch)));     
                                       
            }


            /// <summary>
            /// Harmony patch that fires after an uncensor mesh is changed
            /// </summary>
            private static void ReloadCharacterBodyPatch(ref CharaCustomFunctionController __instance)
            {              
                var controller = GetCharaController(__instance.ChaControl);
                if (controller == null) return;

                //Let the character controller know the uncensor mesh changed
                controller.OnUncensorChanged();
            }

        }
    }
}
