using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    //These are the preset belly shape templates that are used to set a specific belly shape from a dropdown
    public static class BellyTemplate 
    {
        public static string[] shapeNames = {"None", "Small", "Standard", "Standard Flat", "Multiples", "Torpedo", "Hyper", "Chub", "Fat"};

        
        /// <summary>
        /// Get the preset by shapeName index
        /// </summary>
        public static PregnancyPlusData GetTemplate(int i) 
        {
            #if KKS
                return BuildKKShape(shapeNames[i]);
            #else
                return BuildHS2Shape(shapeNames[i]);
            #endif
        }

        
        /// <summary>
        /// Overload for GetTemplate() with string as argument
        /// </summary>
        public static PregnancyPlusData GetTemplate(string templateName) 
        {
            #if KKS
                return BuildKKShape(templateName);
            #else
                return BuildHS2Shape(templateName);
            #endif
        }


        /// <summary>
        /// Return the slider values for the selected belly shape
        /// </summary>
        private static PregnancyPlusData BuildKKShape(string shapeName)
        {
            var shape = new PregnancyPlusData();
            shape.pluginVersion = PregnancyPlusPlugin.Version;
            shape.inflationSize = 40f;

            //Note: These values looked decent on most default characters, but depend heavily on belly and body bone scales.
            switch (shapeName)
            {                
                case "None":                                      
                    return shape;

                case "Small":                                      
                    shape.inflationMultiplier = -0.1f;
                    shape.inflationRoundness = 0.005f;
                    shape.inflationStretchX = -0.1f;
                    shape.inflationStretchY = -0.1f;
                    shape.inflationDrop = 0.02f;
                    return shape;

                case "Standard":
                    shape.inflationMultiplier = 0.3f;
                    shape.inflationRoundness = 0.005f;                    
                    shape.inflationStretchX = -0.14f;
                    shape.inflationStretchY = -0.12f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.05f;
                    return shape;

                //Flat is meant for characters with belly bones scales resulting in very pointy bellies
                case "Standard Flat":
                    shape.inflationMultiplier = 0.2f;
                    shape.inflationRoundness = 0.007f;                    
                    shape.inflationStretchX = -0.02f;
                    shape.inflationStretchY = -0.02f;
                    shape.inflationShiftZ = -0.005f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.05f;
                    return shape;
                
                case "Torpedo":
                    shape.inflationMultiplier = 0.4f;
                    shape.inflationRoundness = 0.05f;                    
                    shape.inflationStretchX = -0.35f;
                    shape.inflationStretchY = -0.35f;
                    shape.inflationMoveZ = 0.01f;
                    shape.inflationShiftZ = 0.02f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.07f;
                    return shape;

                case "Multiples":
                    shape.inflationMultiplier = 0.48f;
                    shape.inflationRoundness = 0.1f;
                    shape.inflationStretchX = -0.22f;
                    shape.inflationStretchY = -0.22f;
                    shape.inflationShiftZ = 0.007f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.002f;
                    shape.inflationDrop = 0.07f;
                    return shape;

                case "Hyper":
                    shape.inflationMultiplier = 0.7f;
                    shape.inflationRoundness = 0.14f;
                    shape.inflationStretchX = -0.25f;
                    shape.inflationStretchY = -0.25f;
                    shape.inflationShiftZ = 0.015f;
                    shape.inflationTaperZ = -0.002f;
                    shape.inflationDrop = 0.15f;
                    return shape;
                
                case "Chub":
                    shape.inflationMultiplier = 0f;
                    shape.inflationRoundness = 0.008f;
                    shape.inflationStretchX = -0.1f; 
                    shape.inflationFatFold = 0.9f;
                    shape.inflationFatFoldGap = -0.02f;
                    return shape;

                case "Fat":
                    shape.inflationMultiplier = 0.1f;
                    shape.inflationRoundness = 0.018f;
                    shape.inflationStretchX = -0.1f; 
                    shape.inflationTaperZ = 0.007f;
                    shape.inflationFatFold = 1f;
                    shape.inflationFatFoldGap = -0.04f;
                    return shape;
                    
                default:
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning($" BuildKKShape() Shape {shapeName} not found, oops ");
                    return new PregnancyPlusData();
            }
        }


        /// <summary>
        /// Return the slider values for the selected belly shape
        /// </summary>
        private static PregnancyPlusData BuildHS2Shape(string shapeName)
        {
            var shape = new PregnancyPlusData();
            shape.pluginVersion = PregnancyPlusPlugin.Version;
            shape.inflationSize = 40f;

            switch (shapeName)
            {
                case "None":                                      
                    return shape;

                case "Small":                                      
                    shape.inflationMultiplier = -0.05f;
                    shape.inflationRoundness = 0.005f;
                    shape.inflationStretchX = -0.1f;
                    shape.inflationDrop = 0.02f;
                    return shape;

                case "Standard":
                    shape.inflationMultiplier = 0.3f;
                    shape.inflationRoundness = 0.005f;                    
                    shape.inflationStretchX = -0.18f;
                    shape.inflationStretchY = -0.15f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.05f;
                    return shape;

                //Flat is meant for characters with belly bones scales resulting in very pointy bellies
                case "Standard Flat":
                    shape.inflationMultiplier = 0.2f;
                    shape.inflationRoundness = 0.007f;                    
                    shape.inflationStretchX = -0.1f;
                    shape.inflationStretchY = -0.1f;
                    shape.inflationShiftZ = -0.2f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.05f;
                    return shape;
                
                case "Torpedo":
                    shape.inflationMultiplier = 0.4f;
                    shape.inflationRoundness = 0.3f;                    
                    shape.inflationStretchX = -0.35f;
                    shape.inflationStretchY = -0.35f;
                    shape.inflationMoveZ = 0.1f;
                    shape.inflationShiftZ = 0.2f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.07f;
                    return shape;

                case "Multiples":
                    shape.inflationMultiplier = 0.6f;
                    shape.inflationRoundness = 0.15f;
                    shape.inflationStretchX = -0.25f;
                    shape.inflationStretchY = -0.25f;
                    shape.inflationShiftZ = 0.07f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.002f;
                    shape.inflationDrop = 0.1f;
                    return shape;

                case "Hyper":
                    shape.inflationMultiplier = 0.8f;
                    shape.inflationRoundness = 1f;
                    shape.inflationStretchX = -0.3f;
                    shape.inflationStretchY = -0.3f;
                    shape.inflationShiftZ = 0.15f;
                    shape.inflationTaperZ = -0.002f;
                    shape.inflationDrop = 0.15f;
                    return shape;
                
                case "Chub":
                    shape.inflationMultiplier = 0f;
                    shape.inflationRoundness = 0.008f;
                    shape.inflationStretchX = -0.1f; 
                    shape.inflationFatFold = 0.9f;
                    shape.inflationFatFoldGap = -0.02f;
                    return shape;

                case "Fat":
                    shape.inflationMultiplier = 0.1f;
                    shape.inflationRoundness = 0.018f;
                    shape.inflationStretchX = -0.1f; 
                    shape.inflationTaperZ = 0.007f;
                    shape.inflationFatFold = 1f;
                    shape.inflationFatFoldGap = -0.04f;
                    return shape;
                    
                default:
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning($" BuildHS2Shape() Shape {shapeName} not found, oops ");
                    return new PregnancyPlusData();
            }
        }
    }
}