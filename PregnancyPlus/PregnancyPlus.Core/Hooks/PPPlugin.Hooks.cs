using HarmonyLib;
using KKAPI.Maker;
using KKAPI.Studio;
#if AI || HS2
    using AIChara;
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


            #if KKS
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


        }
    }
}
