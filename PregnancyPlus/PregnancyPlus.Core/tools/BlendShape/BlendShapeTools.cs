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
        ///     undoMatrix: When the SMR has local rotation or bindpose scale, we need to add it to the deltas for the blendshape to look/align correcty
        /// </summary>
        public static Vector3[] GetV3Deltas(Vector3[] origins, Vector3[] targets, Matrix4x4 undoMatrix, bool[] alteredVerts) 
        {
            var deltas = new Vector3[origins.Length];

            for (var i = 0; i < origins.Length; i++) 
            {
                //If the vert has not been altered, no delta change
                if (!alteredVerts[i]) continue;

                //Dont want the extra overhead of matrix multiplication if we don't need it
                if (undoMatrix.Equals(Matrix4x4.identity))
                    deltas[i] = targets[i] - origins[i];
                else
                    deltas[i] = undoMatrix.MultiplyPoint3x4(targets[i] - origins[i]);
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