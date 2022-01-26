using UnityEngine;
using System;

namespace KK_PregnancyPlus
{
    //This contains extra methods used for blendshpe creation
    public static class BlendShapeTools
    {
        
        /// <summary>
        /// Subtract the vectors lists to get deltas lists
        ///     undoMatrix: When the SMR has local rotation or bindpose scale, we need to add it to the deltas for the blendshape to look/align correcty
        /// </summary>
        public static Vector3[] GetV3Deltas(Vector3[] origins, Vector3[] targets, Matrix4x4 undoTfMatrix, bool[] alteredVerts) 
        {
            var deltas = new Vector3[origins.Length];
            var hasTransform = undoTfMatrix != Matrix4x4.identity;

            for (var i = 0; i < origins.Length; i++) 
            {
                //If the vert has not been altered, no delta change
                if (!alteredVerts[i]) continue;

                deltas[i] = GetV3Delta(origins[i], targets[i], undoTfMatrix, hasTransform);
            }

            return deltas;
        }


        /// <summary>
        /// Subtract the vectors lists to get deltas lists (Overload for Vector4 tangents)
        /// </summary>
        public static Vector3[] GetV3Deltas(Vector4[] origins, Vector4[] targets, Matrix4x4 undoTfMatrix, bool[] alteredVerts) 
        {
            var deltas = new Vector3[origins.Length];
            var hasTransform = undoTfMatrix != Matrix4x4.identity;

            for (var i = 0; i < origins.Length; i++) 
            {
                //If the vert has not been altered, no delta change                
                if (!alteredVerts[i]) continue;

                //I guess Unity knows how to automatically convert from V4 to V3 since there are no compile errors here?
                deltas[i] = GetV3Delta(origins[i], targets[i], undoTfMatrix, hasTransform);
            }

            return deltas;
        }


        /// <summary>
        /// Subtract two vectors to get their delta        
        /// </summary>
        public static Vector3 GetV3Delta(Vector3 origin, Vector3 target, Matrix4x4 undoTfMatrix, bool hasTransform)
        {
            //Dont want the extra overhead of matrix multiplication if we don't need it
            if (!hasTransform)
                return target - origin;
            else
                return undoTfMatrix.MultiplyPoint3x4(target - origin);
        }

    }

}