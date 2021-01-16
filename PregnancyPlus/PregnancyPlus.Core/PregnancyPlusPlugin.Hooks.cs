using HarmonyLib;
using KKAPI.Maker;
#if AI || HS2
    using AIChara;
#elif KK
    using KKAPI.MainGame;
#endif


namespace KK_PregnancyPlus
{
    public partial class PregnancyPlusPlugin
    {
        private static class Hooks
        {
            public static void InitHooks(Harmony harmonyInstance)
            {
                harmonyInstance.PatchAll(typeof(Hooks));
            }

            /// <summary>
            /// Trigger the ClothesStateChangeEvent for toggling on and off a clothing item
            /// </summary>
            [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetClothesState))]
            private static void SetClothesStatePostfix(ChaControl __instance, int clothesKind)
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
            private static void ChangeCustomClothes(ChaControl __instance, int kind)
            {

                //Ignore gloves, shoes, socks
                if (IsIgnoredClothing(kind)) return;

                if (MakerAPI.InsideAndLoaded)
                {
                    var controller = GetCharaController(__instance);
                    if (controller == null) return;
                
                    //Send event to the CustomCharaFunctionController that the clothes were changed on
                    controller.ClothesStateChangeEvent(__instance.chaID, kind, true);  
                }
            }




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
