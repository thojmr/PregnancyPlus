using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;

namespace KK_PregnancyPlus
{
    //This contains extra methods used for blendshpe creation
    public static class BlendShapeTools
    {
        
        /// <summary>
        /// Subtract the vectors to get deltas
        ///     rotationUndo: When the SMR has local rotation, we need to make the deltas align to that
        /// </summary>
        public static Vector3[] GetV3Deltas(Vector3[] origins, Vector3[] targets, Matrix4x4 rotationUndo) 
        {
            var deltas = new Vector3[origins.Length];

            for (var i = 0; i < origins.Length; i++) 
            {
                //Dont want the extra overhead of matrix multiplication if we don't need it
                if (rotationUndo.Equals(Matrix4x4.identity))
                    deltas[i] = targets[i] - origins[i];
                else
                    deltas[i] = rotationUndo.MultiplyPoint3x4(targets[i] - origins[i]);
            }

            return deltas;
        }


        public static Mesh CopyMesh(Mesh mesh)
        {
            //Copy mesh the unity way (before I did it one field at a time, and that missed some fields)
            return (Mesh)UnityEngine.Object.Instantiate(mesh);
        }


        public static Vector3[] ConvertV4ToV3(Vector4[] v4s) 
        {
            var v3s = new Vector3[v4s.Length];

            for (var i = 0; i < v4s.Length; i++) 
            {
                v3s[i] = new Vector3(v4s[i].x, v4s[i].y, v4s[i].z);
                i++;
            }

            return v3s;
        }
    }

}