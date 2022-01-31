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

        //Hooks for Character Clothing changes (Add, Remove)
        private static class HooksClothing
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                harmonyInstance.PatchAll(typeof(HooksClothing));
            }


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


            /// <summary>
            /// Ignore gloves, socks, and shoes since they dont affect the belly area
            /// </summary>
            internal static bool IsIgnoredClothing(int clothesKind) 
            {
                #if KKS
                    return (clothesKind == (int)ChaFileDefine.ClothesKind.gloves || clothesKind == (int)ChaFileDefine.ClothesKind.socks || clothesKind == (int)ChaFileDefine.ClothesKind.shoes_inner || clothesKind == (int)ChaFileDefine.ClothesKind.shoes_outer);
                #elif HS2 || AI
                    return (clothesKind == (int)ChaFileDefine.ClothesKind.gloves || clothesKind == (int)ChaFileDefine.ClothesKind.socks || clothesKind == (int)ChaFileDefine.ClothesKind.shoes);
                #endif
            }


        }
    }
}
