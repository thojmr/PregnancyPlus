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
        public bool GameplayEnabled = true;
        public List<PregnancyPlusCharaController.MeshBlendShape> meshBlendShape = new List<PregnancyPlusCharaController.MeshBlendShape>();

#endregion


        //Allows cloning, to avoid pass by ref issues when keeping history
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string ValuesToString() {
            return $" inflationSize {inflationSize} inflationMultiplier {inflationMultiplier} GameplayEnabled {GameplayEnabled} ...";
        }

        //Allow comparison between all public properties of two PregnancyPlusData objects
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

            return hasChanges;
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
                inflationFatFold.GetHashCode();

            return hashCode;            
        }

#region Save/Load (Thanks for the code Marco)

        public bool HasAnyValue() {

            foreach (var fieldInfo in _serializedFields)
            {
                var value = fieldInfo.GetValue(this);
                var defaultValue = fieldInfo.GetValue(_default);
                if (!Equals(defaultValue, value)) {
                    return true;
                }
            }

            return false;
        }

        private static readonly PregnancyPlusData _default = new PregnancyPlusData();
        private static readonly FieldInfo[] _serializedFields = typeof(PregnancyPlusData).GetFields(BindingFlags.Public | BindingFlags.Instance);

        public static PregnancyPlusData Load(PluginData data)
        {
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

        public PluginData Save()
        {
            var result = new PluginData { version = 1 };
            foreach (var fieldInfo in _serializedFields)
            {
                var value = fieldInfo.GetValue(this);
                // Check if any value is different than default, if not then don't save any data
                var defaultValue = fieldInfo.GetValue(_default);

                if (!Equals(defaultValue, value))
                    result.data.Add(fieldInfo.Name, value);
            }

            return result.data.Count > 0 ? result : null;
        }

#endregion

    }
}