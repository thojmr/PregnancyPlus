using HarmonyLib;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Studio;
using KKAPI.Studio.UI;
using KKAPI.Utilities;
using UniRx;
using UnityEngine;

namespace KK_PregnancyPlus
{
    //This partial class contains all the common properties used by the other partials
    public static partial class PregnancyPlusGui
    {
        private static PregnancyPlusPlugin _pluginInstance;

        internal static PregnancyPlusData ppDataDefaults = new PregnancyPlusData();
        
#region Don't change these, they would change users cards default scales
        #if KK
            private static int scaleLimits = 1;
        #elif HS2 || AI
            //once again everything is bigger in HS2
            private static int scaleLimits = 5;
        #endif
#endregion

        //The allowed slider ranges for each slider type
        public static class SliderRange {
            public static float[] inflationSize = {0, 40};
            public static float[] inflationMoveY = {-0.5f, 0.5f};
            public static float[] inflationMoveZ = {-0.2f, 0.2f};
            public static float[] inflationStretchX = {-0.3f, 0.3f};
            public static float[] inflationStretchY = {-0.3f, 0.3f};
            public static float[] inflationShiftY = {-0.2f, 0.2f};
            public static float[] inflationShiftZ = {-0.15f, 0.15f};
            public static float[] inflationTaperY = {-0.075f, 0.075f};
            public static float[] inflationTaperZ = {-0.075f, 0.075f};
            public static float[] inflationMultiplier = {-2f, 2f};
        }

    }
}
