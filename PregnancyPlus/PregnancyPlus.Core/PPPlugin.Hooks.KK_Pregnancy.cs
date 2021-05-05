using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Studio;
using System;
using KKAPI.Chara;
using BepInEx.Configuration;


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        //Contains all the hooks for KK/AI_Pregnancy integration
        private static class Hooks_KK_Pregnancy
        {

            //Used to identifiy the assembly class methods during reflection
            #if KK
                internal static string pluginName = "KK_Pregnancy";
            #elif AI
                internal static string pluginName = "AI_Pregnancy";
            #endif

            public static void InitHooks(Harmony harmonyInstance)
            {            
                TryPatchPreggersInflation(harmonyInstance);            
            }

            /// <summary>
            /// Manually Hook the KK_Pregnancy AddInflation method to trigger Preg+ inflation as well
            /// </summary>
            public static void TryPatchPreggersInflation(Harmony hi)//Thanks Marco!
            {                
                var pregnancyCharaController = Type.GetType($"KK_Pregnancy.PregnancyCharaController, {pluginName}", false);
                if (pregnancyCharaController == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.PregnancyCharaController, Preg+ inflation integration won't work until you install {pluginName} (please report this if you do have latest version of {pluginName} installed)");
                        return;
                }

                var AddInflationMethod = pregnancyCharaController.GetMethod("AddInflation", AccessTools.all);
                if (AddInflationMethod == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.PregnancyCharaController.AddInflation - something isn't right, please report this");
                    return;                        
                }
                
                hi.Patch(AddInflationMethod,
                        postfix: new HarmonyMethod(typeof(Hooks_KK_Pregnancy), nameof(Hooks_KK_Pregnancy.InflationChangePatch)));     



                var DrainInflationMethod = pregnancyCharaController.GetMethod("DrainInflation", AccessTools.all);
                if (DrainInflationMethod == null)
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.PregnancyCharaController.DrainInflation - something isn't right, please report this");
                    return;                        
                }     

                hi.Patch(DrainInflationMethod,
                    postfix: new HarmonyMethod(typeof(Hooks_KK_Pregnancy), nameof(Hooks_KK_Pregnancy.InflationChangePatch)));                                       
            }

            /// <summary>
            /// Harmony patch that gets the characters new InflationAmount from KK_Pregnancy
            /// </summary>
            private static void InflationChangePatch(int amount, ref CharaCustomFunctionController __instance)
            {
                //Only continue in main game mode
                if (!PregnancyPlusPlugin.StoryMode.Value || StudioAPI.InsideStudio || MakerAPI.InsideAndLoaded)
                {
                    return;
                }

                //Get the KK_Pregnancy plugin MaxInflation size value
                var pregnancyPlugin = Type.GetType($"KK_Pregnancy.PregnancyPlugin, {pluginName}", false); 
                if (pregnancyPlugin == null) return;

                //If inflation is not enabled then just return
                var inflationEnabledObj = pregnancyPlugin.GetProperty("InflationEnable").GetValue(pregnancyPlugin, null);
                if (inflationEnabledObj == null) return;                    
                var inflationEnabled = (ConfigEntry<bool>) inflationEnabledObj; 
                if (!inflationEnabled.Value) return;

                var maxInflationSizeObj = pregnancyPlugin.GetProperty("InflationMaxCount").GetValue(pregnancyPlugin, null);
                if (maxInflationSizeObj == null) return;                    
                var maxInflationSize = (ConfigEntry<int>) maxInflationSizeObj; 

                var inflationAmount = 0f;
                //Get the pregnancy InflationAmount
                var amountObj = __instance.GetType().GetProperty("InflationAmount")?.GetValue(__instance, null);          
                if (amountObj != null) inflationAmount = Convert.ToSingle(amountObj);              
                                
                var controller = GetCharaController(__instance.ChaControl);
                if (controller == null) return;

                //Set the inflation amount on the characters controller
                controller.OnInflationChanged(inflationAmount, maxInflationSize.Value);
            }

        }
    }
}
