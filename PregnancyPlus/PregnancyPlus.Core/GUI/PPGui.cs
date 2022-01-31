
namespace KK_PregnancyPlus
{
    //This partial class contains all the common properties used by the other partials
    public static partial class PregnancyPlusGui
    {
        private static PregnancyPlusPlugin _pluginInstance;

        internal readonly static PregnancyPlusData ppDataDefaults = new PregnancyPlusData();
        
        //Whether to include clothing in mesh smoothing calculations or not, to reduce clipping in some cases
        internal static bool includeClothSmoothing = false;


#region Don't change these, they would change users cards default scales

        //HS2 and AI world scales are bigger than KK, so slider min/max needs to be bigger too
        #if KKS
            private readonly static int scaleLimits = 1;
        #elif HS2 || AI            
            private readonly static int scaleLimits = 5;
        #endif


        #if KKS  //Range multiplier for the min max values allowed (This is partly to correct me setting the HS2 scale to 5x initially when it should have been 10x)
            internal readonly static float rmAltHS2x2 = 1;
            internal readonly static float rmKKx2 = 2f;//Some small adjustments for sliders that felt too large or small in range
            internal readonly static float rmKKxFract = 0.75f;
            internal readonly static float rmKKx10 = 10f;
        #elif HS2 || AI
            internal readonly static float rmAltHS2x2 = 2f;
            internal readonly static float rmKKx2 = 1;
            internal readonly static float rmKKxFract = 1f;
            internal readonly static float rmKKx10 = 1f;
        #endif


        //The allowed slider ranges for each slider type
        public static class SliderRange {

            //Computed slider values including different game world scales
            private static float[] inflationMoveY = {-0.5f * rmKKxFract * scaleLimits, 0.5f * rmKKxFract * scaleLimits};
            private static float[] inflationMoveZ = {-0.2f * rmAltHS2x2 * rmKKxFract * scaleLimits, 0.2f * rmAltHS2x2 * rmKKxFract * scaleLimits};
            private static float[] inflationStretchX = {-0.3f * rmKKx2 * scaleLimits, 0.3f * rmKKx2 * scaleLimits};
            private static float[] inflationStretchY = {-0.3f * rmKKx2 * scaleLimits, 0.3f * rmKKx2 * scaleLimits};
            private static float[] inflationShiftY = {-0.2f * rmAltHS2x2 * scaleLimits, 0.2f * rmAltHS2x2 * scaleLimits};
            private static float[] inflationShiftZ = {-0.15f * rmAltHS2x2 * scaleLimits, 0.15f * rmAltHS2x2 * scaleLimits};
            private static float[] inflationTaperY = {-0.075f * rmAltHS2x2 * scaleLimits, 0.075f * rmAltHS2x2 * scaleLimits};
            private static float[] inflationTaperZ = {-0.075f * rmAltHS2x2 * scaleLimits, 0.075f * rmAltHS2x2 * scaleLimits};
            private static float[] inflationClothOffset = {-2 * rmKKx10 * scaleLimits, 2 * rmKKx10 * scaleLimits};
            private static float[] inflationRoundness = {-0.75f * scaleLimits, 0.75f * scaleLimits};

            
            //No scales needed here, why you ask? because I learned from my mistakes above and made the sliders independent of world scale
            private static float[] inflationSize = {0, 40};
            private static float[] inflationMultiplier = {-2f, 2f}; 
            private static float[] inflationDrop = { 0, 1f };  
            private static float[] inflationFatFold = {0, 2f};
            private static float[] inflationFatFoldHeight = {-1f, 1f};
            private static float[] inflationFatFoldGap = {-1f, 1f};



            public static float[] InflationSize {
                get { return inflationSize; }
            }
            public static float[] InflationMultiplier {
                get { return inflationMultiplier; }
            }          
            public static float[] InflationMoveY {
                get { return inflationMoveY; }
            }  
            public static float[] InflationMoveZ {
                get { return inflationMoveZ; }
            }  
            public static float[] InflationStretchX {
                get { return inflationStretchX; }
            }  
            public static float[] InflationStretchY {
                get { return inflationStretchY; }
            }  
            public static float[] InflationShiftY {
                get { return inflationShiftY; }
            }  
            public static float[] InflationShiftZ {
                get { return inflationShiftZ; }
            }  
            public static float[] InflationTaperY {
                get { return inflationTaperY; }
            }  
            public static float[] InflationTaperZ {
                get { return inflationTaperZ; }
            }              
            public static float[] InflationClothOffset {
                get { return inflationClothOffset; }
            }  
            public static float[] InflationRoundness {
                get { return inflationRoundness; }
            }  
            public static float[] InflationDrop {
                get { return inflationDrop; }
            }  
            public static float[] InflationFatFold {
                get { return inflationFatFold; }
            }  
            public static float[] InflationFatFoldHeight {
                get { return inflationFatFoldHeight; }
            }                                                                                  
            public static float[] InflationFatFoldGap {
                get { return inflationFatFoldGap; }
            }                                                                                  
            
        }

#endregion Don't change these, they would change users cards default scales

    }
}
