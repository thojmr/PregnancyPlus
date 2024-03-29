using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
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
        public float inflationFatFoldGap = 0;
        public bool GameplayEnabled = true;
        public float inflationRoundness = 0;
        public float inflationDrop = 0;
        //Tracks which clothing offset vserion this character was made with  v1 == 0, v2 == 1
        public int clothingOffsetVersion = 1;//We no longer use this, but keeping for legacy reasons (Everyone is V2 now)
        //Each clothing item can have a separate offset now
        public byte[] individualClothingOffsets = null;
        public byte[] meshBlendShape = null;//Type: List<MeshBlendShape> once Deserialized
        public string pluginVersion = null;
        

        //Convert the value to byte[] since KeyValuePair, and other IDictionary objects can't be serialized with LZ4MessagePackSerializer
        public List<KeyValuePair<string, float>> IndividualClothingOffsets 
        {
            get 
            { 
                if (individualClothingOffsets == null) return null;
                return MessagePack.LZ4MessagePackSerializer.Deserialize<List<KeyValuePair<string, float>>>(individualClothingOffsets);
            }
            set 
            {
                if (value == null) 
                {
                    individualClothingOffsets = null;
                    return;
                }
                individualClothingOffsets = MessagePack.LZ4MessagePackSerializer.Serialize(value);
            }
        }

#endregion
        
        /// <summary>   
        /// Reset slider values back to default, and sets GamePlayEnabled flag for this character (in case it was disabled from Maker)
        /// </summary>
        public void Reset()
        {
            inflationSize = 0;
            inflationMoveY = 0;
            inflationMoveZ = 0;
            inflationStretchX = 0;
            inflationStretchY = 0;
            inflationShiftY = 0;
            inflationShiftZ = 0;
            inflationTaperY = 0;
            inflationTaperZ = 0;
            inflationMultiplier = 0;
            inflationClothOffset = 0;
            inflationFatFold = 0;
            inflationFatFoldHeight = 0;
            inflationFatFoldGap = 0;
            inflationRoundness = 0;
            inflationDrop = 0;
            //Also allows you to enable a gameplay disabled character
            GameplayEnabled = true;
        }

        //Allows cloning, to avoid pass by ref issues when keeping history
        public object Clone()
        {
            var clonedObj = (PregnancyPlusData)this.MemberwiseClone();

            //Memberwise clone does not deep copy, so copy any objects manually
            // var individualClothingOffsets = clonedObj.individualClothingOffsets;
            // if (individualClothingOffsets != null)
            // {
            //     clonedObj.individualClothingOffsets = individualClothingOffsets;
            // }            

            return clonedObj;
        }

        //When you want to copy the slider values of one config to another, but leave everythig else as is
        public void SetSliders(PregnancyPlusData source)
        {
            inflationSize = source.inflationSize;
            inflationMoveY = source.inflationMoveY;
            inflationMoveZ = source.inflationMoveZ;
            inflationStretchX = source.inflationStretchX;
            inflationStretchY = source.inflationStretchY;
            inflationShiftY = source.inflationShiftY;
            inflationShiftZ = source.inflationShiftZ;
            inflationTaperY = source.inflationTaperY;
            inflationTaperZ = source.inflationTaperZ;
            inflationMultiplier = source.inflationMultiplier;
            inflationClothOffset = source.inflationClothOffset;
            inflationFatFold = source.inflationFatFold;
            inflationFatFoldHeight = source.inflationFatFoldHeight;
            inflationFatFoldGap = source.inflationFatFoldGap;
            inflationRoundness = source.inflationRoundness;
            inflationDrop = source.inflationDrop;
        }

        public string ValuesToString() 
        {
            return $"v{GetPluginVersion()} inflationSize {inflationSize} GameplayEnabled {GameplayEnabled} BS {HasBlendShape()}";
        }

        //Allow comparison between all public properties of two PregnancyPlusData objects (excluding clothingOffsetVersion)
        public override bool Equals(Object obj)
        {
            if (obj == null) return false;
            var otherData = obj as PregnancyPlusData;
            if (otherData == null) return false;

            //Compare this class instance values to another
            if (inflationSize != otherData.inflationSize) return false;              
            if (inflationMoveY != otherData.inflationMoveY) return false;
            if (inflationMoveZ != otherData.inflationMoveZ) return false;
            if (inflationStretchX != otherData.inflationStretchX) return false;
            if (inflationStretchY != otherData.inflationStretchY) return false;
            if (inflationShiftY != otherData.inflationShiftY) return false;
            if (inflationShiftZ != otherData.inflationShiftZ) return false;
            if (inflationTaperY != otherData.inflationTaperY) return false;
            if (inflationTaperZ != otherData.inflationTaperZ) return false;
            if (inflationMultiplier != otherData.inflationMultiplier) return false;
            if (inflationClothOffset != otherData.inflationClothOffset) return false;
            if (inflationFatFold != otherData.inflationFatFold) return false;           
            if (inflationFatFoldHeight != otherData.inflationFatFoldHeight) return false;           
            if (inflationFatFoldGap != otherData.inflationFatFoldGap) return false;           
            if (inflationRoundness != otherData.inflationRoundness) return false;                      
            if (inflationDrop != otherData.inflationDrop) return false;       

            var offsets = IndividualClothingOffsets;
            var otherOffsets = otherData.IndividualClothingOffsets;  
            if (offsets != null || otherOffsets != null) 
            {
                if (offsets == null && otherOffsets != null) return false;
                if (offsets != null && otherOffsets == null) return false;
                if (offsets.Count != otherOffsets.Count) return false;

                for (int i = 0; i < offsets.Count; i++)
                {
                    if (offsets[i].Value != otherOffsets[i].Value) return false;
                }
            }                     

            return true;
        }


        //Allow comparison between all public properties of two PregnancyPlusData objects (excluding clothingOffsetVersion)
        public bool InflationSizeOnlyChange(Object obj)
        {
            if (obj == null) return false;
            var otherData = obj as PregnancyPlusData;
            if (otherData == null) return false;
            
            if (inflationSize == otherData.inflationSize) return false;              
            //Compare this class instance values to another
            if (inflationMoveY != otherData.inflationMoveY) return false;
            if (inflationMoveZ != otherData.inflationMoveZ) return false;
            if (inflationStretchX != otherData.inflationStretchX) return false;
            if (inflationStretchY != otherData.inflationStretchY) return false;
            if (inflationShiftY != otherData.inflationShiftY) return false;
            if (inflationShiftZ != otherData.inflationShiftZ) return false;
            if (inflationTaperY != otherData.inflationTaperY) return false;
            if (inflationTaperZ != otherData.inflationTaperZ) return false;
            if (inflationMultiplier != otherData.inflationMultiplier) return false;
            if (inflationClothOffset != otherData.inflationClothOffset) return false;
            if (inflationFatFold != otherData.inflationFatFold) return false;           
            if (inflationFatFoldHeight != otherData.inflationFatFoldHeight) return false;           
            if (inflationFatFoldGap != otherData.inflationFatFoldGap) return false;           
            if (inflationRoundness != otherData.inflationRoundness) return false;                      
            if (inflationDrop != otherData.inflationDrop) return false;  

            var offsets = IndividualClothingOffsets;
            var otherOffsets = otherData.IndividualClothingOffsets;                      
            if (offsets != null || otherOffsets != null) 
            {
                if (offsets == null && otherOffsets != null) return false;
                if (offsets != null && otherOffsets == null) return false;
                if (offsets.Count != otherOffsets.Count) return false;

                for (int i = 0; i < offsets.Count; i++)
                {
                    if (offsets[i].Value != otherOffsets[i].Value) return false;
                }
            }                  

            return true;
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
                inflationFatFoldHeight.GetHashCode() +
                individualClothingOffsets.GetHashCode() +
                inflationFatFoldGap.GetHashCode();

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

        /// <summary>   
        /// Returns true when the plugin version is below the input
        /// </summary>
        public bool IsPluginVersionBelow(double version) 
        {
            var existingVersion = GetPluginVersion();
            if (existingVersion == null || existingVersion == "0") 
                return true;

            double.TryParse(existingVersion, out double dubExistingVersion);
            //If we cant parse version, assume latest version
            if (dubExistingVersion == double.NaN || dubExistingVersion == 0) return false;

            return dubExistingVersion < version;
        }
        
        /// <summary>   
        /// When a character card is pre 3.6 load the character with the old belly size and shape logic, so it doesnt look off in new versions
        /// </summary>
        public bool UseOldCalcLogic()
        {
            var oldCard = !VersionExists() && HasAnyValue() && !HasBlendShape();
            if (oldCard && PregnancyPlusPlugin.DebugLog.Value)  PregnancyPlusPlugin.Logger.LogInfo($" Old preg+ card detected v{GetPluginVersion()} UseOldCalcLogic()");
            return oldCard;
        }

        public bool VersionExists()
        {
            return pluginVersion != null;
        }

        public bool HasBlendShape()
        {
            return meshBlendShape != null && meshBlendShape.Length > 0;
        }

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
                //Skip the below fields always, we don't care if they changed in here
                if (fieldInfo.Name == "pluginVersion") continue;
                if (fieldInfo.Name == "clothingOffsetVersion") continue;

                var value = fieldInfo.GetValue(this);
                var defaultValue = fieldInfo.GetValue(_default);
                if (!Equals(defaultValue, value)) 
                {
                    return true;
                }
            }

            return false;
        }

#region Save/Load (Thanks for the code Marco)
    

        private static readonly PregnancyPlusData _default = new PregnancyPlusData();
        private static readonly FieldInfo[] _serializedFields = typeof(PregnancyPlusData).GetFields(BindingFlags.Public | BindingFlags.Instance);

        public static PregnancyPlusData Load(PluginData data)
        {
            //On new card return null
            if (data?.data == null) return null;

            var result = new PregnancyPlusData();
            foreach (var fieldInfo in _serializedFields)
            {
                if (data.data.TryGetValue(fieldInfo.Name, out var val))
                {
                    try
                    {
                        if (fieldInfo.FieldType.IsEnum) val = (int)val;
                        fieldInfo.SetValue(result, val);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
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
            
            foreach (var fieldInfo in _serializedFields)
            {
                var value = fieldInfo.GetValue(this);                
                var defaultValue = fieldInfo.GetValue(_default);

                // Check if any value is different than default, if not then don't save any data
                if (!Equals(defaultValue, value)) 
                {
                    result.data.Add(fieldInfo.Name, value);

                    //Skip the below fields always, we don't care if they changed in here
                    if (fieldInfo.Name == "pluginVersion") continue;
                }
            }

            //When we don't want to change any of the below values (just replace a single val or two)
            if (hotSwap) return result.data.Count > 0 ? result : null;

            //Always update plugin version on saving card, helps with debugging
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