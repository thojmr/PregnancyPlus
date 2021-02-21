namespace KK_PregnancyPlus
{
    //This partial class contains all the common properties used by the other partials
    public static partial class PregnancyPlusGui
    {
        private static PregnancyPlusPlugin _pluginInstance;

        internal readonly static PregnancyPlusData ppDataDefaults = new PregnancyPlusData();
        
#region Don't change these, they would change users cards default scales

        #if KK
            private readonly static int scaleLimits = 1;
        #elif HS2 || AI
            //once again everything is bigger in HS2
            private readonly static int scaleLimits = 5;
        #endif

        #if KK  //Range multiplier for the min max values allowed
            private readonly static float rmAltHS2x2 = 1;
            private readonly static float rmKKx2 = 2f;//Some small adjustments for sliders that felt too large or small in range
            private readonly static float rmKKxFract = 0.75f;
            private readonly static float rmKKx4 = 5f;
        #elif HS2 || AI
            private readonly static float rmAltHS2x2 = 2f;
            private readonly static float rmKKx2 = 1;
            private readonly static float rmKKxFract = 1f;
            private readonly static float rmKKx4 = 1f;
        #endif

        //The allowed slider ranges for each slider type
        public static class SliderRange {
            public readonly static float[] inflationSize = {0, 40};
            public readonly static float[] inflationMultiplier = {-2f, 2f};            
            public readonly static float[] inflationMoveY = {-0.5f * rmKKxFract, 0.5f * rmKKxFract};
            public readonly static float[] inflationMoveZ = {-0.2f * rmAltHS2x2 * rmKKxFract, 0.2f * rmAltHS2x2 * rmKKxFract};
            public readonly static float[] inflationStretchX = {-0.3f * rmKKx2, 0.3f * rmKKx2};
            public readonly static float[] inflationStretchY = {-0.3f * rmKKx2, 0.3f * rmKKx2};
            public readonly static float[] inflationShiftY = {-0.2f * rmAltHS2x2, 0.2f * rmAltHS2x2};
            public readonly static float[] inflationShiftZ = {-0.15f * rmAltHS2x2, 0.15f * rmAltHS2x2};
            public readonly static float[] inflationTaperY = {-0.075f * rmAltHS2x2, 0.075f * rmAltHS2x2};
            public readonly static float[] inflationTaperZ = {-0.075f * rmAltHS2x2, 0.075f * rmAltHS2x2};            
            public readonly static float[] inflationClothOffset = {-2 * rmKKx4, 2 * rmKKx4};
            public readonly static float[] inflationFatFold = {0, 2f};
            public readonly static float[] inflationRoundness = {-0.75f, 0.75f};
        }

#endregion Don't change these, they would change users cards default scales

    }
}
