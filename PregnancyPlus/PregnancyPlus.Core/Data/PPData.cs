using System;
using System.Reflection;
using System.Collections.Generic;
using ExtensibleSaveFormat;

namespace KK_PregnancyPlus
{
    public sealed class PregnancyPlusData : ICloneable
    {

#region Names of these are important, used as dictionary keys

        public float inflationSize = 0;
        public float inflationMoveY = 0;
        public float inflationMoveZ = 0;
        public float inflationStretchX = 0;
        public float inflationStretchY = 0;
        public float inflationShiftY = 0;
        public float inflationShiftZ = 0;
        public float inflationTaperY = 0;
        public float inflationTaperZ = 0;
        public float inflationMultiplier = 0;
        public float inflationClothOffset = 0;
        public float inflationFatFold = 0;
        public float inflationFatFoldHeight = 0;
        public bool GameplayEnabled = true;
        public float inflationRoundness = 0;
        public float inflationDrop = 0;
        public int clothingOffsetVersion = 1;//Tracks which clothing offset vserion this character was made with  v1 == 0, v2 == 1
        public byte[] meshBlendShape = null;//Type: List<MeshBlendShape> once Deserialized
        public string pluginVersion = null;

#endregion


        //Allows cloning, to avoid pass by ref issues when keeping history
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string ValuesToString() 
        {
            return $"v{GetPluginVersion()} inflationSize {inflationSize} GameplayEnabled {GameplayEnabled} clothingOffsetVersion {clothingOffsetVersion} BS {HasBlendShape()}";
        }

        //Allow comparison between all public properties of two PregnancyPlusData objects (excluding clothingOffsetVersion)
        public override bool Equals(Object obj)
        {
            var otherData = obj as PregnancyPlusData;
            if (otherData == null) return false;
            var hasChanges = false;

            //Compare this class instance values to another
            if (inflationSize != otherData.inflationSize) hasChanges = true;              
            if (inflationMoveY != otherData.inflationMoveY) hasChanges = true;
            if (inflationMoveZ != otherData.inflationMoveZ) hasChanges = true;
            if (inflationStretchX != otherData.inflationStretchX) hasChanges = true;
            if (inflationStretchY != otherData.inflationStretchY) hasChanges = true;
            if (inflationShiftY != otherData.inflationShiftY) hasChanges = true;
            if (inflationShiftZ != otherData.inflationShiftZ) hasChanges = true;
            if (inflationTaperY != otherData.inflationTaperY) hasChanges = true;
            if (inflationTaperZ != otherData.inflationTaperZ) hasChanges = true;
            if (inflationMultiplier != otherData.inflationMultiplier) hasChanges = true;
            if (inflationClothOffset != otherData.inflationClothOffset) hasChanges = true;
            if (inflationFatFold != otherData.inflationFatFold) hasChanges = true;           
            if (inflationFatFoldHeight != otherData.inflationFatFoldHeight) hasChanges = true;           
            if (inflationRoundness != otherData.inflationRoundness) hasChanges = true;                      
            if (inflationDrop != otherData.inflationDrop) hasChanges = true;                      

            return hasChanges;
        }


        //Allow comparison between all public properties of two PregnancyPlusData objects (excluding clothingOffsetVersion)
        public bool InflationSizeOnlyChange(Object obj)
        {
            var otherData = obj as PregnancyPlusData;
            if (otherData == null) return false;
            var inflationSizeOnlyChanges = false;

            //Compare this class instance values to another
            if (inflationSize != otherData.inflationSize) inflationSizeOnlyChanges = true;              
            if (inflationMoveY != otherData.inflationMoveY) inflationSizeOnlyChanges = false;
            if (inflationMoveZ != otherData.inflationMoveZ) inflationSizeOnlyChanges = false;
            if (inflationStretchX != otherData.inflationStretchX) inflationSizeOnlyChanges = false;
            if (inflationStretchY != otherData.inflationStretchY) inflationSizeOnlyChanges = false;
            if (inflationShiftY != otherData.inflationShiftY) inflationSizeOnlyChanges = false;
            if (inflationShiftZ != otherData.inflationShiftZ) inflationSizeOnlyChanges = false;
            if (inflationTaperY != otherData.inflationTaperY) inflationSizeOnlyChanges = false;
            if (inflationTaperZ != otherData.inflationTaperZ) inflationSizeOnlyChanges = false;
            if (inflationMultiplier != otherData.inflationMultiplier) inflationSizeOnlyChanges = false;
            if (inflationClothOffset != otherData.inflationClothOffset) inflationSizeOnlyChanges = false;
            if (inflationFatFold != otherData.inflationFatFold) inflationSizeOnlyChanges = false;           
            if (inflationFatFoldHeight != otherData.inflationFatFoldHeight) inflationSizeOnlyChanges = false;           
            if (inflationRoundness != otherData.inflationRoundness) inflationSizeOnlyChanges = false;                      
            if (inflationDrop != otherData.inflationDrop) inflationSizeOnlyChanges = false;                      

            return inflationSizeOnlyChanges;
        }

        public override int GetHashCode()
        {
            var hashCode = inflationSize.GetHashCode() +
                inflationMoveY.GetHashCode() +
                inflationMoveZ.GetHashCode() +
                inflationStretchX.GetHashCode() +
                inflationStretchY.GetHashCode() +
                inflationShiftY.GetHashCode() +
                inflationShiftZ.GetHashCode() +
                inflationTaperY.GetHashCode() +
                inflationTaperZ.GetHashCode() +
                inflationMultiplier.GetHashCode() +
                inflationClothOffset.GetHashCode() +
                inflationRoundness.GetHashCode() +
                inflationDrop.GetHashCode() +
                clothingOffsetVersion.GetHashCode() +
                inflationFatFold.GetHashCode() +
                inflationFatFoldHeight.GetHashCode();

            return hashCode;            
        }

        /// <summary>   
        /// The plugin version this card was saved with
        /// </summary>
        public string GetPluginVersion() 
        {
            //Anything before v3.6 will be null
            return pluginVersion != null ? pluginVersion : "0";
        }

#region Save/Load (Thanks for the code Marco)
        
        /// <summary>
        /// Will compare current values to default values
        /// </summary>
        /// <param name="includeSize">When false, will ignore inflationSize comparison</param>
        public bool HasAnyValue(bool includeSize = true) 
        {
            foreach (var fieldInfo in _serializedFields)
            {
                //When false, we want to ignore changes in inflationSize
                if (!includeSize && fieldInfo.Name == "inflationSize") continue;
                //Skip the below fields always
                if (fieldInfo.Name == "pluginVersion") continue;

                var value = fieldInfo.GetValue(this);
                var defaultValue = fieldInfo.GetValue(_default);
                if (!Equals(defaultValue, value)) 
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasBlendShape()
        {
            return meshBlendShape != null && meshBlendShape.Length > 0;
        }

        private static readonly PregnancyPlusData _default = new PregnancyPlusData();
        private static readonly FieldInfo[] _serializedFields = typeof(PregnancyPlusData).GetFields(BindingFlags.Public | BindingFlags.Instance);

        public static PregnancyPlusData Load(PluginData data)
        {
            if (data?.data == null) return null;
            var hasClothingVersion = false;

            var result = new PregnancyPlusData();
            foreach (var fieldInfo in _serializedFields)
            {
                if (data.data.TryGetValue(fieldInfo.Name, out var val))
                {
                    try
                    {
                        if (fieldInfo.Name == "clothingOffsetVersion" && (int)val > -1)
                        {
                            hasClothingVersion = true;
                        }

                        if (fieldInfo.FieldType.IsEnum) val = (int)val;
                        fieldInfo.SetValue(result, val);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            //Set clothing offset version to V1 == 0 when slider values exists, but no version was found
            if (!hasClothingVersion && result.HasAnyValue())
            {
                result.clothingOffsetVersion = 0;
            }

            return result;
        }


        /// <summary>
        /// Get PluginData to save to character card
        /// </summary>
        /// <param name="hotSwap">When we just want to update a single prop and re-save</param>
        public PluginData Save(bool hotSwap = false)
        {
            var result = new PluginData { version = 1 };
            var anyValuesChanged = false;
            
            foreach (var fieldInfo in _serializedFields)
            {
                var value = fieldInfo.GetValue(this);
                // Check if any value is different than default, if not then don't save any data
                var defaultValue = fieldInfo.GetValue(_default);

                if (!Equals(defaultValue, value)) 
                {
                    result.data.Add(fieldInfo.Name, value);
                    anyValuesChanged = true;
                }
            }

            //When we don't want to change any of the below values (just replace a single val or two)
            if (hotSwap) return result.data.Count > 0 ? result : null;

            //always save clolthing offset version if any values above set
            if (anyValuesChanged && !result.data.ContainsKey("clothingOffsetVersion")) 
            {
                result.data.Add("clothingOffsetVersion", clothingOffsetVersion);                             
            }

            //Update plugin version on saving card, helps with debugging
            if (!result.data.ContainsKey("pluginVersion")) 
            {                
                result.data.Add("pluginVersion", PregnancyPlusPlugin.Version);
            }
            else 
            {
                result.data["pluginVersion"] = PregnancyPlusPlugin.Version;
            }

            return result.data.Count > 0 ? result : null;
        }

#endregion

    }
}