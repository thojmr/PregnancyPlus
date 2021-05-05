using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Studio;
using System;
using KKAPI.Chara;
using BepInEx.Configuration;
#if AI || HS2
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#endif


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        //Contains all the hooks for KK/AI_Pregnancy integration
        private static class Hooks_KK_Pregnancy
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                #if KK
                    TryPatchPreggersInflation(harmonyInstance);
                #endif
            }


            #if KK

                /// <summary>
                /// Manually Hook the KK_Pregnancy AddInflation method to trigger Preg+ inflation as well
                /// </summary>
                public static void TryPatchPreggersInflation(Harmony hi)//Thanks Marco!
                {
                    var pregnancyCharaController = Type.GetType("KK_Pregnancy.PregnancyCharaController, KK_Pregnancy", false);
                    if (pregnancyCharaController == null)
                    {
                        PregnancyPlusPlugin.Logger.LogWarning(
                            "Could not find KK_Pregnancy.PregnancyCharaController, Preg+ inflation integration won't work until you install KK_Pregnancy (please report this if you do have latest version of KK_Pregnancy installed)");
                            return;
                    }

                    var AddInflationMethod = pregnancyCharaController.GetMethod("AddInflation", AccessTools.all);
                    if (AddInflationMethod == null)
                    {
                        PregnancyPlusPlugin.Logger.LogWarning(
                            "Could not find KK_Pregnancy.PregnancyCharaController.AddInflation - something isn't right, please report this");
                        return;                        
                    }
                    
                    hi.Patch(AddInflationMethod,
                            postfix: new HarmonyMethod(typeof(Hooks_KK_Pregnancy), nameof(Hooks_KK_Pregnancy.InflationChangePatch)));     



                    var DrainInflationMethod = pregnancyCharaController.GetMethod("DrainInflation", AccessTools.all);
                    if (DrainInflationMethod == null)
                    {
                        PregnancyPlusPlugin.Logger.LogWarning(
                            "Could not find KK_Pregnancy.PregnancyCharaController.DrainInflation - something isn't right, please report this");
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
                    var pregnancyPlugin = Type.GetType("KK_Pregnancy.PregnancyPlugin, KK_Pregnancy", false); 
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
                    controller.InflationChanged(inflationAmount, maxInflationSize.Value);
                }

            #endif

        }
    }
}
