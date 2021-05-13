using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using KKAPI.Chara;
#if HS2 || AI
    using AIChara;
#endif

namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        //Contains all the hooks for detecting uncensor changes
        public static class Hooks_Uncensor
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




            /// <summary>   
            /// Check whether the currently loaded body mesh is an uncensor mesh
            /// If the current characters mesh is set by the Uncensor plugin we need to know this to correctly shift the mesh's localspace vertex positions
            /// </summary>  
            public static bool IsUncensorBody(ChaControl chaControl, string UncensorCOMName) 
            {
                //grab the active uncensor controller of it exists
                var bodyGUID = GetUncensorBodyGuid(chaControl, UncensorCOMName);
                if (bodyGUID == null) return false;

                return bodyGUID != PregnancyPlusCharaController.DefaultBodyFemaleGUID && bodyGUID != PregnancyPlusCharaController.DefaultBodyMaleGUID;
            }


            /// <summary>   
            /// Gets the active uncensor body GUID
            /// </summary>  
            public static string GetUncensorBodyGuid(ChaControl chaControl, string UncensorCOMName) 
            {
                //grab the active uncensor controller of it exists
                var uncensorController = PregnancyPlusHelper.GetCharacterBehaviorController<CharaCustomFunctionController>(chaControl, UncensorCOMName);
                if (uncensorController == null) return null;

                //Get the body type name, and see if it is the default mesh name
                var bodyData = uncensorController.GetType().GetProperty("BodyData")?.GetValue(uncensorController, null);
                if (bodyData == null)
                { 
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.UncensorSelector.UncensorSelectorController.BodyData - something isn't right, please report this");
                    return null;
                }

                var bodyGUID = Traverse.Create(bodyData).Field("BodyGUID")?.GetValue<string>();
                if (bodyGUID == null) 
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find {pluginName}.UncensorSelector.UncensorSelectorController.BodyData.BodyGUID - something isn't right, please report this");
                     return null;
                }

                return bodyGUID;
            }


            // /// <summary>   
            // /// TODO I don't think there is an easy way to get vertex count of meshes we havent loaded yet
            // /// Get key value list of uncensor GUID's and uncensor mesh Vert counts
            // /// </summary>  
            // public static Dictionary<string, string> GetUncensorBodyDict() 
            // {
            //     var uncensorSelector = Type.GetType($"KK_Plugins.UncensorSelector, {pluginName}", false);
            //     if (uncensorSelector == null)
            //     {
            //         PregnancyPlusPlugin.Logger.LogInfo(
            //             $"Could not find {pluginName}.UncensorSelector - Not an issue");
            //             return new Dictionary<string, string>();
            //     }

            //     var BodyDictionary = uncensorSelector.GetField("BodyDictionary");
            //     if (BodyDictionary == null)
            //     {
            //         PregnancyPlusPlugin.Logger.LogWarning(
            //             $"Could not find {pluginName}.UncensorSelector.BodyDictionary - something isn't right, please report this");
            //         return new Dictionary<string, string>();                      
            //     }

            //     // var fullBodyList = (Dictionary<string, string>) BodyConfigListFull;

            //     var bodyDict = new Dictionary<string, string>();
            //     foreach(var key in BodyDictionary.Keys)
            //     {
            //         bodyDict[key] = BodyDictionary[key].VertexCont;
            //     }
            //     // var bodyList = fullBodyList.Values;
            //     // return bodyList;
            //     return (string[]) bodyDict;
            // }


            /// <summary>   
            /// Change to a specific uncensor
            /// </summary>  
            public static bool ChangeUncensorTo(ChaControl chaControl, string UncensorCOMName, string bodyGUID) 
            {
                //grab the active uncensor controller of it exists
                var uncensorController = PregnancyPlusHelper.GetCharacterBehaviorController<CharaCustomFunctionController>(chaControl, UncensorCOMName);
                if (uncensorController == null) return false;            

                //Set the body GUID value
                var bodyList = Traverse.Create(uncensorController).Property("BodyGUID").SetValue(bodyGUID);
                if (bodyList == null) 
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not set {pluginName}.UncensorSelector.UncensorSelectorController.BodyGUID - something isn't right, please report this");
                    return false;
                }

                //Get UncensorChange method, to trigger
                var updateUncensor = Traverse.Create(uncensorController).Method("UpdateUncensor");
                if (updateUncensor == null) 
                {
                    PregnancyPlusPlugin.Logger.LogWarning(
                        $"Could not find method {pluginName}.UncensorSelector.UncensorSelectorController.UpdateUncensor - something isn't right, please report this");
                    return false;
                }

                //Trigger uncensor change
                updateUncensor.GetValue(new object[0]);

                return true;
            }

        }
    }
}
