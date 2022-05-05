using System.Collections.Generic;

namespace KK_PregnancyPlus
{
    //These are the preset belly shape templates that are used to set a specific belly shape from a dropdown
    public static class BellyTemplate 
    {
        public static string[] shapeNames = {"None", "Small", "Standard", "Standard Flat", "Multiples", "Torpedo", "Hyper", "Chub Small", "Chub Standard", "Fat"};

        
        /// <summary>
        /// Get the preset by shapeName index
        /// </summary>
        public static PregnancyPlusData GetTemplate(int i) 
        {
            return BuildShape(shapeNames[i]);
        }

        
        /// <summary>
        /// Overload for GetTemplate() with string as argument
        /// </summary>
        public static PregnancyPlusData GetTemplate(string templateName) 
        {
            return BuildShape(templateName);
        }


        /// <summary>
        /// Return the slider values for the selected belly shape
        /// </summary>
        private static PregnancyPlusData BuildShape(string shapeName)
        {
            var shape = new PregnancyPlusData();
            shape.pluginVersion = PregnancyPlusPlugin.Version;
            //All presets below represent the MAX desired size.  So set the inflationSize to 40 for all of them.
            shape.inflationSize = 40f;

            //Note: These values looked decent on most normal scale characters, but depend heavily on belly and body bone scales.
            switch (shapeName)
            {                
                //"None" does not trigger changes in studio and maker on purpose
                case "None":                                      
                    return shape;

                case "Small":     
                    #if KKS                                 
                        shape.inflationMultiplier = -0.1f;
                        shape.inflationStretchY = -0.05f;
                    #elif HS2 || AI
                        shape.inflationMultiplier = -0.05f;
                    #endif
                    shape.inflationRoundness = 0.005f;
                    shape.inflationStretchX = -0.05f;
                    shape.inflationDrop = 0.02f;
                    return shape;

                case "Standard":
                    #if KKS 
                        shape.inflationMultiplier = 0.25f;                                            
                        shape.inflationStretchX = -0.12f;
                        shape.inflationStretchY = -0.07f;
                        shape.inflationTaperY = -0.005f;
                        shape.inflationTaperZ = -0.003f;                        
                    #elif HS2 || AI
                        shape.inflationMultiplier = 0.3f;
                        shape.inflationStretchX = -0.18f;
                        shape.inflationStretchY = -0.13f;
                        shape.inflationTaperY = -0.01f;
                        shape.inflationTaperZ = -0.005f;
                    #endif
                    shape.inflationRoundness = 0.005f;
                    shape.inflationDrop = 0.05f;
                    return shape;

                //Flat is meant for characters with very pointy bellies
                case "Standard Flat":
                    #if KKS                                                 
                        shape.inflationStretchX = -0.02f;
                        shape.inflationStretchY = -0.018f;
                        shape.inflationShiftZ = -0.005f;                        
                    #elif HS2 || AI
                        shape.inflationStretchX = -0.1f;
                        shape.inflationStretchY = -0.1f;
                        shape.inflationShiftZ = -0.2f;
                    #endif
                    shape.inflationMultiplier = 0.2f;
                    shape.inflationRoundness = 0.007f;    
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;      
                    shape.inflationDrop = 0.05f;          
                    return shape;
                
                case "Torpedo":
                    #if KKS                         
                        shape.inflationRoundness = 0.05f;                    
                        shape.inflationShiftZ = 0.02f;        
                        shape.inflationMoveZ = 0.01f;            
                    #elif HS2 || AI
                        shape.inflationRoundness = 0.3f;                                    
                        shape.inflationShiftZ = 0.2f;
                        shape.inflationMoveZ = 0.1f;                        
                    #endif
                    shape.inflationMultiplier = 0.4f;
                    shape.inflationStretchX = -0.35f;
                    shape.inflationStretchY = -0.35f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.005f;
                    shape.inflationDrop = 0.07f;
                    return shape;

                case "Multiples":
                    #if KKS 
                        shape.inflationMultiplier = 0.48f;
                        shape.inflationRoundness = 0.1f;
                        shape.inflationStretchX = -0.22f;
                        shape.inflationStretchY = -0.2f;
                        shape.inflationDrop = 0.07f;
                    #elif HS2 || AI
                        shape.inflationMultiplier = 0.6f;
                        shape.inflationRoundness = 0.15f;
                        shape.inflationStretchX = -0.25f;
                        shape.inflationStretchY = -0.25f;
                        shape.inflationDrop = 0.1f;
                    #endif
                    shape.inflationShiftZ = 0.007f;
                    shape.inflationTaperY = -0.01f;
                    shape.inflationTaperZ = -0.002f;
                    return shape;

                case "Hyper":
                    #if KKS 
                        shape.inflationMultiplier = 0.7f;
                        shape.inflationRoundness = 0.14f;
                        shape.inflationStretchX = -0.25f;
                        shape.inflationStretchY = -0.25f;
                    #elif HS2 || AI                   
                        shape.inflationMultiplier = 0.8f;
                        shape.inflationRoundness = 1f;
                        shape.inflationStretchX = -0.3f;
                        shape.inflationStretchY = -0.3f;
                    #endif
                    shape.inflationShiftZ = 0.015f;
                    shape.inflationTaperZ = -0.002f;
                    shape.inflationDrop = 0.15f;
                    return shape;
                
                case "Chub Small":
                    shape.inflationMultiplier = -0.17f;
                    shape.inflationTaperZ = -0.004f;
                    shape.inflationFatFold = 0.9f;
                    return shape;

                case "Chub Standard":
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
                    shape.inflationFatFoldGap = -0.03f;
                    return shape;
                    
                default:
                    if (PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogWarning($" BuildKKShape() Shape {shapeName} not found, oops ");
                    return new PregnancyPlusData();
            }
        }

    }
}